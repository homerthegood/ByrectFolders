using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace DirectFolders;

/// <summary>테마 한 벌의 색·모양 값. Glass=true면 그라데이션·유리 패널(테두리 고리+그림자)·강조 막대를 쓴다.</summary>
internal sealed class Palette
{
    public string Name = "", Label = "";
    public bool Glass;

    public Color Canvas, CanvasTop, CanvasBottom, GlowLight, GlowMint;
    public Color Tile, TileRing, Shadow;
    public Color Inset, InsetEdge, Focus;
    public Color Text, Ink, Muted, Faint;
    public Color ButtonEdge, ButtonEdgeHover, ButtonTop, ButtonBottom;
    public Color Blue, BlueHover, BlueSoft, BlueText;
    public Color PrimaryTop, PrimaryMid, PrimaryBottom, PrimaryHoverTop, PrimaryHoverMid, PrimaryHoverBottom, PrimaryEdge;
    public Color Red, RedSoft, RedEdge, Green, Pick, Aqua, Mint, TitleBar, TitleText;
    public int TileRadius, ButtonRadius, InputRadius, TabRadius, TileShadow;
    // 보조 패널, 목록 행(선택·가리킴), 가는 구분선, 단축키 표시(키캡)
    public Color TileQuiet, RowSelTop, RowSelBottom, RowSelEdge, RowHover, Hairline, KeyFill, KeyEdge, SwitchOff;
}

