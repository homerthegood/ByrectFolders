namespace DirectFolders;

/// <summary>
/// 앱 이름. 화면에 보이는 이름은 Name, 폴더·레지스트리·exe 이름은 Id.
/// (코드 네임스페이스·트레이 호스트 창 이름·뮤텍스는 이전 이름 DirectFolders를 유지한다:
///  이전 빌드가 실행 중이어도 같은 앱으로 인식해 중복 실행을 막기 위함.)
/// 참고: "Direct Folders"(띄어쓰기)는 가져오기 대상인 Code Sector 제품 이름이라 바꾸지 않는다.
/// </summary>
internal static class AppInfo
{
    public const string Name = "Byrect Folders";
    public const string Id = "ByrectFolders";

    /// <summary>이전 이름(설정 폴더·자동 시작·설치 항목 이전용).</summary>
    public const string LegacyId = "DirectFolders";
}
