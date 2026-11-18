using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DirectFolders;

internal sealed class FavoriteItem
{
    public string? Name { get; set; }
    public string? Path { get; set; }

    /// <summary>이 폴더로 바로 이동하는 전역 단축키(예: "Ctrl+Alt+1").</summary>
    public string? Hotkey { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Separator { get; set; }

    public List<FavoriteItem>? Items { get; set; }

    [JsonIgnore]
    public string? ExpandedPath => string.IsNullOrWhiteSpace(Path) ? null : Environment.ExpandEnvironmentVariables(Path);

    [JsonIgnore]
    public bool IsGroup => Items != null;

    public static IEnumerable<FavoriteItem> Flatten(IEnumerable<FavoriteItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            if (item.Items != null)
                foreach (var child in Flatten(item.Items)) yield return child;
        }
    }
}

/// <summary>앱별 기본 폴더 규칙: 해당 앱에서 열기/저장 대화상자가 뜨면 자동으로 이동.</summary>
internal sealed class AppFolderRule
{
    /// <summary>실행 파일 이름(예: "3dsmax.exe", 확장자 생략 가능).</summary>
    public string App { get; set; } = "";
    public string Path { get; set; } = "";
    /// <summary>"both"(기본) / "open" / "save".</summary>
    public string? Dialog { get; set; }
    /// <summary>대화상자 제목에 이 글자가 들어 있을 때만 적용(선택).</summary>
    public string? Title { get; set; }

    [JsonIgnore]
    public string ExpandedPath => Environment.ExpandEnvironmentVariables(Path);

    [JsonIgnore]
    public string DialogKind => Dialog?.ToLowerInvariant() is "open" or "save" ? Dialog.ToLowerInvariant() : "both";

    public static string NormalizeApp(string app)
    {
        app = app.Trim();
        return app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? app : app + ".exe";
    }

    public bool Matches(string exeName, bool isSave, string title)
    {
        if (!string.Equals(NormalizeApp(App), exeName, StringComparison.OrdinalIgnoreCase)) return false;
        if (DialogKind == "open" && isSave) return false;
        if (DialogKind == "save" && !isSave) return false;
        if (!string.IsNullOrWhiteSpace(Title) && !title.Contains(Title, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}

internal sealed class Settings
{
    public string Hotkey { get; set; } = "Ctrl+Alt+D";
    public bool MiddleClick { get; set; } = true;
    public bool ShowOpenFolders { get; set; } = true;
    /// <summary>설정 창 테마: "aero"(기본) / "light".</summary>
    public string Theme { get; set; } = DirectFolders.Theme.DefaultName;
    public List<FavoriteItem> Items { get; set; } = new();
    public List<AppFolderRule> AppFolders { get; set; } = new();

    public Settings Clone() =>
        JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this, SettingsStore.WriteOptions), SettingsStore.ReadOptions)!;
}

internal static class SettingsStore
{
    public static string Dir { get; } = PrepareDir();

    /// <summary>
    /// %APPDATA%\ByrectFolders. 처음 한 번, 이전 이름 폴더(%APPDATA%\DirectFolders)가 있으면 내용을 복사해 온다.
    /// 이전 폴더는 지우지 않고 그대로 둔다(되돌릴 때 대비).
    /// </summary>
    private static string PrepareDir()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, AppInfo.Id);
        string legacy = Path.Combine(appData, AppInfo.LegacyId);
        try
        {
            if (!Directory.Exists(dir) && Directory.Exists(legacy))
            {
                foreach (var src in Directory.EnumerateFiles(legacy, "*", SearchOption.AllDirectories))
                {
                    string dst = Path.Combine(dir, Path.GetRelativePath(legacy, src));
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Copy(src, dst, overwrite: false);
                }
            }
        }
        catch
        {
            // 복사 실패 시 새 폴더에서 빈 설정으로 시작(이전 폴더는 그대로 남아 있음)
        }
        return dir;
    }

    public static string FilePath { get; } = Path.Combine(Dir, "favorites.json");

    internal static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    internal static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 한글을 \uXXXX로 바꾸지 않음
    };

    /// <summary>설정 UI에서 저장. 기존 파일은 favorites.json.bak으로 보관한다(주석은 보존되지 않음).</summary>
    public static void Save(Settings settings)
    {
        Directory.CreateDirectory(Dir);
        if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", overwrite: true);
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, WriteOptions));
        File.Move(tmp, FilePath, overwrite: true);
    }

    public static void EnsureExists()
    {
        if (File.Exists(FilePath)) return;
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, Example);
    }

    public static Settings Load(out string? error)
    {
        error = null;
        try
        {
            EnsureExists();
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), ReadOptions);
            return s ?? new Settings();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return new Settings();
        }
    }

    private const string Example = """
{
  // 메뉴 호출 단축키. 조합: Ctrl, Alt, Shift, Win + 키 이름(A~Z, F1~F12, D0~D9 등)
  "hotkey": "Ctrl+Alt+D",

  // 바탕화면·탐색기·파일 대화상자 위 휠 클릭으로 메뉴 호출 (다른 앱에서는 통과)
  "middleClick": true,

  // 현재 열려 있는 탐색기 창·탭의 폴더를 메뉴 맨 위에 표시
  "showOpenFolders": true,

  // 항목 종류
  //   폴더    : { "name": "표시 이름", "path": "경로", "hotkey": "Ctrl+Alt+1" }
  //             (환경변수 %USERPROFILE% 등 사용 가능, hotkey는 선택: 누르면 메뉴 없이 바로 이동)
  //   구분선  : { "separator": true }
  //   하위메뉴: { "name": "그룹 이름", "items": [ ... ] }
  // 존재하지 않는 경로는 회색으로 비활성 표시된다.

  // 앱별 기본 폴더: 해당 앱에서 열기/저장 대화상자가 뜨면 자동으로 이 폴더로 이동
  //   { "app": "3dsmax.exe", "path": "D:\\Project\\Max", "dialog": "both|open|save", "title": "제목 일부(선택)" }
  "appFolders": [],

  "items": [
    { "name": "바탕화면", "path": "%USERPROFILE%\\Desktop" },
    { "name": "다운로드", "path": "%USERPROFILE%\\Downloads" },
    { "name": "문서", "path": "%USERPROFILE%\\Documents" },
    { "separator": true },
    {
      "name": "작업",
      "items": [
        { "name": "D:\\work", "path": "D:\\work" },
        { "name": "없는 경로 예시", "path": "D:\\존재하지_않는_폴더" }
      ]
    }
  ]
}
""";
}