/// <summary>
/// 설정 창·대화상자 테마. Aplaybox Downloader의 테마를 WinForms로 옮긴 것.
/// - Aero(기본): Frutiger Aero. 옅은 하늘색 그라데이션 바탕, 흰 유리 패널, 남색 글자, 파란 그라데이션 기본 버튼, 아쿠아→민트 강조 막대.
/// - Light: Bento. 회색 바탕 위 테두리 없는 흰 둥근 타일, 파란 강조색. (이전 기본 테마, 그대로 보존)
/// 설정의 "theme" 값으로 고른다. 새로 여는 창부터 적용된다.
/// </summary>
internal static class Theme
{
    private static Color Hex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);

    public static readonly Palette Light = new()
    {
        Name = "light", Label = "Light",
        Canvas = Hex(0xe7e7e9), CanvasTop = Hex(0xe7e7e9), CanvasBottom = Hex(0xe7e7e9),
        Tile = Color.White, TileRing = Color.White, Shadow = Color.Transparent,
        Inset = Hex(0xf6f6f7), InsetEdge = Hex(0xececee), Focus = Hex(0x1a7cff),
        Text = Hex(0x111114), Ink = Hex(0x111114), Muted = Hex(0x6e6e76), Faint = Hex(0xa3a3aa),
        ButtonEdge = Hex(0xe3e3e6), ButtonEdgeHover = Hex(0xd6d6da), ButtonTop = Color.White, ButtonBottom = Color.White,
        Blue = Hex(0x1a7cff), BlueHover = Hex(0x0f6ae6), BlueSoft = Hex(0xeaf3ff), BlueText = Hex(0x0b5bd3),
        PrimaryTop = Hex(0x1a7cff), PrimaryMid = Hex(0x1a7cff), PrimaryBottom = Hex(0x1a7cff),
        PrimaryHoverTop = Hex(0x0f6ae6), PrimaryHoverMid = Hex(0x0f6ae6), PrimaryHoverBottom = Hex(0x0f6ae6), PrimaryEdge = Hex(0x1a7cff),
        Red = Hex(0xd92d33), RedSoft = Hex(0xfff1f1), RedEdge = Hex(0xffd3d5), Green = Hex(0x0e8a43),
        Pick = Hex(0xd4e6ff), Aqua = Hex(0x1a7cff), Mint = Hex(0x1a7cff), TitleBar = Hex(0xe7e7e9), TitleText = Hex(0x111114),
        TileRadius = 16, ButtonRadius = 10, InputRadius = 10, TabRadius = 14, TileShadow = 0,
        TileQuiet = Hex(0xf4f4f6), RowSelTop = Hex(0xeaf3ff), RowSelBottom = Hex(0xeaf3ff), RowSelEdge = Hex(0xcfe3ff), RowHover = Hex(0xf5f5f7),
        Hairline = Hex(0xececee), KeyFill = Color.White, KeyEdge = Hex(0xdcdce0), SwitchOff = Hex(0xd4d4d9),
    };

    public static readonly Palette Aero = new()
    {
        Name = "aero", Label = "Aero", Glass = true,
        Canvas = Hex(0xe3f0f8), CanvasTop = Hex(0xd4e7f3), CanvasBottom = Hex(0xeef6fa),
        GlowLight = Color.FromArgb(230, 255, 255, 255), GlowMint = Color.FromArgb(166, 200, 240, 224),
        Tile = Hex(0xf9fcfe), TileRing = Hex(0xc4dfef), Shadow = Color.FromArgb(255, 60, 120, 170),
        Inset = Color.White, InsetEdge = Hex(0xd3e5f0), Focus = Hex(0x6cc6ec),
        Text = Hex(0x163f5c), Ink = Hex(0x0f3350), Muted = Hex(0x56748c), Faint = Hex(0x8aa3b6),
        ButtonEdge = Hex(0xcfe3ef), ButtonEdgeHover = Hex(0xa9d0e8), ButtonTop = Color.White, ButtonBottom = Hex(0xf0f8fd),
        Blue = Hex(0x3a9ad8), BlueHover = Hex(0x1f78b8), BlueSoft = Hex(0xe6f5fd), BlueText = Hex(0x1f78b8),
        PrimaryTop = Hex(0x7ccdf3), PrimaryMid = Hex(0x4aa8e2), PrimaryBottom = Hex(0x3a97d6),
        PrimaryHoverTop = Hex(0x8ad6f7), PrimaryHoverMid = Hex(0x56b3ea), PrimaryHoverBottom = Hex(0x409fdd), PrimaryEdge = Hex(0x4aa8e2),
        Red = Hex(0xc23b3b), RedSoft = Hex(0xfff1f1), RedEdge = Hex(0xf3c4c4), Green = Hex(0x22c55e),
        Pick = Hex(0xd6ecfa), Aqua = Hex(0x6cc6ec), Mint = Hex(0x7fd8b4), TitleBar = Hex(0xd4e7f3), TitleText = Hex(0x1f3a4d),
        TileRadius = 20, ButtonRadius = 12, InputRadius = 12, TabRadius = 16, TileShadow = 5,
        TileQuiet = Hex(0xeef6fb), RowSelTop = Hex(0xe6f5fd), RowSelBottom = Hex(0xd3ecfa), RowSelEdge = Hex(0xa9d6f0), RowHover = Hex(0xedf6fc),
        Hairline = Hex(0xd7e8f2), KeyFill = Color.White, KeyEdge = Hex(0xc4dcea), SwitchOff = Hex(0xc9dbe7),
    };

    public static readonly Palette[] All = { Aero, Light };
    public const string DefaultName = "aero";

    /// <summary>현재 테마.</summary>
    public static Palette P { get; private set; } = Aero;

    /// <summary>이름으로 테마를 고른다(모르는 이름이면 기본). 바뀌었으면 true.</summary>
    public static bool Use(string? name)
    {
        var next = All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Aero;
        if (next == P) return false;
        P = next;
        return true;
    }

    public static bool Glass => P.Glass;
    public static Color Canvas => P.Canvas;
    public static Color Tile => P.Tile;
    public static Color Inset => P.Inset;
    public static Color InsetEdge => P.InsetEdge;
    public static Color Text => P.Text;
    public static Color Ink => P.Ink;
    public static Color Muted => P.Muted;
    public static Color Faint => P.Faint;
    public static Color ButtonEdge => P.ButtonEdge;
    public static Color ButtonEdgeHover => P.ButtonEdgeHover;
    public static Color Blue => P.Blue;
    public static Color BlueHover => P.BlueHover;
    public static Color BlueSoft => P.BlueSoft;
    public static Color BlueText => P.BlueText;
    public static Color Red => P.Red;
    public static Color RedSoft => P.RedSoft;
    public static Color RedEdge => P.RedEdge;
    public static Color Green => P.Green;
    public static Color Pick => P.Pick;

    // ───────── 글꼴 ─────────

    private static readonly PrivateFontCollection Fonts = new();
    private static FontFamily? _family;

    /// <summary>내장 나눔스퀘어라운드(없으면 맑은 고딕). GDI(네이티브 컨트롤)에서도 쓰도록 메모리 글꼴로도 등록한다.</summary>
    private static FontFamily Family
    {
        get
        {
            if (_family != null) return _family;
            try
            {
                using var s = typeof(Theme).Assembly.GetManifestResourceStream("NanumSquareRoundB.ttf");
                if (s != null)
                {
                    var data = new byte[s.Length];
                    s.ReadExactly(data);
                    IntPtr mem = Marshal.AllocCoTaskMem(data.Length); // PrivateFontCollection이 쓰는 동안 해제하지 않음
                    Marshal.Copy(data, 0, mem, data.Length);
                    Fonts.AddMemoryFont(mem, data.Length);
                    uint count = 0;
                    AddFontMemResourceEx(mem, (uint)data.Length, IntPtr.Zero, ref count);
                    _family = Fonts.Families.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Log.Write("theme font load failed: " + ex.Message);
            }
            return _family ??= new FontFamily("맑은 고딕");
        }
    }

    public static Font UiFont(float size = 9.5f) => new(Family, size, FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>경로·보조 정보용 가는 글꼴(맑은 고딕 Regular). 굵고 둥근 UI 글꼴과 굵기로 대비를 준다.</summary>
    public static Font DataFont(float size = 9f) => new("맑은 고딕", size, FontStyle.Regular, GraphicsUnit.Point);

    // ───────── 공통 적용 ─────────

    /// <summary>폼 바탕·글꼴·제목 표시줄 색(Windows 11)을 맞춘다. background를 주지 않으면 캔버스(설정 창) 바탕.</summary>
    public static void Apply(Form form, Color? background = null)
    {
        form.BackColor = background ?? Canvas;
        form.ForeColor = Text;
        form.Font = UiFont();
        Color caption = background ?? P.TitleBar;
        form.HandleCreated += (_, _) => TitleBar(form.Handle, caption);
        if (form.IsHandleCreated) TitleBar(form.Handle, caption);
    }

    private static void TitleBar(IntPtr hwnd, Color bg)
    {
        int caption = ColorRef(bg), text = ColorRef(P.TitleText), border = ColorRef(bg);
        DwmSetWindowAttribute(hwnd, 35, ref caption, 4); // DWMWA_CAPTION_COLOR
        DwmSetWindowAttribute(hwnd, 36, ref text, 4);    // DWMWA_TEXT_COLOR
        DwmSetWindowAttribute(hwnd, 34, ref border, 4);  // DWMWA_BORDER_COLOR
    }

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    /// <summary>
    /// 팝업 창(메뉴) 모서리를 둥글게. Windows 11은 DWM이 매끈하게 처리하고,
    /// 지원하지 않는 Windows에서는 창 영역(Region)을 둥글게 잘라 대신한다.
    /// </summary>
    public static void RoundPopup(ToolStripDropDown menu)
    {
        void Set()
        {
            int pref = 2; // DWMWCP_ROUND
            int hr = DwmSetWindowAttribute(menu.Handle, 33, ref pref, 4); // DWMWA_WINDOW_CORNER_PREFERENCE
            if (hr == 0)
            {
                int border = ColorRef(P.TileRing);
                DwmSetWindowAttribute(menu.Handle, 34, ref border, 4);
                return;
            }
            void Clip()
            {
                using var path = Round(new RectangleF(0, 0, menu.Width, menu.Height), Scale(menu, 8));
                menu.Region = new Region(path);
            }
            Clip();
            menu.SizeChanged += (_, _) => Clip();
        }
        if (menu.IsHandleCreated) Set();
        menu.HandleCreated += (_, _) => Set();
    }

    /// <summary>트리·목록에 탐색기 스타일(밝은 선택색, 얇은 스크롤바)을 적용.</summary>
    public static void ExplorerStyle(Control c)
    {
        void Set() => SetWindowTheme(c.Handle, "Explorer", null);
        if (c.IsHandleCreated) Set();
        c.HandleCreated += (_, _) => Set();
    }

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0)
        {
            p.AddRectangle(r);
            return p;
        }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static int Scale(Control? c, int v) => (int)Math.Round(v * (c?.DeviceDpi ?? SystemDpi) / 96.0);

    private static int SystemDpi
    {
        get { try { return (int)GetDpiForSystem(); } catch { return 96; } }
    }

    /// <summary>세로 그라데이션 붓(위→아래). 가장자리 색 번짐을 막으려고 영역을 1px 넓혀 만든다.</summary>
    public static LinearGradientBrush Vertical(RectangleF r, Color top, Color bottom, Color? mid = null, float midAt = 0.5f)
    {
        var area = new RectangleF(r.X, r.Y - 1, Math.Max(1, r.Width), Math.Max(1, r.Height) + 2);
        var brush = new LinearGradientBrush(area, top, bottom, LinearGradientMode.Vertical);
        if (mid is { } m)
            brush.InterpolationColors = new ColorBlend { Colors = new[] { top, m, bottom }, Positions = new[] { 0f, midAt, 1f } };
        return brush;
    }

    // ───────── 캔버스(창 바탕) ─────────

    /// <summary>창 바탕을 rect에 그린다. 유리 테마: 하늘색 세로 그라데이션 + 오른쪽 위 흰 빛, 왼쪽 아래 민트 빛.</summary>
    public static void DrawCanvas(Graphics g, Rectangle rect, float scale)
    {
        if (!P.Glass || rect.Width <= 0 || rect.Height <= 0)
        {
            using var flat = new SolidBrush(P.Canvas);
            g.FillRectangle(flat, rect);
            return;
        }
        using (var lg = Vertical(rect, P.CanvasTop, P.CanvasBottom, P.Canvas, 0.45f))
            g.FillRectangle(lg, rect);
        Glow(g, rect.X + rect.Width * 0.85f, rect.Y - rect.Height * 0.10f, 900 * scale * 0.6f, 520 * scale * 0.6f, P.GlowLight);
        Glow(g, rect.X - rect.Width * 0.08f, rect.Y + rect.Height * 1.05f, 800 * scale * 0.6f, 460 * scale * 0.6f, P.GlowMint);
    }

    private static void Glow(Graphics g, float cx, float cy, float rx, float ry, Color color)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
        using var brush = new PathGradientBrush(path) { CenterColor = color, SurroundColors = new[] { Color.FromArgb(0, color) } };
        g.FillPath(brush, path);
    }

    /// <summary>컨트롤 c가 놓인 자리의 창 바탕을 그린다(폼 전체 그라데이션과 이어지게 위치를 맞춘다).</summary>
    public static void PaintCanvas(Graphics g, Control c)
    {
        var form = c.FindForm() ?? c;
        if (!P.Glass)
        {
            g.Clear(P.Canvas);
            return;
        }
        var origin = c == form || !c.IsHandleCreated || !form.IsHandleCreated ? Point.Empty : form.PointToClient(c.PointToScreen(Point.Empty));
        var state = g.Save();
        g.TranslateTransform(-origin.X, -origin.Y);
        DrawCanvas(g, form.ClientRectangle, form.DeviceDpi / 96f);
        g.Restore(state);
    }

    /// <summary>직접 그리는 컨트롤의 바깥(둥근 모서리 밖) 바탕: 부모가 캔버스면 캔버스를, 아니면 부모 색을 칠한다.</summary>
    public static void ClearParent(Graphics g, Control c)
    {
        var back = c.Parent?.BackColor ?? Canvas;
        if (P.Glass && (back.A < 255 || back.ToArgb() == P.Canvas.ToArgb())) PaintCanvas(g, c);
        else g.Clear(back.A < 255 ? Canvas : back);
    }

    [DllImport("gdi32.dll")] private static extern IntPtr AddFontMemResourceEx(IntPtr font, uint size, IntPtr pdv, ref uint count);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string app, string? idList);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
}

