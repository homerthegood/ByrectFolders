using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DirectFolders;

internal enum PickKind { Folder, File, AddHere, Settings }

internal readonly record struct MenuPick(PickKind Kind, string? Path = null);

/// <summary>메뉴에 넣을 내용. 호출 시점에 모아 전달한다.</summary>
internal sealed class MenuModel
{
    public IReadOnlyList<FavoriteItem> Items { get; init; } = Array.Empty<FavoriteItem>();
    public IReadOnlyList<string> OpenFolders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RecentFolders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RecentFiles { get; init; } = Array.Empty<string>();
    /// <summary>"여기 추가" 대상 폴더(호출한 창의 현재 폴더). null이면 비활성.</summary>
    public string? CurrentFolder { get; init; }
}

/// <summary>
/// Win32 팝업 메뉴. 구성:
///   폴더 ▶ (열린 탐색기 폴더 / 최근 폴더)
///   파일 ▶ (Windows 최근 파일)
///   ── 즐겨찾기 ──
///   여기 추가...
///   설정...
/// </summary>
internal static class FavoritesMenu
{
    public static MenuPick? Show(MenuModel model, IntPtr owner, Native.POINT pt)
    {
        var map = new Dictionary<int, MenuPick>();
        int nextId = 1;
        int Add(MenuPick pick)
        {
            int id = nextId++;
            map[id] = pick;
            return id;
        }

        IntPtr menu = Native.CreatePopupMenu();

        // 폴더 ▶
        IntPtr folders = Native.CreatePopupMenu();
        bool any = false;
        if (model.OpenFolders.Count > 0)
        {
            Header(folders, "열린 탐색기 폴더");
            foreach (var f in model.OpenFolders) Item(folders, Add(new(PickKind.Folder, f)), f, MenuIcons.Folder);
            any = true;
        }
        if (model.RecentFolders.Count > 0)
        {
            if (any) Native.AppendMenu(folders, Native.MF_SEPARATOR, IntPtr.Zero, null);
            Header(folders, "최근 폴더");
            foreach (var f in model.RecentFolders)
            {
                if (PathCheck.FolderAvailable(f)) Item(folders, Add(new(PickKind.Folder, f)), f, MenuIcons.Folder);
                else Item(folders, 0, f, MenuIcons.Folder, grayed: true);
            }
            any = true;
        }
        if (!any) Header(folders, "(없음)");
        Sub(menu, folders, "폴더", MenuIcons.Folder);

        // 파일 ▶
        IntPtr files = Native.CreatePopupMenu();
        if (model.RecentFiles.Count == 0) Header(files, "(없음)");
        foreach (var f in model.RecentFiles)
            Item(files, Add(new(PickKind.File, f)), Path.GetFileName(f) + "\t" + ShortDir(f), MenuIcons.ForFile(f));
        Sub(menu, files, "파일", MenuIcons.File);

        Native.AppendMenu(menu, Native.MF_SEPARATOR, IntPtr.Zero, null);

        // 즐겨찾기
        if (model.Items.Count == 0) Header(menu, "(즐겨찾기 없음)");
        else BuildFavorites(menu, model.Items, Add);

        // 여기 추가
        Native.AppendMenu(menu, Native.MF_SEPARATOR, IntPtr.Zero, null);
        if (model.CurrentFolder != null)
            Item(menu, Add(new(PickKind.AddHere, model.CurrentFolder)), "여기 추가...\t" + FolderName(model.CurrentFolder), MenuIcons.Add);
        else
            Item(menu, 0, "여기 추가...", MenuIcons.Add, grayed: true);

        // 설정
        Native.AppendMenu(menu, Native.MF_SEPARATOR, IntPtr.Zero, null);
        Item(menu, Add(new(PickKind.Settings)), "설정...", MenuIcons.Settings);

        try
        {
            Native.ForceForeground(owner);
            int cmd = Native.TrackPopupMenuEx(menu,
                Native.TPM_RETURNCMD | Native.TPM_RIGHTBUTTON | Native.TPM_NONOTIFY,
                pt.X, pt.Y, owner, IntPtr.Zero);
            Native.PostMessage(owner, Native.WM_NULL, IntPtr.Zero, IntPtr.Zero);
            return map.TryGetValue(cmd, out var pick) ? pick : null;
        }
        finally
        {
            Native.DestroyMenu(menu); // 하위 메뉴도 함께 해제됨(아이콘 비트맵은 캐시 유지)
        }
    }

