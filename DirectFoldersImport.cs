using System.Text.Json;
using System.Text.Json.Nodes;

namespace DirectFolders;

/// <summary>
/// Code Sector "Direct Folders"의 즐겨찾기(menu.json)와 최근 폴더(folders.txt)를 가져온다.
/// menu.json 형식: { "Direct Folders Menu": [ { "folder": "이름", "path": "경로" }, ... ] }
/// 하위 메뉴·구분선은 형식이 확인되지 않아 배열 속성이 있으면 그룹, divider/separator 키가 있으면 구분선으로 본다.
/// </summary>
internal static class DirectFoldersImport
{
    public sealed class Result
    {
        public string Source { get; init; } = "";
        public List<FavoriteItem> Items { get; init; } = new();
        public List<string> RecentFolders { get; init; } = new();
        public int Skipped { get; set; }
        public int FolderCount => FavoriteItem.Flatten(Items).Count(i => !i.IsGroup && !i.Separator);
    }

    /// <summary>menu.json 후보 위치에서 처음 발견된 파일.</summary>
    public static string? FindMenuFile()
    {
        var dirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Direct Folders"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Direct Folders"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Direct Folders"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Direct Folders"),
        };
        return dirs.Select(d => Path.Combine(d, "menu.json")).FirstOrDefault(File.Exists);
    }

    public static Result Load(string menuFile)
    {
        var root = JsonNode.Parse(File.ReadAllText(menuFile), documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        // 최상위: { "Direct Folders Menu": [...] } 또는 배열 자체
        JsonArray? list = root as JsonArray
            ?? (root as JsonObject)?.Select(p => p.Value).OfType<JsonArray>().FirstOrDefault();

        var result = new Result { Source = menuFile };
        if (list != null) result.Items.AddRange(ParseList(list, result));

        string recent = Path.Combine(Path.GetDirectoryName(menuFile)!, "folders.txt");
        if (File.Exists(recent))
        {
            result.RecentFolders.AddRange(File.ReadAllLines(recent)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && (l.StartsWith(@"\\") || Path.IsPathRooted(l))));
        }
        return result;
    }

    private static IEnumerable<FavoriteItem> ParseList(JsonArray list, Result result)
    {
        foreach (var node in list)
        {
            if (node is not JsonObject obj)
            {
                result.Skipped++;
                continue;
            }
            string? Str(params string[] keys) =>
                keys.Select(k => obj.FirstOrDefault(p => p.Key.Equals(k, StringComparison.OrdinalIgnoreCase)).Value)
                    .OfType<JsonValue>().Select(v => v.TryGetValue(out string? s) ? s : null).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

            var children = obj.Select(p => p.Value).OfType<JsonArray>().FirstOrDefault();
            string? name = Str("folder", "name", "title", "caption", "submenu", "menu", "group");
            string? path = Str("path");

            if (children != null)
            {
                yield return new FavoriteItem { Name = name ?? "그룹", Items = ParseList(children, result).ToList() };
            }
            else if (obj.Any(p => p.Key.Contains("divider", StringComparison.OrdinalIgnoreCase)
                               || p.Key.Contains("separator", StringComparison.OrdinalIgnoreCase)))
            {
                yield return new FavoriteItem { Separator = true };
            }
            else if (path != null && obj.ContainsKey("file") && !obj.ContainsKey("folder"))
            {
                result.Skipped++; // 파일 항목: 즐겨찾기는 폴더만 지원
            }
            else if (path != null)
            {
                yield return new FavoriteItem { Name = name ?? FolderName(path), Path = path };
            }
            else
            {
                result.Skipped++;
            }
        }
    }

    /// <summary>이미 있는 경로를 뺀 가져올 항목. 비게 된 그룹은 제외.</summary>
    public static List<FavoriteItem> NewItems(IEnumerable<FavoriteItem> existing, IEnumerable<FavoriteItem> imported)
    {
        var have = new HashSet<string>(
            FavoriteItem.Flatten(existing).Select(i => i.ExpandedPath).OfType<string>().Select(Norm),
            StringComparer.OrdinalIgnoreCase);
        return Filter(imported, have);
    }

    private static List<FavoriteItem> Filter(IEnumerable<FavoriteItem> items, HashSet<string> have)
    {
        var list = new List<FavoriteItem>();
        foreach (var item in items)
        {
            if (item.IsGroup)
            {
                var children = Filter(item.Items!, have);
                if (children.Any(c => !c.Separator)) list.Add(new FavoriteItem { Name = item.Name, Items = children });
            }
            else if (item.Separator)
            {
                if (list.Count > 0 && !list[^1].Separator) list.Add(item);
            }
            else if (item.ExpandedPath != null && have.Add(Norm(item.ExpandedPath)))
            {
                list.Add(item);
            }
        }
        while (list.Count > 0 && list[^1].Separator) list.RemoveAt(list.Count - 1);
        return list;
    }

    private static string Norm(string path) => path.Trim().TrimEnd('\\');

    private static string FolderName(string path)
    {
        string n = Path.GetFileName(path.TrimEnd('\\'));
        return n.Length > 0 ? n : path;
    }
}

/// <summary>첫 실행 안내 등 설정 파일과 별도로 두는 상태(%APPDATA%\ByrectFolders\state.json).</summary>
internal sealed class AppState
{
    public bool DirectFoldersImportAsked { get; set; }
    public bool AutostartAsked { get; set; }

    private static string FilePath => Path.Combine(SettingsStore.Dir, "state.json");

    public static AppState Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath), SettingsStore.ReadOptions) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, SettingsStore.WriteOptions));
        }
        catch (Exception ex)
        {
            Log.Write("state save failed: " + ex.Message);
        }
    }
}

/// <summary>네트워크 경로는 존재 확인이 오래 걸릴 수 있어(서버 무응답 시 수 초) 확인 없이 사용 가능으로 본다.</summary>
internal static class PathCheck
{
    public static bool IsUnc(string path) => path.StartsWith(@"\\");
    public static bool FolderAvailable(string? path) => path != null && (IsUnc(path) || Directory.Exists(path));
}