/// <summary>팝업 메뉴 색(체크 표시 등 기본 렌더러가 쓰는 색).</summary>
internal sealed class MenuColors : ProfessionalColorTable
{
    public MenuColors() { UseSystemColors = false; }
    public override Color MenuItemSelected => Theme.BlueSoft;
    public override Color MenuItemBorder => Theme.BlueSoft;
    public override Color MenuBorder => Theme.P.TileRing;
    public override Color ToolStripDropDownBackground => Theme.Tile;
    public override Color ImageMarginGradientBegin => Theme.Tile;
    public override Color ImageMarginGradientMiddle => Theme.Tile;
    public override Color ImageMarginGradientEnd => Theme.Tile;
    public override Color MenuItemSelectedGradientBegin => Theme.BlueSoft;
    public override Color MenuItemSelectedGradientEnd => Theme.BlueSoft;
    public override Color MenuItemPressedGradientBegin => Theme.BlueSoft;
    public override Color MenuItemPressedGradientEnd => Theme.BlueSoft;
    public override Color CheckBackground => Theme.BlueSoft;
    public override Color CheckSelectedBackground => Theme.BlueSoft;
    public override Color CheckPressedBackground => Theme.BlueSoft;
    public override Color SeparatorDark => Theme.InsetEdge;
    public override Color SeparatorLight => Theme.Tile;
}

