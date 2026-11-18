namespace DirectFolders;

/// <summary>
/// 명령줄
///   (없음)                 트레이 상주 실행
///   --settings             설정 창 열기(실행 중이면 그 인스턴스에서, 아니면 실행 후 열기)
///   --install [--autostart] 대화상자 없이 사용자 단위 설치 후 설치본 실행
///   --uninstall [--quiet]  제거(--quiet: 확인 없이, 설정은 유지)
///   --wait                 이전 인스턴스 종료를 최대 5초 기다린 뒤 실행(설치 직후용)
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool Has(string a) => args.Contains(a, StringComparer.OrdinalIgnoreCase);

        if (Has("--uninstall"))
        {
            Installer.Uninstall(quiet: Has("--quiet"));
            return;
        }
        if (Has("--install"))
        {
            Installer.CloseRunningInstance();
            Installer.Install(autostart: Has("--autostart"));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Installer.InstalledExe, "--wait") { UseShellExecute = false });
            return;
        }

        bool openSettings = Has("--settings");
        IntPtr running = Native.FindWindow(null, TrayApp.HostCaption);
        if (openSettings && running != IntPtr.Zero)
        {
            Native.PostMessage(running, TrayApp.WmOpenSettings, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        bool wait = Has("--wait");
        using var mutex = new Mutex(false, @"Local\DirectFoldersPrototype");
        bool acquired;
        try
        {
            acquired = mutex.WaitOne(wait ? 5000 : 0);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }
        if (!acquired) return; // 이미 실행 중

        try
        {
            var app = new TrayApp();
            if (openSettings) Native.PostMessage(Native.FindWindow(null, TrayApp.HostCaption), TrayApp.WmOpenSettings, IntPtr.Zero, IntPtr.Zero);
            Application.Run(app);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}
