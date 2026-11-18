using Microsoft.Win32;

namespace DirectFolders;

/// <summary>HKCU Run 키로 Windows 시작 시 자동 실행을 관리한다(관리자 권한 불필요).</summary>
internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppInfo.Id;
    private const string LegacyValueName = AppInfo.LegacyId;

    private static string CommandFor(string exe) => $"\"{exe}\"";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool enabled, string? exePath = null)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, CommandFor(exePath ?? Environment.ProcessPath!));
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// 등록되어 있는데 exe 위치가 바뀌었으면 현재 경로로 갱신한다.
    /// 단, 설치본이 등록되어 있으면 다른 위치(빌드 폴더 등)에서 실행해도 덮어쓰지 않는다.
    /// </summary>
    public static void RefreshPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key == null) return;
        // 이전 이름으로 등록돼 있으면 새 이름으로 옮긴다(이전 exe 경로는 아래에서 현재 경로로 갱신)
        if (key.GetValue(LegacyValueName) is string legacy)
        {
            if (key.GetValue(ValueName) == null) key.SetValue(ValueName, legacy);
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }
        if (key.GetValue(ValueName) is not string current) return;
        string mine = CommandFor(Environment.ProcessPath!);
        if (string.Equals(current, mine, StringComparison.OrdinalIgnoreCase)) return;
        if (!Installer.IsRunningInstalledCopy && current.Contains(Installer.InstallDir, StringComparison.OrdinalIgnoreCase)) return;
        key.SetValue(ValueName, mine);
    }
}