/// <summary>팝업 메뉴 그리기: 테두리는 창(DWM)에 맡기고, 강조는 안쪽으로 들여 둥글게, 구분선은 얇게.</summary>
internal sealed class MenuRenderer : ToolStripProfessionalRenderer
{
    public MenuRenderer() : base(new MenuColors()) { RoundedEdges = false; }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Theme.Tile);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        if (!item.Enabled || !(item.Selected || item.Pressed)) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int m = Theme.Scale(e.ToolStrip, 4);
        using var path = Theme.Round(new RectangleF(m, 0.5f, item.Width - m * 2 - 1, item.Height - 1.5f), Theme.Scale(e.ToolStrip, 7));
        using var b = new SolidBrush(Theme.BlueSoft);
        g.FillPath(b, path);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int pad = Theme.Scale(e.ToolStrip, 10), y = e.Item.Height / 2;
        using var p = new Pen(Theme.InsetEdge);
        e.Graphics.DrawLine(p, pad, y, e.Item.Width - pad, y);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Muted;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = e.ImageRectangle;
        float s = Math.Min(r.Width, r.Height), x = r.X + (r.Width - s) / 2f, y = r.Y + (r.Height - s) / 2f;
        using var pen = new Pen(Theme.Blue, Math.Max(1.6f, s / 8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, new[] { new PointF(x + s * 0.18f, y + s * 0.52f), new PointF(x + s * 0.42f, y + s * 0.76f), new PointF(x + s * 0.84f, y + s * 0.26f) });
    }
}

/// <summary>작은 대화상자·메뉴용 공통 부품(캡션·입력칸·버튼·메뉴).</summary>
internal static class Kit
{
    public static Label Caption(Control owner, string text) =>
        new() { Text = text, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(Theme.Scale(owner, 2), Theme.Scale(owner, 12), 0, Theme.Scale(owner, 4)) };

    public static InputFrame Input(Control owner, Control inner) =>
        new(inner) { Dock = DockStyle.Fill, Height = Theme.Scale(owner, 32), Margin = new Padding(0, 0, Theme.Scale(owner, 6), 0) };

