using System.Diagnostics;

namespace DirectFolders;

/// <summary>
/// 새로 뜬 열기/저장 대화상자를 감지해 앱별 기본 폴더 규칙에 맞으면 자동으로 이동시킨다.
/// 대화상자는 뜰 때 포그라운드가 되므로 EVENT_SYSTEM_FOREGROUND만 감시한다(UI 스레드, 메시지 루프 필요).
/// 같은 대화상자는 한 번만 처리한다(사용자가 다른 폴더로 옮긴 뒤 다시 포커스해도 되돌리지 않음).
/// </summary>
internal sealed class DialogWatcher : IDisposable
{
    private readonly Native.WinEventProc _proc;
    private readonly IntPtr _hook;
    private readonly IntPtr _showHook;
    private readonly Func<Settings> _settings;
    private readonly HashSet<IntPtr> _seen = new();

    public DialogWatcher(Func<Settings> settings)
    {
        _settings = settings;
        _proc = OnEvent;
        _hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc,
            0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
        // 포그라운드를 받지 못하고 뜨는 대화상자 대비: 창 표시 이벤트도 감시
        _showHook = Native.SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, IntPtr.Zero, _proc,
            0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
        if (_hook == IntPtr.Zero || _showHook == IntPtr.Zero) Log.Write("dialog watcher: SetWinEventHook 실패");
    }

    private const uint EVENT_OBJECT_SHOW = 0x8002;

    private void OnEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero || idObject != 0) return;
        if (_settings().AppFolders.Count == 0) return;
        if (Native.ClassOf(hwnd) != "#32770") return;        if (Native.GetAncestor(hwnd, Native.GA_ROOT) != hwnd) return; // 최상위 대화상자만
        if (!_seen.Add(hwnd)) return;
        if (_seen.Count > 200) _seen.RemoveWhere(h => !Native.IsWindow(h));
        _ = HandleAsync(hwnd);
    }

    private async Task HandleAsync(IntPtr dlg)
    {
        try
        {
            // 대화상자 컨트롤이 다 만들어질 때까지 잠시 기다린다
            bool isFileDialog = false;
            for (int i = 0; i < 20 && !(isFileDialog = FileDialogNavigator.IsFileDialog(dlg)); i++)
            {
                await Task.Delay(50);
                if (!Native.IsWindow(dlg)) return;
            }
            if (!isFileDialog) return;

            string exe = ProcessName(dlg);
            bool isSave = FileDialogNavigator.IsSaveDialog(dlg);
            string title = Native.WindowText(dlg);
            var rule = _settings().AppFolders.FirstOrDefault(r => r.Matches(exe, isSave, title));
            Log.Write($"dialog opened: {exe} {(isSave ? "save" : "open")} '{title}' rule={(rule == null ? "-" : rule.Path)}");
            if (rule == null) return;

            string path = rule.ExpandedPath;
            if (!Directory.Exists(path))
            {
                Log.Write($"  app folder missing: {path}");
                return;
            }

            // 앱이 초기 폴더로 이동을 마칠 시간을 준 뒤 이동
            await Task.Delay(250);
            if (!Native.IsWindow(dlg)) return;
            bool ok = await FileDialogNavigator.NavigateAsync(dlg, path);
            Log.Write($"  auto navigate {(ok ? "ok" : "failed")}");
        }
        catch (Exception ex)
        {
            Log.Write("dialog watcher error: " + ex.Message);
        }
    }

    private static string ProcessName(IntPtr hwnd)
    {
        try
        {
            Native.GetWindowThreadProcessId(hwnd, out uint pid);
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName + ".exe";
        }
        catch
        {
            return "";
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) Native.UnhookWinEvent(_hook);
        if (_showHook != IntPtr.Zero) Native.UnhookWinEvent(_showHook);
    }
}
