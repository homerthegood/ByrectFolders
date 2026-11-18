namespace DirectFolders;

/// <summary>
/// 다른 프로세스의 열기/저장 대화상자(구형 GetOpenFileName, 신형 IFileDialog 공통)를 판별하고 폴더를 이동시킨다.
/// 방식: 파일 이름 칸 내용 보관 → 폴더 경로 입력 → 확인 → 칸 내용 복원.
/// </summary>
internal static class FileDialogNavigator
{
    // 1148: cmb13(열기 대화상자·구형 Explorer 스타일 파일 이름 콤보)
    // 1001: 신형 저장 대화상자 FloatNotifySink > ComboBox > Edit(1001)
    // 1152: edt1(구형 파일 이름 에디트)
    private const int IdFileNameCombo = 1148;
    private const int IdFileNameEdit = 1152;
    private const int IdSaveFileNameCombo = 1001;
    private const int IdOk = 1;

    public static bool IsFileDialog(IntPtr root)
    {
        return root != IntPtr.Zero
            && Native.ClassOf(root) == "#32770"
            && FindFileNameEdit(root) != IntPtr.Zero;
    }

    /// <summary>
    /// 저장 대화상자인지 판별. 신형 저장 대화상자 구조(1001)이거나 확인 버튼 글자가 저장/Save면 저장으로 본다.
    /// </summary>
    public static bool IsSaveDialog(IntPtr dlg)
    {
        IntPtr edit = FindFileNameEdit(dlg);
        if (edit != IntPtr.Zero && Native.GetDlgCtrlID(edit) == IdSaveFileNameCombo) return true;
        string ok = Native.WindowText(Native.GetDlgItem(dlg, IdOk));
        return ok.Contains("저장") || ok.Contains("Save", StringComparison.OrdinalIgnoreCase);
    }

