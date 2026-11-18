namespace DirectFolders;

/// <summary>%APPDATA%\ByrectFolders\log.txt 에 동작 기록을 남긴다(앱별 실패 원인 확인용).</summary>
internal static class Log
{
    private static readonly string FilePath = Path.Combine(SettingsStore.Dir, "log.txt");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Dir);
            var fi = new FileInfo(FilePath);
            if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
            File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // 로그 실패는 무시
        }
    }
}