    /// <summary>테마가 적용된 팝업 메뉴(둥근 모서리). owner가 없으면(트레이 메뉴) 시스템 배율 기준.</summary>
    public static ContextMenuStrip Menu(Control? owner, bool checkMargin = false)
    {
        var menu = new ContextMenuStrip
        {
            Font = owner?.Font ?? Theme.UiFont(), ShowImageMargin = false, ShowCheckMargin = checkMargin,
            BackColor = Theme.Tile, ForeColor = Theme.Ink, Renderer = new MenuRenderer(),
            Padding = new Padding(0, Theme.Scale(owner, 5), 0, Theme.Scale(owner, 5)),
        };
        Theme.RoundPopup(menu);
        return menu;
    }

    public static ToolStripMenuItem MenuItem(Control? owner, string text, Action onClick, bool danger = false)
    {
        // "이름\t단축키" 형식이면 단축키를 오른쪽 열에 표시
        string[] parts = text.Split('\t', 2);
        var item = new ToolStripMenuItem(parts[0]) { Padding = MenuItemPadding(owner) };
        if (parts.Length > 1) item.ShortcutKeyDisplayString = parts[1];
        if (danger) item.ForeColor = Theme.Red;
        item.Click += (_, _) => onClick();
        return item;
    }

    public static Padding MenuItemPadding(Control? owner) =>
        new(Theme.Scale(owner, 12), Theme.Scale(owner, 5), Theme.Scale(owner, 16), Theme.Scale(owner, 5));

    public static ThemeButton Button(Control owner, string text, ButtonKind kind, int minWidth = 0) =>
        new()
        {
            Text = text, Kind = kind, AutoSize = true,
            MinimumSize = new Size(Theme.Scale(owner, minWidth), Theme.Scale(owner, 32)),
            Margin = new Padding(Theme.Scale(owner, 4), 0, 0, 0),
        };
}

/// <summary>
/// 둥근 타일(패널). Light: 테두리 없는 흰 타일. Aero: 유리 패널(옅은 파란 고리 + 흰 안쪽 테두리 + 아래로 번지는 그림자).
/// 그림자 자리는 타일 안쪽 여백으로 잡으므로 Padding에 ShadowPad를 더해 써야 한다.
/// </summary>
internal class TilePanel : Panel
{
    private bool _quiet;

    public TilePanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Theme.Tile;
    }

    /// <summary>보조 패널: 그림자 없이 한 단계 옅은 바탕. 주 패널(목록)보다 뒤로 물러나 보이게 한다.</summary>
    public bool Quiet
    {
        get => _quiet;
        set { _quiet = value; BackColor = Fill; Invalidate(); }
    }

    public Color Fill => _quiet ? Theme.P.TileQuiet : Theme.Tile;

    public static int ShadowPad(Control c) => Theme.Scale(c, Theme.P.TileShadow);

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.ClearParent(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int sh = ShadowPad(this);
        float radius = Theme.Scale(this, Theme.P.TileRadius);
        var rect = new RectangleF(sh, sh * 0.5f, Width - 1 - sh * 2, Height - 1 - sh * 2);
        if (!Theme.Glass)
        {
            using var path = Theme.Round(rect, radius);
            using var b = new SolidBrush(Fill);
            g.FillPath(b, path);
            return;
        }

        // 그림자: 아래로 조금 내려 여러 겹으로 옅게(보조 패널은 그림자 없음)
        for (int i = _quiet ? 0 : sh; i >= 1; i--)
        {
            var r = rect;
            r.Inflate(i * 0.8f, i * 0.8f);
            r.Offset(0, sh * 0.6f);
            using var path = Theme.Round(r, radius + i);
            using var b = new SolidBrush(Color.FromArgb(5, Theme.P.Shadow));
            g.FillPath(b, path);
        }
        using (var path = Theme.Round(rect, radius))
        {
            using var b = new SolidBrush(Fill);
            g.FillPath(b, path);
            using var ring = new Pen(Theme.P.TileRing);
            g.DrawPath(ring, path);
        }
        var inner = rect;
        inner.Inflate(-1, -1);
        using (var path = Theme.Round(inner, radius - 1))
        using (var edge = new Pen(Color.FromArgb(235, 255, 255, 255)))
            g.DrawPath(edge, path);
    }
}

/// <summary>
/// 창 바탕(캔버스) 위에 놓는 컨테이너. 자기 자리의 바탕을 직접 그린다.
/// (BackColor를 투명으로 두고 부모에게 그리게 하는 WinForms 방식은 부분 다시 그리기 때 그라데이션 위치가 어긋나는 일이 있어 쓰지 않는다.)
/// </summary>
internal class CanvasPanel : Panel
{
    public CanvasPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Canvas;
    }

    protected override void OnPaintBackground(PaintEventArgs e) => Theme.PaintCanvas(e.Graphics, this);
}

/// <summary>창 바탕 위에 놓는 표 배치 컨테이너(CanvasPanel과 같은 방식).</summary>
internal class CanvasTable : TableLayoutPanel
{
    public CanvasTable()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Canvas;
    }

    protected override void OnPaintBackground(PaintEventArgs e) => Theme.PaintCanvas(e.Graphics, this);
}