    private static void BuildFavorites(IntPtr menu, IReadOnlyList<FavoriteItem> items, Func<MenuPick, int> add)
    {
        foreach (var item in items)
        {
            if (item.Separator)
            {
                Native.AppendMenu(menu, Native.MF_SEPARATOR, IntPtr.Zero, null);
                continue;
            }

            string label = item.Name ?? item.Path ?? "(이름 없음)";
            if (!string.IsNullOrWhiteSpace(item.Hotkey) && item.Items == null)
                label += "\t" + (HotkeyParser.Normalize(item.Hotkey) ?? item.Hotkey); // 오른쪽 정렬로 단축키 표시

            if (item.Items != null)
            {
                IntPtr sub = Native.CreatePopupMenu();
                if (item.Items.Count == 0) Header(sub, "(비어 있음)");
                BuildFavorites(sub, item.Items, add);
                Sub(menu, sub, label, MenuIcons.Folder);
                continue;
            }

            string? path = item.ExpandedPath;
            bool exists = PathCheck.FolderAvailable(path);
            Item(menu, exists ? add(new(PickKind.Folder, path)) : 0, label, MenuIcons.Folder, grayed: !exists);
        }
    }

    private static string Escape(string text)
    {
        // &는 밑줄 표시용 접두사이므로 이중화. 탭 뒤(오른쪽 정렬 부분)도 동일.
        return text.Replace("&", "&&");
    }

    private static void Header(IntPtr menu, string text) =>
        Native.AppendMenu(menu, Native.MF_STRING | Native.MF_GRAYED, IntPtr.Zero, Escape(text));

    private static void Item(IntPtr menu, int id, string text, IntPtr icon, bool grayed = false)
    {
        Native.AppendMenu(menu, Native.MF_STRING | (grayed ? Native.MF_GRAYED : 0), (IntPtr)id, Escape(text));
        MenuIcons.SetLast(menu, icon);
    }

    private static void Sub(IntPtr menu, IntPtr sub, string text, IntPtr icon)
    {
        Native.AppendMenu(menu, Native.MF_POPUP, sub, Escape(text));
        MenuIcons.SetLast(menu, icon);
    }

    private static string FolderName(string folder)
    {
        string name = Path.GetFileName(folder.TrimEnd('\\'));
        return string.IsNullOrEmpty(name) ? folder : name;
    }

    private static string ShortDir(string file)
    {
        string dir = Path.GetDirectoryName(file) ?? "";
        return dir.Length <= 40 ? dir : "…" + dir[^39..];
    }
}

/// <summary>메뉴 항목 아이콘. 셸 아이콘을 32비트 알파 비트맵으로 바꿔 캐시한다(디스크 접근 없음).</summary>
internal static class MenuIcons
{
    private static readonly Dictionary<string, IntPtr> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static IntPtr Folder => FromShell("folder", "x", 0x10);
    public static IntPtr File => FromShell("file", "x", 0x80);
    public static IntPtr Add => FromDrawing("add", (g, s) =>
    {
        using var pen = new Pen(Color.FromArgb(46, 160, 67), Math.Max(2f, s / 5f));
        g.DrawLine(pen, s / 2f, 2, s / 2f, s - 2);
        g.DrawLine(pen, 2, s / 2f, s - 2, s / 2f);
    });

    /// <summary>톱니바퀴(설정).</summary>
    public static IntPtr Settings => FromDrawing("settings", (g, s) =>
    {
        float c = s / 2f, outer = s * 0.47f, body = s * 0.34f, hole = s * 0.14f;
        using var brush = new SolidBrush(Color.FromArgb(96, 104, 118));
        var state = g.Save();
        g.TranslateTransform(c, c);
        for (int i = 0; i < 8; i++) // 톱니 8개
        {
            g.FillRectangle(brush, -s * 0.08f, -outer, s * 0.16f, outer - body + s * 0.06f);
            g.RotateTransform(45);
        }
        g.Restore(state);
        g.FillEllipse(brush, c - body, c - body, body * 2, body * 2);
        var mode = g.CompositingMode;
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy; // 가운데 구멍은 투명하게
        using (var clear = new SolidBrush(Color.Transparent))
            g.FillEllipse(clear, c - hole, c - hole, hole * 2, hole * 2);
        g.CompositingMode = mode;
    });

