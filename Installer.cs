using System.Diagnostics;
using Microsoft.Win32;

namespace DirectFolders;

/// <summary>
/// 사용자 단위 설치(관리자 권한 불필요).
/// %LOCALAPPDATA%\Programs\ByrectFolders 에 exe 복사, 시작 메뉴 바로가기, '앱 및 기능' 제거 항목 등록.
/// 설정(%APPDATA%\ByrectFolders)은 설치·제거와 별개로 유지된다(제거 시 선택 삭제).
/// </summary>
internal static class Installer
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppInfo.Id;
    private const string LegacyUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppInfo.LegacyId;

    /// <summary>이전 이름(DirectFolders)으로 설치된 흔적 정리: 제거 항목, 시작 메뉴 바로가기, 설치 폴더.</summary>
    private static void RemoveLegacyInstall()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(LegacyUninstallKey, throwOnMissingSubKey: false);
            string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppInfo.LegacyId + ".lnk");
            if (File.Exists(lnk)) File.Delete(lnk);
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppInfo.LegacyId);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex)
        {
            Log.Write("legacy install cleanup failed: " + ex.Message);
        }
    }

    public static string InstallDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppInfo.Id);

    public static string InstalledExe => Path.Combine(InstallDir, AppInfo.Id + ".exe");

    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppInfo.Name + ".lnk");

    public static bool IsRunningInstalledCopy =>
        string.Equals(Path.GetFullPath(Environment.ProcessPath ?? ""), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);

    public static void Install(bool autostart)
    {
        RemoveLegacyInstall();
        Directory.CreateDirectory(InstallDir);
        File.Copy(Environment.ProcessPath!, InstalledExe, overwrite: true);
        CreateShortcut(ShortcutPath, InstalledExe);

        using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            string version = typeof(Installer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            key.SetValue("DisplayName", AppInfo.Name);
            key.SetValue("DisplayVersion", version);
            key.SetValue("Publisher", "개인용");
            key.SetValue("DisplayIcon", InstalledExe);
            key.SetValue("InstallLocation", InstallDir);
            key.SetValue("UninstallString", $"\"{InstalledExe}\" --uninstall");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
        }

        if (autostart) Startup.Set(true, InstalledExe);
        else if (Startup.IsEnabled()) Startup.Set(true, InstalledExe); // 기존 등록은 설치본 경로로 교체
        Log.Write($"installed to {InstallDir}, autostart={autostart}");
    }

    /// <summary>실행 중인 인스턴스가 있으면 종료시키고 최대 3초 기다린다.</summary>
    public static void CloseRunningInstance()
    {
        IntPtr host = Native.FindWindow(null, TrayApp.HostCaption);
        if (host == IntPtr.Zero) return;
        Native.PostMessage(host, TrayApp.WmExit, IntPtr.Zero, IntPtr.Zero);
        for (int i = 0; i < 30 && Native.FindWindow(null, TrayApp.HostCaption) != IntPtr.Zero; i++) Thread.Sleep(100);
        Thread.Sleep(300); // 프로세스 종료·파일 잠금 해제 여유
    }

    /// <summary>--uninstall 실행 시 호출. 실행 중인 인스턴스를 닫은 뒤 제거한다.</summary>
    public static void Uninstall(bool quiet)
    {
        if (!quiet && MessageBox.Show(AppInfo.Name + "를 제거할까요?", AppInfo.Name + " 제거",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;

        CloseRunningInstance();
        Startup.Set(false);
        try { File.Delete(ShortcutPath); } catch { }
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);

        bool removeSettings = !quiet && MessageBox.Show(
            $"설정(즐겨찾기)도 삭제할까요?\n{SettingsStore.Dir}\n\n'아니요'를 누르면 재설치 시 그대로 사용됩니다.",
            AppInfo.Name + " 제거", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        // 실행 중인 자기 자신(exe)은 바로 지울 수 없으므로 종료 후 cmd로 삭제
        string cmd = $"/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{InstallDir}\"";
        if (removeSettings) cmd += $" & rmdir /s /q \"{SettingsStore.Dir}\"";
        Process.Start(new ProcessStartInfo("cmd.exe", cmd) { CreateNoWindow = true, UseShellExecute = false });
        Log.Write($"uninstalled (settings removed={removeSettings})");
        if (!quiet) MessageBox.Show("제거했습니다.", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void CreateShortcut(string lnkPath, string target)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell 없음");
        dynamic shell = Activator.CreateInstance(type)!;
        dynamic lnk = shell.CreateShortcut(lnkPath);
        lnk.TargetPath = target;
        lnk.WorkingDirectory = Path.GetDirectoryName(target);
        lnk.Description = "즐겨찾기 폴더 메뉴";
        lnk.Save();
    }
}
