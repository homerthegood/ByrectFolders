using System.Runtime.InteropServices;

namespace DirectFolders;

/// <summary>열려 있는 탐색기 창(탭)을 Shell.Application COM으로 조회·이동한다.</summary>
internal static class ExplorerShell
{
    private static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid IID_IShellBrowser = new("000214E2-0000-0000-C000-000000000046");

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider
    {
        [PreserveSig]
        int QueryService(in Guid guidService, in Guid riid, out IntPtr ppv);
    }

    // IShellBrowser는 IOleWindow를 상속하므로 vtable 첫 메서드가 GetWindow다.
    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowserWindow
    {
        [PreserveSig]
        int GetWindow(out IntPtr hwnd);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? title);

    private static IEnumerable<dynamic> Windows()
    {
        var type = Type.GetTypeFromProgID("Shell.Application");
        if (type == null) yield break;
        dynamic shell = Activator.CreateInstance(type)!;
        dynamic windows = shell.Windows();
        int count = windows.Count;
        for (int i = 0; i < count; i++)
        {
            dynamic? w = null;
            try { w = windows.Item(i); } catch { }
            if (w != null) yield return w;
        }
    }

    private static string? PathOf(dynamic w)
    {
        try
        {
            string? p = w.Document.Folder.Self.Path;
            return p != null && Directory.Exists(p) ? p : null; // 가상 폴더(내 PC 등) 제외
        }
        catch
        {
            return null;
        }
    }

    private static IntPtr TabHwndOf(object w)
    {
        try
        {
            if (w is not IServiceProvider sp) return IntPtr.Zero;
            if (sp.QueryService(SID_STopLevelBrowser, IID_IShellBrowser, out var ptr) != 0 || ptr == IntPtr.Zero)
                return IntPtr.Zero;
            try
            {
                var browser = (IShellBrowserWindow)Marshal.GetObjectForIUnknown(ptr);
                return browser.GetWindow(out var hwnd) == 0 ? hwnd : IntPtr.Zero;
            }
            finally
            {
                Marshal.Release(ptr);
            }
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    /// <summary>열려 있는 탐색기 창·탭의 실제 폴더 경로(중복 제거).</summary>
    public static List<string> GetOpenFolders(int max = 20)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var w in Windows())
            {
                string? p = PathOf(w);
                if (p != null && seen.Add(p)) result.Add(p);
                if (result.Count >= max) break;
            }
        }
        catch (Exception ex)
        {
            Log.Write("open folders failed: " + ex.Message);
        }
        return result;
    }

    /// <summary>탐색기 창의 활성 탭 ShellWindows 항목.</summary>
    private static dynamic? ActiveTab(IntPtr cabinet)
    {
        // Windows 11 탭 탐색기: 첫 번째 ShellTabWindowClass 자식이 활성 탭
        IntPtr activeTab = FindWindowEx(cabinet, IntPtr.Zero, "ShellTabWindowClass", null);
        dynamic? candidate = null;
        foreach (var w in Windows())
        {
            long hwnd;
            try { hwnd = (long)w.HWND; } catch { continue; }
            if (hwnd != cabinet.ToInt64()) continue;
            if (activeTab == IntPtr.Zero || TabHwndOf((object)w) == activeTab) return w;
            candidate ??= w; // 활성 탭 판별 실패 시 첫 탭
        }
        return candidate;
    }

    /// <summary>탐색기 창 활성 탭의 실제 폴더 경로(가상 폴더면 null).</summary>
    public static string? GetWindowFolder(IntPtr cabinet)
    {
        try
        {
            var tab = ActiveTab(cabinet);
            return tab == null ? null : PathOf(tab);
        }
        catch (Exception ex)
        {
            Log.Write("explorer folder failed: " + ex.Message);
            return null;
        }
    }

    /// <summary>지정한 탐색기 창의 활성 탭을 폴더로 이동한다. 새 창·새 탭을 만들지 않는다.</summary>
    public static bool NavigateWindow(IntPtr cabinet, string folder)
    {
        try
        {
            dynamic? candidate = ActiveTab(cabinet);
            if (candidate == null)
            {
                Log.Write($"explorer navigate: window {cabinet} not found in ShellWindows");
                return false;
            }
            candidate.Navigate2(folder);
            Log.Write($"explorer navigate: {cabinet} -> {folder}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Write("explorer navigate failed: " + ex.Message);
            return false;
        }
    }
}