    public static IntPtr ForFile(string path)
    {
        string ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext) || ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".ico", StringComparison.OrdinalIgnoreCase))
            return File;
        return FromShell("ext" + ext, "x" + ext, 0x80);
    }

    /// <summary>설정 창 트리용 폴더 아이콘 이미지.</summary>
    public static Bitmap? FolderImage(int size)
    {
        var sfi = new SHFILEINFO();
        if (SHGetFileInfo("x", 0x10, ref sfi, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES) == IntPtr.Zero
            || sfi.hIcon == IntPtr.Zero)
            return null;
        try
        {
            using var icon = Icon.FromHandle(sfi.hIcon);
            using var src = icon.ToBitmap();
            return new Bitmap(src, size, size);
        }
        finally
        {
            DestroyIcon(sfi.hIcon);
        }
    }

    public static void SetLast(IntPtr menu, IntPtr bitmap)
    {
        if (bitmap == IntPtr.Zero) return;
        int pos = GetMenuItemCount(menu) - 1;
        var info = new MENUITEMINFO { cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(), fMask = MIIM_BITMAP, hbmpItem = bitmap };
        SetMenuItemInfo(menu, (uint)pos, true, ref info);
    }

    private static IntPtr FromShell(string key, string fakePath, uint attributes)
    {
        if (Cache.TryGetValue(key, out var bmp)) return bmp;
        var sfi = new SHFILEINFO();
        IntPtr ok = SHGetFileInfo(fakePath, attributes, ref sfi, (uint)Marshal.SizeOf<SHFILEINFO>(),
            SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
        bmp = ok != IntPtr.Zero && sfi.hIcon != IntPtr.Zero ? ToBitmap(sfi.hIcon) : IntPtr.Zero;
        Cache[key] = bmp;
        return bmp;
    }

    private static IntPtr FromDrawing(string key, Action<Graphics, int> draw)
    {
        if (Cache.TryGetValue(key, out var bmp)) return bmp;
        bmp = IntPtr.Zero;
        try
        {
            int size = SystemInformation.SmallIconSize.Width;
            using var img = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(img))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                draw(g, size);
            }
            bmp = ToDib(img);
        }
        catch (Exception ex)
        {
            Log.Write("menu icon failed: " + ex.Message);
        }
        Cache[key] = bmp;
        return bmp;
    }

    private static IntPtr ToBitmap(IntPtr hIcon)
    {
        try
        {
            int size = SystemInformation.SmallIconSize.Width;
            using var icon = Icon.FromHandle(hIcon);
            using var src = icon.ToBitmap();
            using var scaled = src.Width == size ? null : new Bitmap(src, size, size);
            return ToDib(scaled ?? src);
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    /// <summary>미리 곱한 알파(PArgb) 32비트 top-down DIB. 메뉴 hbmpItem이 투명하게 그려진다.</summary>
    private static IntPtr ToDib(Bitmap img)
    {
        {
            var bmi = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = img.Width,
                biHeight = -img.Height, // top-down
                biPlanes = 1,
                biBitCount = 32,
            };
            IntPtr hbmp = CreateDIBSection(IntPtr.Zero, ref bmi, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (hbmp == IntPtr.Zero) return IntPtr.Zero;

            var data = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var row = new byte[img.Width * 4];
                for (int y = 0; y < img.Height; y++)
                {
                    Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                    Marshal.Copy(row, 0, bits + y * row.Length, row.Length);
                }
            }
            finally
            {
                img.UnlockBits(data);
            }
            return hbmp;
        }
    }

    private const uint MIIM_BITMAP = 0x80;
    private const uint SHGFI_ICON = 0x100, SHGFI_SMALLICON = 0x1, SHGFI_USEFILEATTRIBUTES = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct MENUITEMINFO
    {
        public uint cbSize, fMask, fType, fState, wID;
        public IntPtr hSubMenu, hbmpChecked, hbmpUnchecked, dwItemData, dwTypeData;
        public uint cch;
        public IntPtr hbmpItem;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll")] private static extern int GetMenuItemCount(IntPtr menu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetMenuItemInfo(IntPtr menu, uint item, bool byPosition, ref MENUITEMINFO info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO sfi, uint size, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
}
