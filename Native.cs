using System.Runtime.InteropServices;
using System.Text;

namespace DirectFolders;

internal static class Native
{
    public const int WH_MOUSE_LL = 14;
    public const int WM_NULL = 0x0000;
    public const int WM_SETTEXT = 0x000C;
    public const int WM_GETTEXT = 0x000D;
    public const int WM_GETTEXTLENGTH = 0x000E;
    public const int WM_COMMAND = 0x0111;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP = 0x0208;
    public const int WM_HOTKEY = 0x0312;
    public const int WM_APP = 0x8000;
    public const int BM_CLICK = 0x00F5;

    public const uint LLMHF_INJECTED = 0x1;
    public const uint GA_ROOT = 2;
    public const uint SMTO_ABORTIFHUNG = 0x2;

    public const uint MF_STRING = 0x0;
    public const uint MF_GRAYED = 0x1;
    public const uint MF_POPUP = 0x10;
    public const uint MF_SEPARATOR = 0x800;
    public const uint MF_BYPOSITION = 0x400;
    public const uint TPM_RIGHTBUTTON = 0x2;
    public const uint TPM_NONOTIFY = 0x80;
    public const uint TPM_RETURNCMD = 0x100;

    public const uint MOD_ALT = 0x1;
    public const uint MOD_CONTROL = 0x2;
    public const uint MOD_SHIFT = 0x4;
    public const uint MOD_WIN = 0x8;
    public const uint MOD_NOREPEAT = 0x4000;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? name);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT pt);
    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern int GetDlgCtrlID(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern IntPtr GetDlgItem(IntPtr dlg, int id);
    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    public static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageTimeoutBuf(IntPtr hwnd, int msg, IntPtr wParam, StringBuilder lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageTimeoutStr(IntPtr hwnd, int msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool AppendMenu(IntPtr menu, uint flags, IntPtr idOrSub, string? text);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool InsertMenu(IntPtr menu, uint position, uint flags, IntPtr idOrSub, string? text);
    [DllImport("user32.dll")]
    public static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")]
    public static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr tpm);
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vk);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [DllImport("user32.dll")]
    public static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")]
    public static extern bool PostThreadMessage(uint threadId, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? cls, string? title);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int max);

    public static string WindowText(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "";
        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    /// <summary>
    /// 이 범위 안에서 만든 창은 시스템 DPI 인식으로 동작한다.
    /// 배율이 다른 모니터가 섞인 환경에서 WinForms의 모니터별 배율 처리가 일부 컨트롤에만 적용되는 문제를 피하기 위해
    /// 설정 창 등 일반 폼에 사용한다(다른 배율 모니터에서는 Windows가 확대/축소해 그림). 훅·메뉴 좌표 처리에는 영향 없음.
    /// </summary>
    public readonly struct SystemDpiScope : IDisposable
    {
        private readonly IntPtr _old;
        public SystemDpiScope() => _old = SetThreadDpiAwarenessContext(new IntPtr(-2)); // DPI_AWARENESS_CONTEXT_SYSTEM_AWARE
        public void Dispose()
        {
            if (_old != IntPtr.Zero) SetThreadDpiAwarenessContext(_old);
        }
    }

    /// <summary>폼의 창 핸들을 시스템 DPI 인식으로 만든다(자식 컨트롤은 부모를 따름). 모달 루프는 범위 밖에서 돈다.</summary>
    public static T WithSystemDpi<T>(T form) where T : Form
    {
        using (new SystemDpiScope())
        {
            _ = form.Handle;
        }
        return form;
    }

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr hmod, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hook);

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    /// <summary>재전송한 클릭임을 훅에서 알아보기 위한 표식(dwExtraInfo).</summary>
    public static readonly IntPtr ReplayTag = (IntPtr)0x44465245;

    /// <summary>가로챘던 휠 클릭을 원래 대상에 다시 보낸다(훅은 표식을 보고 통과시킴).</summary>
    public static async Task ReplayMiddleClickAsync()
    {
        uint down = SendInput(1, new[] { new INPUT { type = 0, mi = new MOUSEINPUT { dwFlags = 0x0020, dwExtraInfo = ReplayTag } } }, Marshal.SizeOf<INPUT>());
        await Task.Delay(40);
        uint up = SendInput(1, new[] { new INPUT { type = 0, mi = new MOUSEINPUT { dwFlags = 0x0040, dwExtraInfo = ReplayTag } } }, Marshal.SizeOf<INPUT>());
        Log.Write($"replay middle click: down={down} up={up} err={Marshal.GetLastWin32Error()}");
    }

    public static string ClassOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>다른 프로세스 컨트롤의 텍스트를 읽는다. 실패(무응답·UIPI 차단) 시 null.</summary>
    public static string? GetText(IntPtr hwnd)
    {
        if (SendMessageTimeout(hwnd, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 500, out var len) == IntPtr.Zero)
            return null;
        var sb = new StringBuilder((int)len + 1);
        if (SendMessageTimeoutBuf(hwnd, WM_GETTEXT, (IntPtr)sb.Capacity, sb, SMTO_ABORTIFHUNG, 500, out _) == IntPtr.Zero)
            return null;
        return sb.ToString();
    }

    public static bool SetText(IntPtr hwnd, string text)
    {
        return SendMessageTimeoutStr(hwnd, WM_SETTEXT, IntPtr.Zero, text, SMTO_ABORTIFHUNG, 500, out _) != IntPtr.Zero;
    }

    /// <summary>포그라운드 잠금을 우회해 창을 활성화한다.</summary>
    public static void ForceForeground(IntPtr hwnd)
    {
        var fg = GetForegroundWindow();
        uint fgThread = fg == IntPtr.Zero ? 0 : GetWindowThreadProcessId(fg, out _);
        uint cur = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != cur && AttachThreadInput(cur, fgThread, true);
        SetForegroundWindow(hwnd);
        if (attached) AttachThreadInput(cur, fgThread, false);
    }
}
