using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DirectFolders;

/// <summary>
/// 휠 클릭 전역 감지. ShouldIntercept가 true인 창 위에서만 클릭을 막고, 나머지는 그대로 통과시킨다.
/// 훅은 전용 스레드에서 돈다. UI 스레드가 COM 호출·메뉴 등으로 막혀도 훅 응답이 늦어지지 않게 하기 위함이다
/// (응답이 늦으면 Windows가 훅을 건너뛰거나 조용히 해제한다).
/// ShouldIntercept·Intercepted는 훅 스레드에서 호출되므로 가볍고 스레드 안전해야 한다.
/// </summary>
internal sealed class MouseHook : IDisposable
{
    private const int WM_QUIT = 0x0012;

    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private Native.LowLevelMouseProc? _proc;
    private IntPtr _hook;
    private uint _threadId;
    private int _error;
    private bool _swallowUp;

    public Func<IntPtr, bool>? ShouldIntercept { get; set; }
    public Action<IntPtr, Native.POINT>? Intercepted { get; set; }

    public MouseHook()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "MouseHook" };
        _thread.Start();
        _ready.Wait();
        if (_hook == IntPtr.Zero) throw new Win32Exception(_error);
    }

    private void Run()
    {
        _threadId = Native.GetCurrentThreadId();
        _proc = HookProc;
        _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, Native.GetModuleHandle(null), 0);
        _error = Marshal.GetLastWin32Error();
        _ready.Set();
        if (_hook == IntPtr.Zero) return;

        while (Native.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            Native.TranslateMessage(ref msg);
            Native.DispatchMessage(ref msg);
        }
        Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == Native.WM_MBUTTONDOWN)
            {
                // 주입 입력(마우스 유틸리티의 버튼 재지정 등)도 동일하게 처리한다.
                var info = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                if (info.dwExtraInfo == Native.ReplayTag)
                    return Native.CallNextHookEx(_hook, nCode, wParam, lParam); // 우리가 재전송한 클릭
                IntPtr root = Native.GetAncestor(Native.WindowFromPoint(info.pt), Native.GA_ROOT);
                bool intercept = false;
                try { intercept = root != IntPtr.Zero && ShouldIntercept?.Invoke(root) == true; } catch { }
                if (intercept)
                {
                    _swallowUp = true;
                    Intercepted?.Invoke(root, info.pt);
                    return (IntPtr)1;
                }
            }
            else if (msg == Native.WM_MBUTTONUP && _swallowUp
                     && Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam).dwExtraInfo != Native.ReplayTag)
            {
                _swallowUp = false;
                return (IntPtr)1;
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_threadId != 0) Native.PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
    }
}