internal enum ButtonKind { Normal, Primary, Quiet, Danger }

/// <summary>버튼에 그리는 작은 선 아이콘. 글리프 문자(▲▼＋) 대신 직접 그려 굵기·크기를 글자와 맞춘다.</summary>
internal enum ButtonGlyph { None, Plus, Up, Down, CaretDown, Help }

/// <summary>둥근 버튼. Normal=흰 바탕+테두리, Primary=파란 바탕(Aero는 그라데이션), Quiet=투명, Danger=연한 빨강.</summary>
internal sealed class ThemeButton : Button
{
    private bool _hover, _down;
    public ButtonKind Kind { get; set; }

    /// <summary>글자 앞(CaretDown은 글자 뒤)에 그리는 아이콘. 글자가 없으면 아이콘만 가운데에 그린다.</summary>
    public ButtonGlyph Glyph { get; set; }

    public ThemeButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }

    private int GlyphSize => Theme.Scale(this, Glyph == ButtonGlyph.Help ? 16 : 10);
    private int GlyphGap => Theme.Scale(this, 7);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.ClearParent(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var p = Theme.P;
        bool hot = Enabled && (_hover || _down);
        Color top, bottom, edge, text;
        Color? mid = null;
        bool fill = true;
        switch (Kind)
        {
            case ButtonKind.Primary:
                top = hot ? p.PrimaryHoverTop : p.PrimaryTop;
                mid = hot ? p.PrimaryHoverMid : p.PrimaryMid;
                bottom = hot ? p.PrimaryHoverBottom : p.PrimaryBottom;
                edge = p.Glass ? p.PrimaryEdge : top;
                text = Color.White;
                break;
            case ButtonKind.Quiet:
                fill = hot;
                top = bottom = p.Glass ? Color.White : p.Inset;
                edge = p.Glass ? p.ButtonEdge : top;
                text = p.Ink;
                break;
            case ButtonKind.Danger:
                top = p.Glass ? Color.White : (hot ? Color.FromArgb(0xff, 0xe6, 0xe7) : p.RedSoft);
                bottom = hot ? Color.FromArgb(0xff, 0xe6, 0xe7) : p.RedSoft;
                edge = p.RedEdge;
                text = p.Red;
                break;
            default:
                top = _down ? p.ButtonBottom : p.ButtonTop;
                bottom = p.Glass ? p.ButtonBottom : (hot ? p.Inset : p.ButtonTop);
                if (!p.Glass) top = bottom;
                edge = hot ? p.ButtonEdgeHover : p.ButtonEdge;
                text = p.Ink;
                break;
        }
        if (!Enabled && Kind == ButtonKind.Primary)
        {
            // 누를 일이 없는 기본 버튼(저장할 변경 없음): 색을 빼고 평평하게
            top = bottom = p.Glass ? p.ButtonBottom : p.Inset;
            mid = null;
            edge = p.ButtonEdge;
            text = p.Faint;
        }

        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float radius = Theme.Scale(this, p.ButtonRadius);
        if (fill)
        {
            using var path = Theme.Round(rect, radius);
            using (var b = Theme.Vertical(rect, top, bottom, mid, 0.55f))
                g.FillPath(b, path);
            using var pen = new Pen(edge);
            g.DrawPath(pen, path);
            if (p.Glass && Kind == ButtonKind.Primary && Enabled)
            {
                // 위쪽 안쪽 하이라이트(유리 광택)
                var hi = rect;
                hi.Inflate(-1.2f, -1.2f);
                using var hiPath = Theme.Round(hi, radius - 1);
                using var hiPen = new Pen(Color.FromArgb(_down ? 40 : 110, 255, 255, 255));
                var clip = g.Clip;
                g.SetClip(new RectangleF(0, 0, Width, Height * 0.5f));
                g.DrawPath(hiPen, hiPath);
                g.Clip = clip;
            }
        }
        if (Focused && ShowFocusCues)
        {
            using var path = Theme.Round(new RectangleF(1.5f, 1.5f, Width - 3.5f, Height - 3.5f), Math.Max(1, radius - 1));
            using var pen = new Pen(Kind == ButtonKind.Primary ? Color.White : p.Focus, 1.5f);
            g.DrawPath(pen, path);
        }

        var color = Enabled || Kind == ButtonKind.Primary ? text : Color.FromArgb(110, text);
        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        if (Glyph == ButtonGlyph.None)
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, color, flags | TextFormatFlags.HorizontalCenter);
            return;
        }

        // 아이콘 + 글자를 한 덩어리로 가운데 정렬
        int gs = GlyphSize, textW = Text.Length == 0 ? 0 : TextRenderer.MeasureText(g, Text, Font, Size.Empty, flags).Width;
        int total = gs + (textW > 0 ? GlyphGap + textW : 0);
        int x = (Width - total) / 2, cy = Height / 2;
        bool after = Glyph == ButtonGlyph.CaretDown;
        int glyphX = after ? x + textW + (textW > 0 ? GlyphGap : 0) : x;
        int textX = after ? x : x + gs + GlyphGap;
        if (textW > 0)
            TextRenderer.DrawText(g, Text, Font, new Rectangle(textX, 0, textW + 2, Height), color, flags);
        DrawGlyph(g, Glyph, new RectangleF(glyphX, cy - gs / 2f, gs, gs), color, Theme.Scale(this, 100) / 100f);
    }

    internal static void DrawGlyph(Graphics g, ButtonGlyph glyph, RectangleF r, Color color, float scale)
    {
        using var pen = new Pen(color, 1.7f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, h = r.Width / 2;
        switch (glyph)
        {
            case ButtonGlyph.Plus:
                g.DrawLine(pen, cx - h, cy, cx + h, cy);
                g.DrawLine(pen, cx, cy - h, cx, cy + h);
                break;
            case ButtonGlyph.Up:
                g.DrawLines(pen, new[] { new PointF(cx - h, cy + h * 0.45f), new PointF(cx, cy - h * 0.55f), new PointF(cx + h, cy + h * 0.45f) });
                break;
            case ButtonGlyph.Down:
                g.DrawLines(pen, new[] { new PointF(cx - h, cy - h * 0.45f), new PointF(cx, cy + h * 0.55f), new PointF(cx + h, cy - h * 0.45f) });
                break;
            case ButtonGlyph.CaretDown:
                g.DrawLines(pen, new[] { new PointF(cx - h * 0.7f, cy - h * 0.3f), new PointF(cx, cy + h * 0.4f), new PointF(cx + h * 0.7f, cy - h * 0.3f) });
                break;
            case ButtonGlyph.Help:
            {
                // 원 안의 물음표: 선으로 직접 그린다(작은 크기에서 글꼴보다 또렷함)
                float rad = h - 0.5f * scale;
                using var thin = new Pen(color, 1.4f * scale);
                g.DrawEllipse(thin, cx - rad, cy - rad, rad * 2, rad * 2);
                float q = rad * 0.42f;
                g.DrawArc(pen, cx - q, cy - q * 1.55f, q * 2, q * 2, 180, 270);
                g.DrawLine(pen, cx, cy + q * 0.45f, cx, cy + q * 0.6f);
                using var dot = new SolidBrush(color);
                float d = 1.0f * scale;
                g.FillEllipse(dot, cx - d, cy + q * 1.25f - d, d * 2, d * 2);
                break;
            }
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var t = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        int glyph = Glyph == ButtonGlyph.None ? 0 : GlyphSize + (Text.Length > 0 ? GlyphGap : 0);
        return new Size(Math.Max(t.Width + glyph + Theme.Scale(this, Kind == ButtonKind.Quiet ? 14 : 22), MinimumSize.Width), Math.Max(t.Height + Theme.Scale(this, 10), MinimumSize.Height));
    }
}

/// <summary>켬/끔 스위치. CheckBox를 그대로 쓰되(Checked, CheckedChanged, 키보드 Space) 모양만 알약형 스위치로 그린다.</summary>
internal sealed class ToggleSwitch : CheckBox
{
    private bool _hover;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AutoSize = false;
        Cursor = Cursors.Hand;
        Text = "";
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Size = new Size(Theme.Scale(this, 42), Theme.Scale(this, 24));
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var p = Theme.P;
        Theme.ClearParent(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new RectangleF(1, 1, Width - 3, Height - 3);
        using (var path = Theme.Round(track, track.Height / 2))
        {
            if (Checked)
            {
                using var b = Theme.Vertical(track, _hover ? p.PrimaryHoverTop : p.PrimaryTop, _hover ? p.PrimaryHoverBottom : p.PrimaryBottom, _hover ? p.PrimaryHoverMid : p.PrimaryMid, 0.55f);
                g.FillPath(b, path);
            }
            else
            {
                using var b = new SolidBrush(p.SwitchOff);
                g.FillPath(b, path);
            }
            if (Focused && ShowFocusCues)
            {
                using var ring = new Pen(p.Focus, 2f);
                g.DrawPath(ring, path);
            }
        }
        float d = track.Height - 6;
        float x = Checked ? track.Right - d - 3 : track.X + 3;
        using var shadow = new SolidBrush(Color.FromArgb(40, 20, 60, 100));
        g.FillEllipse(shadow, x, track.Y + 4, d, d);
        g.FillEllipse(Brushes.White, x, track.Y + 3, d, d);
    }
}

/// <summary>입력칸 틀(input): 둥근 바탕 안에 테두리 없는 TextBox/ComboBox. 포커스 시 강조색 테두리.</summary>
internal sealed class InputFrame : Panel
{
    public Control Inner { get; }

    public InputFrame(Control inner)
    {
        Inner = inner;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Theme.Inset;
        inner.BackColor = Theme.Inset;
        inner.ForeColor = Theme.Text;
        if (inner is TextBox tb) tb.BorderStyle = BorderStyle.None;
        if (inner is ComboBox cb) cb.FlatStyle = FlatStyle.Flat;
        inner.Dock = DockStyle.Fill;
        Controls.Add(inner);
        inner.GotFocus += (_, _) => Invalidate();
        inner.LostFocus += (_, _) => Invalidate();
        inner.EnabledChanged += (_, _) => { Inner.BackColor = Inner.Enabled ? Theme.Inset : Theme.Tile; Invalidate(); };
        Cursor = Cursors.IBeam;
        Click += (_, _) => inner.Focus();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        int h = Theme.Scale(this, 10);
        int innerH = Inner is TextBox ? Inner.PreferredSize.Height : Inner.Height;
        int v = Math.Max(Theme.Scale(this, 3), (Height - innerH) / 2);
        Padding = Inner is ComboBox ? new Padding(Theme.Scale(this, 4), Math.Max(1, (Height - Inner.Height) / 2), Theme.Scale(this, 4), 0) : new Padding(h, v, h, 0);
        base.OnLayout(levent);
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        new(Width, Inner.PreferredSize.Height + Theme.Scale(this, 10));

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor is { A: 255 } back ? back : Theme.Tile);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool focus = Inner.ContainsFocus;
        var fill = Inner.Enabled ? Theme.Inset : Theme.Tile;
        using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.Scale(this, Theme.P.InputRadius));
        using var b = new SolidBrush(fill);
        g.FillPath(b, path);
        using var p = new Pen(focus ? Theme.P.Focus : Theme.InsetEdge, focus ? 2f : 1f);
        g.DrawPath(p, path);
    }
}

