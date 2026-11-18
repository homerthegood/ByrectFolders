using System.Diagnostics;
using System.Text.Json;

namespace DirectFolders;

/// <summary>이 프로그램으로 이동한 폴더 기록. %APPDATA%\ByrectFolders\history.json에 보관.</summary>
internal static class FolderHistory
{
    private const int Max = 20;
    private static readonly string FilePath = Path.Combine(SettingsStore.Dir, "history.json");
    private static List<string>? _items;

    public static IReadOnlyList<string> Items => Load();

    public static void Add(string folder)
    {
        var list = Load();
        list.RemoveAll(p => string.Equals(p.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        list.Insert(0, folder);
        if (list.Count > Max) list.RemoveRange(Max, list.Count - Max);
        Save(list);
    }

    public static void Clear() => Save(new List<string>());

    /// <summary>기존 기록 뒤에 없는 폴더만 덧붙인다(가져오기용).</summary>
    public static void Merge(IEnumerable<string> folders)
    {
        var list = Load().ToList();
        foreach (var f in folders)
        {
            if (list.Count >= Max) break;
            if (!list.Any(p => string.Equals(p.TrimEnd('\\'), f.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))) list.Add(f);
        }
        Save(list);
    }

    private static List<string> Load()
    {
        if (_items != null) return _items;
        try
        {
            _items = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch (Exception ex)
        {
            Log.Write("history load failed: " + ex.Message);
            _items = new();
        }
        return _items;
    }

    private static void Save(List<string> list)
    {
        _items = list;
        try
        {
            Directory.CreateDirectory(SettingsStore.Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list, SettingsStore.WriteOptions));
        }
        catch (Exception ex)
        {
            Log.Write("history save failed: " + ex.Message);
        }
    }
}

/// <summary>Windows 최근 항목(%APPDATA%\Microsoft\Windows\Recent)의 파일 목록.</summary>
internal static class RecentFiles
{
    private static readonly Dictionary<string, (DateTime Stamp, string? Target)> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static List<string> Get(int max = 20)
    {
        var result = new List<string>();
        var sw = Stopwatch.StartNew();
        try
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            if (!Directory.Exists(dir)) return result;
            var links = new DirectoryInfo(dir).GetFiles("*.lnk")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(max * 3);

            dynamic? shell = null;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var lnk in links)
            {
                string? target;
                if (Cache.TryGetValue(lnk.FullName, out var c) && c.Stamp == lnk.LastWriteTimeUtc)
                {
                    target = c.Target;
                }
                else
                {
                    shell ??= Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                    try { target = (string?)shell!.CreateShortcut(lnk.FullName).TargetPath; }
                    catch { target = null; }
                    Cache[lnk.FullName] = (lnk.LastWriteTimeUtc, target);
                }
                if (string.IsNullOrEmpty(target) || !seen.Add(target)) continue;
                // 네트워크 경로는 존재 확인이 느릴 수 있어 확인하지 않는다
                if (!target.StartsWith(@"\\") && !File.Exists(target)) continue;
                result.Add(target);
                if (result.Count >= max) break;
            }
        }
        catch (Exception ex)
        {
            Log.Write("recent files failed: " + ex.Message);
        }
        Log.Write($"recent files: {result.Count}개, {sw.ElapsedMilliseconds}ms");
        return result;
    }
}