    public static IntPtr FindFileNameEdit(IntPtr dlg)
    {
        // 우선순위: 1148(열기·구형) > 1001(신형 저장) > 1152(구형 edt1)
        IntPtr combo1148 = IntPtr.Zero, save1001 = IntPtr.Zero, edt1152 = IntPtr.Zero;
        Native.EnumChildWindows(dlg, (h, _) =>
        {
            if (!Native.IsWindowVisible(h)) return true;
            int id = Native.GetDlgCtrlID(h);
            if (id != IdFileNameCombo && id != IdSaveFileNameCombo && id != IdFileNameEdit) return true;
            string cls = Native.ClassOf(h);
            string parent = Native.ClassOf(Native.GetParent(h));
            if (id == IdFileNameCombo && combo1148 == IntPtr.Zero && cls is "ComboBoxEx32" or "ComboBox" or "Edit")
                combo1148 = h;
            else if (id == IdSaveFileNameCombo && save1001 == IntPtr.Zero && cls == "Edit" && parent == "ComboBox"
                     && Native.ClassOf(Native.GetParent(Native.GetParent(h))) == "FloatNotifySink")
                save1001 = h;
            else if (id == IdFileNameEdit && edt1152 == IntPtr.Zero && cls == "Edit" && parent == "#32770")
                edt1152 = h;
            return true;
        }, IntPtr.Zero);

        IntPtr host = combo1148 != IntPtr.Zero ? combo1148 : save1001 != IntPtr.Zero ? save1001 : edt1152;
        if (host == IntPtr.Zero) return IntPtr.Zero;
        if (Native.ClassOf(host) == "Edit") return host;

        IntPtr edit = IntPtr.Zero;
        Native.EnumChildWindows(host, (h, _) =>
        {
            if (Native.ClassOf(h) == "Edit")
            {
                edit = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return edit;
    }

    /// <summary>
    /// 대화상자의 현재 폴더. 주소 표시줄(ToolbarWindow32, "주소: …" / "Address: …") 글자에서 얻는다.
    /// 알려진 폴더(다운로드 등)는 표시 이름으로 나오므로 실제 경로로 바꾼다. 판별 불가면 null.
    /// </summary>
    public static string? GetCurrentFolder(IntPtr dlg)
    {
        string? text = null;
        Native.EnumChildWindows(dlg, (h, _) =>
        {
            if (Native.ClassOf(h) != "ToolbarWindow32") return true;
            string t = Native.WindowText(h);
            foreach (var prefix in new[] { "주소: ", "Address: " })
            {
                if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    text = t[prefix.Length..].Trim();
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        if (string.IsNullOrEmpty(text)) return null;
        if (Path.IsPathRooted(text) && Directory.Exists(text)) return text;

        // "다운로드" 또는 "다운로드\하위" 형태
        string head = text.Split('\\')[0];
        string rest = text.Length > head.Length ? text[(head.Length + 1)..] : "";
        foreach (var (name, path) in KnownFolders.Value)
        {
            if (!string.Equals(name, head, StringComparison.OrdinalIgnoreCase)) continue;
            string full = rest.Length == 0 ? path : Path.Combine(path, rest);
            if (Directory.Exists(full)) return full;
        }
        Log.Write($"dialog folder unresolved: '{text}'");
        return null;
    }

    private static readonly Lazy<List<(string Name, string Path)>> KnownFolders = new(() =>
    {
        var list = new List<(string, string)>();
        foreach (var sf in new[]
        {
            Environment.SpecialFolder.Desktop, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures,
            Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.UserProfile,
        })
        {
            string p = Environment.GetFolderPath(sf);
            if (p.Length > 0) list.Add((DisplayName(p), p));
        }
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads)) list.Add((DisplayName(downloads), downloads));
        return list;
    });

    private static string DisplayName(string path)
    {
        var sfi = new SHFILEINFO();
        SHGetFileInfo(path, 0, ref sfi, (uint)System.Runtime.InteropServices.Marshal.SizeOf<SHFILEINFO>(), 0x200 /*SHGFI_DISPLAYNAME*/);
        return string.IsNullOrEmpty(sfi.szDisplayName) ? Path.GetFileName(path) : sfi.szDisplayName;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO sfi, uint size, uint flags);

    /// <summary>폴더로 이동한 뒤 파일 이름 칸에 이름을 넣는다(최근 파일 선택용).</summary>
    public static async Task<bool> NavigateToFileAsync(IntPtr dlg, string file)
    {
        string? dir = Path.GetDirectoryName(file);
        if (dir == null || !await NavigateAsync(dlg, dir)) return false;
        IntPtr edit = FindFileNameEdit(dlg);
        return edit != IntPtr.Zero && Native.SetText(edit, Path.GetFileName(file));
    }

    /// <summary>대화상자를 폴더로 이동시킨다. 이동을 확인하지 못하면 false(호출 측에서 탐색기로 대체).</summary>
    public static async Task<bool> NavigateAsync(IntPtr dlg, string folder)
    {
        IntPtr edit = FindFileNameEdit(dlg);
        Log.Write($"navigate dlg={dlg} edit={edit} id={Native.GetDlgCtrlID(edit)} folder={folder}");
        if (edit == IntPtr.Zero) return false;

        string? original = Native.GetText(edit);
        Log.Write($"  original='{original}'");
        if (original == null) return false;           // 무응답 또는 권한 차단

        if (!Native.SetText(edit, folder) || Native.GetText(edit) != folder)
        {
            Log.Write("  set text failed");
            Native.SetText(edit, original);
            return false;
        }

        IntPtr ok = Native.GetDlgItem(dlg, IdOk);
        if (ok != IntPtr.Zero)
            Native.PostMessage(ok, Native.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        else
            Native.PostMessage(dlg, Native.WM_COMMAND, (IntPtr)IdOk, IntPtr.Zero);

        // 폴더로 이동하면 대화상자가 파일 이름 칸을 비우거나 바꾼다. 그대로면 실패로 본다.
        bool navigated = false;
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(50);
            if (!Native.IsWindow(dlg)) return true;   // 대화상자가 닫힘(정상 동작은 아니지만 재시도 불가)
            if (!Native.IsWindow(edit)) edit = FindFileNameEdit(dlg);
            if (edit == IntPtr.Zero) break;
            string? now = Native.GetText(edit);
            if (i % 5 == 0) Log.Write($"  poll {i}: '{now}'");
            if (now != null && now != folder)
            {
                navigated = true;
                break;
            }
        }

        if (edit != IntPtr.Zero) Native.SetText(edit, original);
        Log.Write($"  result navigated={navigated}");
        return navigated;
    }
}