/// <summary>세그먼트 탭(.list-tabs): 알약 모양 바탕 안에 버튼, 선택된 것은 파란 바탕(Aero는 그라데이션).</summary>
internal sealed class SegmentedTabs : Control
{
    private readonly List<string> _items = new();
    private readonly List<Rectangle> _rects = new();
    private int _selected, _hover = -1;

    public event Action<int>? SelectedChanged;

    public SegmentedTabs(params string[] items)
    {
        _items.AddRange(items);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public int Selected
    {
        get => _selected;
        set
        {
            if (value == _selected || value < 0 || value >= _items.Count) return;
            _selected = value;
            Invalidate();
            SelectedChanged?.Invoke(value);
        }
    }

    private void Measure()
    {
        _rects.Clear();
        int pad = Theme.Scale(this, 4), x = pad;
        foreach (var t in _items)
        {
            int w = TextRenderer.MeasureText(t, Font).Width + Theme.Scale(this, 30);
            _rects.Add(new Rectangle(x, pad, w, Height - pad * 2));
            x += w + Theme.Scale(this, 4);
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int pad = Theme.Scale(this, 4);
        int w = _items.Sum(t => TextRenderer.MeasureText(t, Font).Width + Theme.Scale(this, 30)) + Theme.Scale(this, 4) * (_items.Count - 1) + pad * 2;
        return new Size(w, Font.Height + Theme.Scale(this, 20));
    }

    // 핸들 생성 후에야 DeviceDpi가 확정되므로 그때 내용 폭에 맞춘다
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Size = GetPreferredSize(Size.Empty); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (IsHandleCreated) Size = GetPreferredSize(Size.Empty); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); Size = GetPreferredSize(Size.Empty); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Measure();
        var g = e.Graphics;
        var p = Theme.P;
        Theme.ClearParent(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.Scale(this, p.TabRadius)))
        {
            using var back = new SolidBrush(p.Glass ? p.Tile : Color.White);
            g.FillPath(back, path);
            if (p.Glass)
            {
                using var ring = new Pen(p.TileRing);
                g.DrawPath(ring, path);
            }
        }
        float inner = Theme.Scale(this, p.TabRadius - 4);
        for (int i = 0; i < _items.Count; i++)
        {
            var r = _rects[i];
            if (i == _selected)
            {
                using var path = Theme.Round(r, inner);
                using var b = Theme.Vertical(r, p.PrimaryTop, p.PrimaryBottom, p.PrimaryMid, 0.55f);
                g.FillPath(b, path);
            }
            else if (i == _hover)
            {
                using var path = Theme.Round(r, inner);
                using var b = new SolidBrush(p.Glass ? Color.White : p.Inset);
                g.FillPath(b, path);
            }
            TextRenderer.DrawText(g, _items[i], Font, r, i == _selected ? Color.White : i == _hover ? p.Ink : p.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
        if (Focused && ShowFocusCues && _selected < _rects.Count)
        {
            using var path = Theme.Round(Rectangle.Inflate(_rects[_selected], -2, -2), Math.Max(1, inner - 2));
            using var pen = new Pen(Color.White, 1.5f);
            g.DrawPath(pen, path);
        }
    }

    private int HitTest(Point p) => _rects.FindIndex(r => r.Contains(p));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h != _hover) { _hover = h; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h >= 0) { Focus(); Selected = h; }
        base.OnMouseDown(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left) Selected = Math.Max(0, _selected - 1);
        else if (e.KeyCode == Keys.Right) Selected = Math.Min(_items.Count - 1, _selected + 1);
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}
