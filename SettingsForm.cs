namespace DirectFolders;

/// <summary>즐겨찾기·단축키 설정 창(Aplaybox Downloader Light 테마). 저장 시 Saved 이벤트로 새 설정을 넘긴다.</summary>
internal sealed class SettingsForm : Form
{
    /// <summary>저장 처리(파일 기록·적용). 성공하면 true. 창은 닫히지 않는다.</summary>
    public Func<Settings, bool>? SaveHandler { get; set; }

    private readonly Settings _settings;
    private readonly MultiSelectTree _tree = new() { HideSelection = false, AllowDrop = true, FullRowSelect = true };
    private readonly TextBox _name = new();
    private readonly TextBox _path = new();
    private readonly ShortcutBox _itemHotkey = new();
    private readonly ShortcutBox _menuHotkey = new();
    private readonly CheckBox _middleClick = new() { Text = "바탕화면·탐색기·파일 대화상자 위 휠 클릭으로 메뉴 호출", AutoSize = true };
    private readonly CheckBox _showOpen = new() { Text = "열린 탐색기 폴더를 메뉴에 표시", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Theme.Red };
    private readonly Label _count = new() { AutoSize = true, ForeColor = Theme.Blue };
    private readonly ComboBox _theme = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private bool _loadingFields;

    public SettingsForm(Settings settings)
    {
        _settings = settings;
        Text = AppInfo.Name + " 설정";
        Icon = TrayApp.LoadFolderIcon();
        StartPosition = FormStartPosition.CenterScreen;
        // 자동 배율이 적용되지 않는 환경이 있어(글꼴만 커지고 크기는 100% 기준으로 남음) 배율을 직접 계산한다
        AutoScaleMode = AutoScaleMode.None;
        Theme.Apply(this);
        ClientSize = new Size(S(980), S(660));
        MinimumSize = new Size(S(800), S(540));
        Padding = new Padding(S(18), S(14), S(18), S(14));
        DoubleBuffered = true;
        ResizeRedraw = true;
        Resize += (_, _) => { if (Theme.Glass) Invalidate(true); }; // 바탕 그라데이션은 창 크기에 맞춰 다시 그린다

        BuildLayout();
        LoadTree();
        _menuHotkey.Text = HotkeyParser.Normalize(settings.Hotkey) ?? settings.Hotkey;
        _middleClick.Checked = settings.MiddleClick;
        _showOpen.Checked = settings.ShowOpenFolders;
        _theme.SelectedIndex = Math.Max(0, Array.FindIndex(Theme.All, p => string.Equals(p.Name, settings.Theme, StringComparison.OrdinalIgnoreCase)));
        UpdateFields();
        _current = TakeSnapshot(); // 실행 취소 기준점(불러온 상태)
        _savedData = _current.Data;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Theme.Glass) Theme.PaintCanvas(e.Graphics, this);
        else base.OnPaintBackground(e);
    }

    /// <summary>처음 보여 줄 탭(0 즐겨찾기 / 1 앱별 기본 폴더 / 2 일반).</summary>
    public void ShowTab(int index) => _showPage(index);

    /// <summary>96 DPI 기준 값을 현재 모니터 배율로 변환.</summary>
    private int S(int value) => (int)Math.Round(value * DeviceDpi / 96.0);

    // ───────── 레이아웃 ─────────

    private readonly Label _pathState = new() { AutoSize = true };
    private readonly ImageList _icons = new() { ColorDepth = ColorDepth.Depth32Bit };

    private void BuildLayout()
    {
        var tabs = new SegmentedTabs("즐겨찾기", "앱별 기본 폴더", "일반") { Font = Font };
        tabs.Size = tabs.GetPreferredSize(Size.Empty);
        var header = new CanvasPanel { Dock = DockStyle.Top, Height = tabs.Height + S(14) };
        header.Controls.Add(tabs);

        var pages = new[] { Page(), Page(), Page() };
        BuildFavoritesTab(pages[0]);
        BuildAppTab(pages[1]);
        BuildGeneralTab(pages[2]);
        var content = new CanvasPanel { Dock = DockStyle.Fill };
        foreach (var p in pages) content.Controls.Add(p);
        pages[0].BringToFront();
        tabs.SelectedChanged += i =>
        {
            CurrentTab = i;
            pages[i].BringToFront();
            _status.Text = "";
        };
        _showPage = i => tabs.Selected = i;

        // 아래: 도움말(현재 탭) + 상태 + 취소/저장
        var bottom = new CanvasTable { Dock = DockStyle.Bottom, ColumnCount = 4, AutoSize = true, Padding = new Padding(0, S(12), 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var help = Btn("? 도움말", () => ShowHelp(tabs.Selected), ButtonKind.Normal, 96);
        help.Margin = new Padding(0, 0, S(12), 0);
        bottom.Controls.Add(help, 0, 0);
        _status.Anchor = AnchorStyles.Left;
        _status.BackColor = Color.Transparent; // 바탕 그라데이션 위에 글자만
        bottom.Controls.Add(_status, 1, 0);
        var cancel = Btn("닫기", Close, ButtonKind.Normal, 96);
        var save = Btn("저장", () => Save(), ButtonKind.Primary, 96);
        bottom.Controls.Add(cancel, 2, 0);
        bottom.Controls.Add(save, 3, 0);

        Controls.Add(content);
        Controls.Add(bottom);
        Controls.Add(header);
        // 기본 버튼(AcceptButton)은 지정하지 않는다: 입력 중 Enter로 저장되며 창이 닫히는 것을 막기 위함
        CancelButton = cancel;
    }

    private Action<int> _showPage = _ => { };

    /// <summary>지금 보고 있는 탭.</summary>
    public int CurrentTab { get; private set; }

    // ───────── 도움말 ─────────

    private void ShowHelp(int tab)
    {
        var (title, sections) = tab switch
        {
            1 => ("앱별 기본 폴더", AppHelp),
            2 => ("일반", GeneralHelp),
            _ => ("즐겨찾기", FavoritesHelp),
        };
        using var dlg = Native.WithSystemDpi(new HelpDialog(title, sections));
        dlg.ShowDialog(this);
    }

    private static readonly HelpDialog.Section[] FavoritesHelp =
    {
        new("추가",
            "＋ 폴더 추가: 폴더를 골라 목록에 추가합니다. 선택한 항목 바로 아래(그룹이면 그 안)에 들어갑니다.",
            "열린 탐색기에서 ▾: 지금 열려 있는 탐색기 폴더 중 하나를 골라 추가합니다.",
            "메뉴의 '여기 추가...': 탐색기·파일 대화상자·바탕화면에서 메뉴를 불러 현재 폴더를 바로 추가합니다.",
            "그룹: 메뉴에서 하위 메뉴로 표시됩니다. 여러 항목을 선택한 상태에서 누르면 선택한 항목들이 새 그룹 안으로 들어갑니다. 새 그룹은 만들자마자 목록에서 바로 이름을 입력할 수 있습니다.",
            "구분선: 메뉴에 가로줄을 넣습니다."),
        new("선택",
            "Ctrl+클릭으로 하나씩 추가·해제, Shift+클릭으로 범위 선택, Ctrl+A로 전체 선택합니다.",
            "빈 곳(항목 이름 오른쪽 여백 포함)에서 끌면 사각형에 걸친 항목이 모두 선택됩니다(러버밴드 선택). Ctrl을 누른 채 끌면 기존 선택에 더합니다."),
        new("순서·그룹 정리",
            "항목을 끌어 놓으면 놓은 항목 앞으로 이동하고, 그룹 위에 놓으면 그룹 안으로 들어갑니다. 여러 개 선택한 상태에서 끌면 함께 이동합니다.",
            "선택된(파랗게 표시된) 항목은 이름 위가 아니어도 행의 어느 곳을 잡아 끌어도 됩니다.",
            "▲▼ 버튼으로 한 칸씩 옮길 수 있습니다."),
        new("우클릭 메뉴",
            "항목 하나: 이름 바꾸기(F2), 경로 바꾸기, 단축키 지정, 그룹 풀기, 그룹으로 묶기, 그룹으로 이동, 삭제(Del).",
            "여러 개: 그룹으로 묶기, 그룹으로 이동, 경로 일괄 바꾸기, 삭제.",
            "그룹으로 묶기는 첫 항목 자리에 '새 그룹'을 만들고 바로 이름을 입력하는 상태가 됩니다.",
            "경로 일괄 바꾸기는 드라이브 문자나 상위 폴더가 바뀌었을 때 경로의 일부를 한꺼번에 바꿉니다."),
        new("선택한 항목",
            "이름: 메뉴에 표시되는 이름입니다. 목록에서 F2를 누르거나 선택된 항목의 이름을 한 번 더 클릭하면 그 자리에서 바로 바꿀 수 있습니다(Enter 확정, Esc 취소).",
            "경로: %USERPROFILE% 같은 환경 변수를 쓸 수 있으며, 실제 경로가 아래에 표시됩니다. 없는 경로는 목록에서 빨간색으로 표시됩니다.",
            "단축키: 칸을 클릭한 뒤 조합키(예: Ctrl+Alt+1)를 누르면 입력되고, Backspace나 '지우기'로 지웁니다. 누르면 메뉴 없이 해당 폴더로 바로 이동합니다."),
        new("저장",
            "변경 내용은 '저장'을 눌러야 반영됩니다. 저장해도 창은 닫히지 않으며, Enter로는 저장되지 않습니다.",
            "저장하지 않은 변경이 있는 채로 닫으면 저장할지 묻습니다.",
            "저장 시 단축키 형식과 중복(메뉴 단축키 포함)을 검사합니다."),
        new("실행 취소",
            "Ctrl+Z로 직전 변경을 되돌리고, Ctrl+Y(또는 Ctrl+Shift+Z)로 다시 적용합니다. 추가·삭제·이동·그룹·이름과 경로 수정, 앱별 규칙, 일반 탭 설정이 모두 대상입니다.",
            "단축키 입력 칸에 커서가 있을 때는 Ctrl+Z도 단축키 조합으로 입력됩니다."),
    };

    private static readonly HelpDialog.Section[] AppHelp =
    {
        new("기능",
            "특정 앱에서 열기·저장 대화상자가 뜨면 지정한 폴더로 자동으로 이동합니다. 파일 이름 칸 내용은 유지됩니다."),
        new("규칙 항목",
            "앱: 실행 파일 이름(예: 3dsmax.exe). 실행 중인 앱 목록에서 고르거나 직접 입력합니다.",
            "대화상자: 모두 / 열기 / 저장 중 어느 대화상자에 적용할지 정합니다.",
            "제목 포함(선택): 대화상자 제목에 이 글자가 있을 때만 적용합니다(예: Import, 내보내기).",
            "폴더: 이동할 폴더입니다. 폴더가 없으면 이동하지 않습니다."),
        new("적용 순서",
            "위에서부터 처음 맞는 규칙 하나만 적용됩니다. 조건이 구체적인 규칙(제목 포함 지정 등)을 위에 두십시오.",
            "같은 대화상자에서는 한 번만 이동하므로, 대화상자가 뜬 뒤 직접 옮긴 폴더는 되돌리지 않습니다."),
        new("편집",
            "＋ 추가로 새 규칙, 편집 또는 더블클릭으로 수정, ▲▼로 순서 변경, 삭제로 제거합니다.",
            "변경 내용은 '저장'을 눌러야 반영됩니다."),
    };

    private static readonly HelpDialog.Section[] GeneralHelp =
    {
        new("메뉴 호출",
            "메뉴 호출 단축키: 어느 창에서든 누르면 커서 위치에 메뉴가 뜹니다. 칸을 클릭한 뒤 조합키(Ctrl·Alt·Shift·Win + 키)를 누르면 입력됩니다.",
            "휠 클릭: 바탕화면·탐색기의 빈 곳, 파일 열기·저장 대화상자 위에서 휠 클릭하면 메뉴가 뜹니다. 파일·폴더·탭 위 휠 클릭은 원래 동작(새 탭으로 열기 등)이 그대로 실행됩니다."),
        new("메뉴 구성",
            "폴더 ▶: 열린 탐색기 폴더와 최근에 이동한 폴더.",
            "파일 ▶: Windows 최근 파일. 대화상자에서 고르면 그 폴더로 이동하고 파일 이름을 채웁니다.",
            "즐겨찾기 / 여기 추가... / 설정...",
            "설정 파일 열기, 설정 다시 읽기, 최근 폴더 기록 지우기는 트레이 아이콘 우클릭 메뉴에 있습니다.",
            "'열린 탐색기 폴더를 메뉴에 표시'를 끄면 폴더 ▶ 에서 열린 탐색기 폴더가 빠집니다."),
        new("폴더를 고르면",
            "파일 대화상자: 대화상자를 그 폴더로 이동합니다.",
            "탐색기: 현재 창의 활성 탭에서 바로 이동합니다.",
            "그 외: 새 탐색기 창으로 엽니다."),
        new("화면",
            "테마: Aero(하늘색 유리 느낌)와 Light(회색 바탕의 흰 타일) 중에서 고릅니다. 저장하면 설정 창이 새 테마로 다시 열립니다."),
        new("가져오기",
            "Direct Folders에서 가져오기: Code Sector Direct Folders의 즐겨찾기와 최근 폴더를 가져옵니다. 이미 있는 경로는 건너뛰고 즐겨찾기 목록 끝에 추가되며, '저장'을 눌러야 반영됩니다."),
        new("설정 파일",
            "%APPDATA%\\ByrectFolders\\favorites.json 에 저장됩니다. 직접 편집한 뒤 트레이 아이콘 우클릭 > 설정 다시 읽기로 반영할 수 있습니다."),
    };

    private Panel Page() => new CanvasPanel { Dock = DockStyle.Fill };

    /// <summary>제목이 있는 흰 타일.</summary>
    private TilePanel Tile(string? title, out Panel body, Control? titleExtra = null)
    {
        int sh = TilePanel.ShadowPad(this); // 유리 테마: 그림자 자리만큼 안쪽 여백을 더한다
        var tile = new TilePanel { Dock = DockStyle.Fill, Padding = new Padding(S(18) + sh, S(14) + sh, S(18) + sh, S(16) + sh * 2), Margin = Padding.Empty };
        body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Tile };
        tile.Controls.Add(body);
        if (title != null)
        {
            var head = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, BackColor = Theme.Tile, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, S(8)) };
            var titleFont = Theme.UiFont(12.5f);
            if (Theme.Glass) // 제목 앞 강조 막대
                head.Controls.Add(new AccentBar { Size = new Size(S(22), S(4)), Margin = new Padding(0, (titleFont.Height - S(4)) / 2 + S(1), S(9), 0) });
            head.Controls.Add(new Label { Text = title, AutoSize = true, Font = titleFont, ForeColor = Theme.Ink, Margin = new Padding(0, 0, S(6), 0) });
            if (titleExtra != null)
            {
                titleExtra.Margin = new Padding(0, S(5), 0, 0);
                head.Controls.Add(titleExtra);
            }
            tile.Controls.Add(head);
        }
        return tile;
    }

    private void BuildFavoritesTab(Panel page)
    {
        var grid = new CanvasTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 왼쪽 타일: 도구 모음 + 목록
        var left = Tile("즐겨찾기", out var leftBody, _count);
        left.Margin = new Padding(0, 0, S(7), 0);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Theme.Tile, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, S(8)) };
        tools.Controls.Add(Btn("＋ 폴더 추가", AddFolder, ButtonKind.Normal));
        var fromExplorer = Btn("열린 탐색기에서 ▾", () => { }, ButtonKind.Normal);
        fromExplorer.Click += (_, _) => ShowExplorerMenu(fromExplorer);
        tools.Controls.Add(fromExplorer);
        tools.Controls.Add(Btn("그룹", AddGroup, ButtonKind.Quiet));
        tools.Controls.Add(Btn("구분선", AddSeparator, ButtonKind.Quiet));
        tools.Controls.Add(Arrow("▲", () => MoveSelected(-1)));
        tools.Controls.Add(Arrow("▼", () => MoveSelected(1)));

        int icon = S(16);
        _icons.ImageSize = new Size(icon, icon);
        _icons.Images.Add(MenuIcons.FolderImage(icon) ?? new Bitmap(icon, icon));   // 0: 폴더
        _icons.Images.Add(new Bitmap(icon, icon));                                     // 1: 없음(구분선)
        _tree.ImageList = _icons;
        _tree.Dock = DockStyle.Fill;
        _tree.ItemHeight = S(28);
        _tree.ShowLines = false;
        _tree.ShowRootLines = true;
        _tree.BorderStyle = BorderStyle.None;
        _tree.BackColor = Theme.Tile;
        _tree.ForeColor = Theme.Text;
        Theme.ExplorerStyle(_tree);
        leftBody.Controls.Add(_tree);
        leftBody.Controls.Add(tools);

        // 오른쪽 타일: 선택한 항목
        var right = Tile("선택한 항목", out var rightBody);
        right.Margin = new Padding(S(7), 0, 0, 0);
        var form = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, BackColor = Theme.Tile };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        form.Controls.Add(Caption("이름"), 0, 0);
        var nameBox = Input(_name);
        form.Controls.Add(nameBox, 0, 1);
        form.SetColumnSpan(nameBox, 2);

        form.Controls.Add(Caption("경로"), 0, 2);
        form.Controls.Add(Input(_path), 0, 3);
        form.Controls.Add(Btn("찾아보기", BrowsePath, ButtonKind.Normal), 1, 3);
        _pathState.Margin = new Padding(S(2), S(4), 0, 0);
        form.Controls.Add(_pathState, 0, 4);
        form.SetColumnSpan(_pathState, 2);

        form.Controls.Add(Caption("단축키"), 0, 5);
        form.Controls.Add(Input(_itemHotkey), 0, 6);
        form.Controls.Add(Btn("지우기", () => _itemHotkey.Text = "", ButtonKind.Quiet), 1, 6);
        var delete = Btn("이 항목 삭제", DeleteSelected, ButtonKind.Danger);
        delete.Margin = new Padding(0, S(18), 0, 0);
        form.Controls.Add(delete, 0, 7);
        rightBody.Controls.Add(form);

        grid.Controls.Add(left, 0, 0);
        grid.Controls.Add(right, 1, 0);
        page.Controls.Add(grid);

        _tree.AfterSelect += (_, _) => { UpdateFields(); SelectionMoved(); };
        _name.TextChanged += (_, _) => FieldChanged("name");
        _path.TextChanged += (_, _) => FieldChanged("path");
        _itemHotkey.TextChanged += (_, _) => FieldChanged("hotkey");
        foreach (var box in new Control[] { _name, _path, _itemHotkey }) box.Leave += (_, _) => _undoKey = null; // 칸을 떠나면 입력 묶음 끝
        foreach (var box in new[] { _name, _path })
            box.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.Handled = e.SuppressKeyPress = true; // 한 줄 입력 칸의 Enter 경고음 방지
                _undoKey = null;                        // Enter는 입력 확정: 이후 입력은 새 실행 취소 단계
            };
        _tree.ItemDrag += (_, e) =>
        {
            var item = (TreeNode)e.Item!;
            var picked = _tree.Picked;
            var nodes = picked.Count > 1 && picked.Contains(item) ? TopNodes(picked) : new List<TreeNode> { item };
            DoDragDrop(new DataObject(DragFormat, nodes.ToArray()), DragDropEffects.Move);
        };
        _tree.DragOver += TreeDragOver;
        _tree.DragDrop += TreeDragDrop;
        _tree.MenuRequested += (pt, _) => ShowTreeMenu(pt);
        HookLabelEdit();
        _tree.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) { DeleteNodes(_tree.Picked); e.Handled = true; }
            else if (e.KeyCode == Keys.F2) { RenameSelected(); e.Handled = true; }
        };
    }

    private void BuildGeneralTab(Panel page)
    {
        var stack = new CanvasTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var menuTile = Tile("메뉴", out var menuBody);
        menuTile.Dock = DockStyle.Top;
        menuTile.AutoSize = true;
        menuTile.Margin = new Padding(0, 0, 0, S(14));
        menuBody.Dock = DockStyle.Top;
        menuBody.AutoSize = true;
        var g = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, BackColor = Theme.Tile };
        g.Controls.Add(Caption("메뉴 호출 단축키"));
        var hk = Input(_menuHotkey);
        hk.Width = S(260);
        hk.Dock = DockStyle.None;
        g.Controls.Add(hk);
        _menuHotkey.TextChanged += (_, _) => Commit("menuHotkey");
        foreach (var cb in new[] { _middleClick, _showOpen })
        {
            cb.CheckedChanged += (_, _) => Commit();
            cb.BackColor = Theme.Tile;
            cb.ForeColor = Theme.Text;
            cb.Margin = new Padding(0, S(10), 0, 0);
            g.Controls.Add(cb);
        }
        menuBody.Controls.Add(g);

        var lookTile = Tile("화면", out var lookBody);
        lookTile.Dock = DockStyle.Top;
        lookTile.AutoSize = true;
        lookTile.Margin = new Padding(0, 0, 0, S(14));
        lookBody.Dock = DockStyle.Top;
        lookBody.AutoSize = true;
        var look = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, BackColor = Theme.Tile };
        look.Controls.Add(Caption("테마"));
        foreach (var p in Theme.All) _theme.Items.Add(p.Label);
        var themeBox = Input(_theme);
        themeBox.Width = S(260);
        themeBox.Dock = DockStyle.None;
        look.Controls.Add(themeBox);
        _theme.SelectedIndexChanged += (_, _) => Commit();
        lookBody.Controls.Add(look);

        var importTile = Tile("가져오기", out var importBody);
        importTile.Dock = DockStyle.Top;
        importTile.AutoSize = true;
        importBody.Dock = DockStyle.Top;
        importBody.AutoSize = true;
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, BackColor = Theme.Tile };
        row.Controls.Add(Btn("Direct Folders에서 가져오기", ImportDirectFolders, ButtonKind.Normal));
        importBody.Controls.Add(row);

        stack.Controls.Add(menuTile, 0, 0);
        stack.Controls.Add(lookTile, 0, 1);
        stack.Controls.Add(importTile, 0, 2);
        page.Controls.Add(stack);
    }

    /// <summary>입력칸 위의 작은 회색 이름표.</summary>
    private Label Caption(string text) =>
        new() { Text = text, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(S(2), S(10), 0, S(4)) };

    private InputFrame Input(Control inner) =>
        new(inner) { Dock = DockStyle.Fill, Height = S(32), Margin = new Padding(0, 0, S(6), 0) };

    private void ShowExplorerMenu(Control anchor)
    {
        var menu = Kit.Menu(this);
        var folders = ExplorerShell.GetOpenFolders();
        if (folders.Count == 0)
            menu.Items.Add(new ToolStripMenuItem("(열린 탐색기 폴더 없음)") { Enabled = false });
        foreach (var f in folders)
        {
            string folder = f;
            menu.Items.Add(Kit.MenuItem(this, folder, () =>
            {
                string name = Path.GetFileName(folder.TrimEnd('\\'));
                Insert(new FavoriteItem { Name = name.Length > 0 ? name : folder, Path = folder });
            }));
        }
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(anchor, new Point(0, anchor.Height + S(4)));
    }

    // ───────── 앱별 기본 폴더 탭 ─────────

    private readonly ListView _rules = new()
    {
        View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None, HeaderStyle = ColumnHeaderStyle.Nonclickable,
    };

    private static void FitLastColumn(ListView list)
    {
        if (list.Columns.Count == 0) return;
        int others = 0;
        for (int i = 0; i < list.Columns.Count - 1; i++) others += list.Columns[i].Width;
        int w = list.ClientSize.Width - others;
        if (w > 40) list.Columns[^1].Width = w;
    }

    private void BuildAppTab(Panel page)
    {
        var tile = Tile("앱별 기본 폴더", out var body);
        var note = new Label
        {
            Text = "위에서부터 처음 맞는 규칙 하나만 적용되며, 대화상자마다 한 번만 이동합니다.",
            AutoSize = true, ForeColor = Theme.Muted, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, S(10)),
        };
        _rules.BackColor = Theme.Tile;
        _rules.ForeColor = Theme.Text;
        Theme.ExplorerStyle(_rules);
        _rules.Columns.Add("앱", S(160));
        _rules.Columns.Add("대화상자", S(90));
        _rules.Columns.Add("제목 포함", S(130));
        _rules.Columns.Add("폴더", S(460));
        _rules.ClientSizeChanged += (_, _) => FitLastColumn(_rules);
        foreach (var rule in _settings.AppFolders) _rules.Items.Add(MakeRuleItem(rule));
        _rules.DoubleClick += (_, _) => EditRule();

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, BackColor = Theme.Tile, Padding = new Padding(0, S(10), 0, 0) };
        buttons.Controls.Add(Btn("＋ 추가", AddRule, ButtonKind.Normal));
        buttons.Controls.Add(Btn("편집", EditRule, ButtonKind.Normal));
        buttons.Controls.Add(Arrow("▲", () => MoveRule(-1)));
        buttons.Controls.Add(Arrow("▼", () => MoveRule(1)));
        buttons.Controls.Add(Btn("삭제", () => { if (_rules.SelectedItems.Count > 0) { _rules.Items.Remove(_rules.SelectedItems[0]); Commit(); } }, ButtonKind.Danger));

        body.Controls.Add(_rules);
        body.Controls.Add(note);
        body.Controls.Add(buttons);
        page.Controls.Add(tile);
    }

    private static string DialogLabel(AppFolderRule r) => r.DialogKind switch { "open" => "열기", "save" => "저장", _ => "모두" };

    private static ListViewItem MakeRuleItem(AppFolderRule rule)
    {
        var item = new ListViewItem(new[] { rule.App, DialogLabel(rule), rule.Title ?? "", rule.Path }) { Tag = rule };
        if (!PathCheck.FolderAvailable(rule.ExpandedPath)) item.ForeColor = Theme.Red;
        return item;
    }

    private void AddRule()
    {
        using var dlg = Native.WithSystemDpi(new RuleDialog(new AppFolderRule()));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var item = MakeRuleItem(dlg.Rule);
        _rules.Items.Add(item);
        item.Selected = true;
        Commit();
    }

    private void EditRule()
    {
        if (_rules.SelectedItems.Count == 0) return;
        var old = _rules.SelectedItems[0];
        using var dlg = Native.WithSystemDpi(new RuleDialog((AppFolderRule)old.Tag!));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        int index = old.Index;
        _rules.Items.RemoveAt(index);
        var item = MakeRuleItem(dlg.Rule);
        _rules.Items.Insert(index, item);
        item.Selected = true;
        Commit();
    }

    private void MoveRule(int delta)
    {
        if (_rules.SelectedItems.Count == 0) return;
        var item = _rules.SelectedItems[0];
        int index = item.Index + delta;
        if (index < 0 || index >= _rules.Items.Count) return;
        _rules.Items.Remove(item);
        _rules.Items.Insert(index, item);
        item.Selected = true;
        Commit();
    }

    /// <summary>순서 이동용 좁은 버튼.</summary>
    private ThemeButton Arrow(string text, Action onClick)
    {
        var b = Btn(text, onClick, ButtonKind.Quiet);
        b.AutoSize = false;
        b.Size = new Size(S(32), S(30));
        return b;
    }

    private ThemeButton Btn(string text, Action onClick, ButtonKind kind, int minWidth = 0)
    {
        var b = new ThemeButton
        {
            Text = text,
            Kind = kind,
            Font = Font,
            AutoSize = true,
            MinimumSize = new Size(S(minWidth), S(30)),
            Margin = new Padding(0, 0, S(6), S(4)),
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    // ───────── 트리 ↔ 데이터 ─────────

    private void LoadTree()
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var item in _settings.Items) _tree.Nodes.Add(MakeNode(item));
        _tree.ExpandAll();
        _tree.EndUpdate();
        if (_tree.Nodes.Count > 0) _tree.SelectedNode = _tree.Nodes[0];
    }

    private TreeNode MakeNode(FavoriteItem item)
    {
        var node = new TreeNode { Tag = item };
        RefreshNodeText(node);
        if (item.Items != null)
            foreach (var child in item.Items) node.Nodes.Add(MakeNode(child));
        return node;
    }


    private void RefreshNodeText(TreeNode node)
    {
        var item = (FavoriteItem)node.Tag!;
        node.ForeColor = Theme.Text;
        if (item.Separator)
        {
            node.Text = "──────────────";
            node.ForeColor = Theme.Faint;
            node.ImageIndex = node.SelectedImageIndex = 1;
        }
        else if (item.IsGroup)
        {
            // 굵은 NodeFont는 TreeView가 글자 폭을 기본 글꼴 기준으로 잡아 잘리므로 색과 개수로 구분
            node.Text = $"{item.Name ?? "그룹"}    ({item.Items?.Count ?? 0}개)";
            node.ForeColor = Theme.BlueText;
            node.ImageIndex = node.SelectedImageIndex = 0;
        }
        else
        {
            string text = item.Name ?? item.Path ?? "(이름 없음)";
            if (!string.IsNullOrWhiteSpace(item.Hotkey)) text += $"    {HotkeyParser.Normalize(item.Hotkey) ?? item.Hotkey}";
            if (!PathCheck.FolderAvailable(item.ExpandedPath))
            {
                text += "    (경로 없음)";
                node.ForeColor = Theme.Red;
            }
            node.Text = text;
            node.ImageIndex = node.SelectedImageIndex = 0;
        }
    }

    /// <summary>경로 칸 아래 표시: 문제가 있을 때와 환경변수가 풀린 실제 경로만 보여 준다.</summary>
    private void UpdatePathState(FavoriteItem? item)
    {
        string? p = item is { Separator: false, IsGroup: false } ? item.ExpandedPath : null;
        _pathState.ForeColor = Theme.Muted;
        if (item == null || item.Separator || item.IsGroup) _pathState.Text = "";
        else if (string.IsNullOrWhiteSpace(p)) { _pathState.Text = "경로가 비어 있습니다."; _pathState.ForeColor = Theme.Red; }
        else if (!PathCheck.FolderAvailable(p)) { _pathState.Text = "폴더를 찾을 수 없습니다."; _pathState.ForeColor = Theme.Red; }
        else _pathState.Text = p != item.Path ? p : "";
        _pathState.Visible = _pathState.Text.Length > 0;
        _count.Text = FavoriteItem.Flatten(Collect(_tree.Nodes)).Count(i => !i.IsGroup && !i.Separator).ToString();
    }

    private static List<FavoriteItem> Collect(TreeNodeCollection nodes)
    {
        var list = new List<FavoriteItem>();
        foreach (TreeNode node in nodes)
        {
            var item = (FavoriteItem)node.Tag!;
            if (item.IsGroup) item.Items = Collect(node.Nodes);
            list.Add(item);
        }
        return list;
    }

    // ───────── 선택 항목 편집 ─────────

    private FavoriteItem? Selected => _tree.SelectedNode?.Tag as FavoriteItem;

    private void UpdateFields()
    {
        _loadingFields = true;
        var item = Selected;
        bool folder = item != null && !item.Separator && !item.IsGroup;
        _name.Enabled = item != null && !item.Separator;
        _path.Enabled = folder;
        _itemHotkey.Enabled = folder;
        _name.Text = item?.Name ?? "";
        _path.Text = folder ? item!.Path ?? "" : "";
        _itemHotkey.Text = folder ? (HotkeyParser.Normalize(item!.Hotkey) ?? item!.Hotkey ?? "") : "";
        UpdatePathState(item);
        _loadingFields = false;
    }

    private void FieldChanged(string field)
    {
        if (_loadingFields || Selected is not { } item || _tree.SelectedNode == null) return;
        if (!item.Separator) item.Name = _name.Text;
        if (!item.Separator && !item.IsGroup)
        {
            item.Path = _path.Text;
            item.Hotkey = string.IsNullOrWhiteSpace(_itemHotkey.Text) ? null : _itemHotkey.Text;
        }
        RefreshNodeText(_tree.SelectedNode);
        UpdatePathState(item);
        // 같은 항목·같은 칸을 이어서 입력하면 실행 취소 한 번에 묶는다
        Commit($"{field}:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item)}");
    }

    private void BrowsePath()
    {
        using var dlg = new FolderBrowserDialog { UseDescriptionForTitle = true, Description = "폴더 선택" };
        var current = Selected?.ExpandedPath;
        if (current != null && Directory.Exists(current)) dlg.InitialDirectory = current;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _path.Text = dlg.SelectedPath;
        if (string.IsNullOrWhiteSpace(_name.Text)) _name.Text = Path.GetFileName(dlg.SelectedPath.TrimEnd('\\')) is { Length: > 0 } n ? n : dlg.SelectedPath;
    }

    // ───────── 추가·삭제·이동 ─────────

    private void Insert(FavoriteItem item)
    {
        var node = MakeNode(item);
        var sel = _tree.SelectedNode;
        if (sel == null) _tree.Nodes.Add(node);
        else if (((FavoriteItem)sel.Tag!).IsGroup) { sel.Nodes.Add(node); sel.Expand(); }
        else
        {
            var siblings = sel.Parent?.Nodes ?? _tree.Nodes;
            siblings.Insert(sel.Index + 1, node);
        }
        _tree.SelectedNode = node;
        RefreshGroups();
        Commit();
    }

    private void AddFolder()
    {
        using var dlg = new FolderBrowserDialog { UseDescriptionForTitle = true, Description = "즐겨찾기에 추가할 폴더" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        string name = Path.GetFileName(dlg.SelectedPath.TrimEnd('\\'));
        Insert(new FavoriteItem { Name = name.Length > 0 ? name : dlg.SelectedPath, Path = dlg.SelectedPath });
    }

    /// <summary>Direct Folders의 menu.json에서 즐겨찾기를 목록 끝에 추가(이미 있는 경로 제외). 저장해야 반영된다.</summary>
    private void ImportDirectFolders()
    {
        string? file = DirectFoldersImport.FindMenuFile();
        if (file == null)
        {
            using var pick = new OpenFileDialog
            {
                Title = "Direct Folders 설정 파일(menu.json) 선택",
                Filter = "menu.json|menu.json|JSON 파일 (*.json)|*.json",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            };
            if (pick.ShowDialog(this) != DialogResult.OK) return;
            file = pick.FileName;
        }

        DirectFoldersImport.Result found;
        try
        {
            found = DirectFoldersImport.Load(file);
        }
        catch (Exception ex)
        {
            _status.ForeColor = Theme.Red;
            _status.Text = "가져오기 실패: " + ex.Message;
            return;
        }

        var newItems = DirectFoldersImport.NewItems(Collect(_tree.Nodes), found.Items);
        int newCount = FavoriteItem.Flatten(newItems).Count(i => !i.IsGroup && !i.Separator);
        var newRecent = found.RecentFolders.Where(r => !FolderHistory.Items.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        if (newCount == 0 && newRecent.Count == 0)
        {
            _status.ForeColor = Theme.Muted;
            _status.Text = $"가져올 새 항목이 없습니다(즐겨찾기 {found.FolderCount}개 모두 등록됨).";
            return;
        }

        string msg = $"{file}\n\n즐겨찾기 {found.FolderCount}개 중 새 항목 {newCount}개, 최근 폴더 {newRecent.Count}개를 가져옵니다.\n"
                   + "즐겨찾기는 목록 끝에 추가되며 '저장'을 눌러야 반영됩니다.";
        if (MessageBox.Show(this, msg, "Direct Folders에서 가져오기", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;

        _tree.BeginUpdate();
        if (newItems.Count > 0 && _tree.Nodes.Count > 0 && !((FavoriteItem)_tree.Nodes[^1].Tag!).Separator)
            _tree.Nodes.Add(MakeNode(new FavoriteItem { Separator = true }));
        TreeNode? first = null;
        foreach (var item in newItems)
        {
            var node = MakeNode(item);
            _tree.Nodes.Add(node);
            first ??= node;
        }
        _tree.ExpandAll();
        _tree.EndUpdate();
        if (first != null)
        {
            _tree.SelectedNode = first;
            first.EnsureVisible();
        }
        Commit();
        FolderHistory.Merge(newRecent); // 최근 폴더는 설정 파일과 별도라 바로 반영
        _status.ForeColor = Theme.Muted;
        _status.Text = $"즐겨찾기 {newCount}개를 추가했습니다(저장 필요). 최근 폴더 {newRecent.Count}개 반영.";
    }

    /// <summary>그룹 버튼: 여러 개 선택돼 있으면 그것들을 새 그룹으로 묶고, 아니면 빈 그룹을 추가한다.</summary>
    private void AddGroup()
    {
        var picked = TopNodes(_tree.Picked);
        if (picked.Count > 1)
        {
            GroupNodes(picked); // 끝나면 이름 편집 상태가 된다
            return;
        }
        Insert(new FavoriteItem { Name = "새 그룹", Items = new List<FavoriteItem>() });
        RenameSelected(); // 새 그룹은 바로 이름을 바꿀 수 있게
    }

    private void AddSeparator() => Insert(new FavoriteItem { Separator = true });

    private void DeleteSelected() => DeleteNodes(_tree.Picked);

    /// <summary>선택 항목 삭제. 여러 개이거나 내용이 있는 그룹이면 확인한다.</summary>
    private void DeleteNodes(IEnumerable<TreeNode> picked)
    {
        var nodes = TopNodes(picked);
        if (nodes.Count == 0) return;
        bool groupWithItems = nodes.Any(n => ((FavoriteItem)n.Tag!).IsGroup && n.Nodes.Count > 0);
        string? ask = nodes.Count > 1 ? $"{nodes.Count}개 항목을 삭제합니다." + (groupWithItems ? "\n그룹 안의 항목도 함께 삭제됩니다." : "")
                    : groupWithItems ? "그룹 안의 항목도 함께 삭제됩니다." : null;
        if (ask != null && MessageBox.Show(this, ask, "삭제", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            return;

        var gone = new HashSet<TreeNode>(nodes);
        bool Alive(TreeNode? n) { for (var p = n; p != null; p = p.Parent) if (gone.Contains(p)) return false; return n != null; }
        var last = nodes[^1];
        TreeNode? next = null;
        for (var n = last.NextNode; n != null && next == null; n = n.NextNode) if (Alive(n)) next = n;
        for (var n = nodes[0].PrevNode; n != null && next == null; n = n.PrevNode) if (Alive(n)) next = n;
        next ??= Alive(nodes[0].Parent) ? nodes[0].Parent : null;

        _tree.BeginUpdate();
        foreach (var n in nodes) n.Remove();
        RefreshGroups();
        _tree.EndUpdate();
        _tree.SetPicked(next != null ? new[] { next } : Array.Empty<TreeNode>(), next);
        UpdateFields();
        Commit();
    }

    /// <summary>다른 선택 항목의 하위에 있는 것은 뺀 목록(트리 순서).</summary>
    private static List<TreeNode> TopNodes(IEnumerable<TreeNode> nodes)
    {
        var set = nodes.ToHashSet();
        return nodes.Where(n => { for (var p = n.Parent; p != null; p = p.Parent) if (set.Contains(p)) return false; return true; }).ToList();
    }

    /// <summary>트리 구조가 바뀐 뒤 그룹 데이터와 "(N개)" 표시를 맞춘다.</summary>
    private void RefreshGroups()
    {
        foreach (var node in _tree.AllNodes().Where(n => ((FavoriteItem)n.Tag!).IsGroup).Reverse())
        {
            ((FavoriteItem)node.Tag!).Items = node.Nodes.Cast<TreeNode>().Select(c => (FavoriteItem)c.Tag!).ToList();
            RefreshNodeText(node);
        }
        UpdatePathState(Selected);
    }

    // ───────── 우클릭 메뉴 ─────────

    private void ShowTreeMenu(Point at)
    {
        var picked = TopNodes(_tree.Picked);
        var menu = Kit.Menu(this);
        ToolStripMenuItem Item(string text, Action a, bool danger = false) => Kit.MenuItem(this, text, a, danger);

        if (picked.Count == 0)
        {
            menu.Items.Add(Item("폴더 추가...", AddFolder));
            menu.Items.Add(Item("그룹 추가", AddGroup));
            menu.Items.Add(Item("구분선 추가", AddSeparator));
        }
        else if (picked.Count == 1)
        {
            var node = picked[0];
            var item = (FavoriteItem)node.Tag!;
            if (!item.Separator) menu.Items.Add(Item("이름 바꾸기\tF2", RenameSelected));
            if (!item.Separator && !item.IsGroup)
            {
                menu.Items.Add(Item("경로 바꾸기...", BrowsePath));
                menu.Items.Add(Item("단축키 지정", () => { _itemHotkey.Focus(); }));
            }
            if (item.IsGroup && node.Nodes.Count > 0) menu.Items.Add(Item("그룹 풀기", () => Ungroup(node)));
            menu.Items.Add(Item("그룹으로 묶기", () => GroupNodes(picked)));
            AddMoveToGroup(menu, picked);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Item("삭제\tDel", () => DeleteNodes(picked), danger: true));
        }
        else
        {
            menu.Items.Add(Item($"그룹으로 묶기 ({picked.Count}개)", () => GroupNodes(picked)));
            AddMoveToGroup(menu, picked);
            if (FoldersIn(picked).Any()) menu.Items.Add(Item("경로 일괄 바꾸기...", () => ReplacePaths(picked)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Item($"삭제 ({picked.Count}개)\tDel", () => DeleteNodes(picked), danger: true));
        }
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(_tree, at);
    }

    /// <summary>"그룹으로 이동 ▶": 기존 그룹 목록(선택 항목 자신과 그 하위 제외) + 최상위.</summary>
    private void AddMoveToGroup(ContextMenuStrip menu, List<TreeNode> nodes)
    {
        var groups = _tree.AllNodes()
            .Where(g => ((FavoriteItem)g.Tag!).IsGroup && !nodes.Any(n => n == g || IsDescendant(g, n)) && !nodes.All(n => n.Parent == g))
            .ToList();
        bool anyNested = nodes.Any(n => n.Parent != null);
        if (groups.Count == 0 && !anyNested) return;
        var sub = new ToolStripMenuItem("그룹으로 이동") { Padding = menu.Items.Count > 0 ? menu.Items[0].Padding : Padding.Empty };
        sub.DropDown = Kit.Menu(this);
        if (anyNested) sub.DropDownItems.Add(Kit.MenuItem(this, "(최상위)", () => MoveNodes(nodes, null)));
        foreach (var g in groups)
        {
            var target = g;
            string label = new string(' ', Depth(g) * 4) + (((FavoriteItem)g.Tag!).Name ?? "그룹");
            sub.DropDownItems.Add(Kit.MenuItem(this, label, () => MoveNodes(nodes, target)));
        }
        menu.Items.Add(sub);
    }

    private static int Depth(TreeNode n) { int d = 0; for (var p = n.Parent; p != null; p = p.Parent) d++; return d; }

    private static IEnumerable<FavoriteItem> FoldersIn(IEnumerable<TreeNode> nodes) =>
        FavoriteItem.Flatten(nodes.Select(n => (FavoriteItem)n.Tag!).ToList()).Where(i => !i.IsGroup && !i.Separator);

    /// <summary>선택한 항목의 이름을 목록에서 바로 편집한다(탐색기의 F2와 같은 방식).</summary>
    private void RenameSelected()
    {
        if (Selected is not { Separator: false } || _tree.SelectedNode is not { } node) return;
        _tree.Focus();
        node.EnsureVisible();
        node.BeginEdit();
    }

    private bool _labelEditing;

    /// <summary>
    /// 목록 내 이름 편집. 항목 글자에는 단축키·"(N개)" 같은 덧붙임이 있으므로
    /// 편집 칸에는 이름만 넣고, 끝나면 이름만 저장한 뒤 표시 글자를 다시 만든다.
    /// </summary>
    private void HookLabelEdit()
    {
        _tree.LabelEdit = true;
        _tree.BeforeLabelEdit += (_, e) =>
        {
            if (e.Node?.Tag is not FavoriteItem { Separator: false } item)
            {
                e.CancelEdit = true;
                return;
            }
            _labelEditing = true;
            string name = item.Name ?? "";
            BeginInvoke(() => _tree.SetEditText(name)); // 편집 칸이 만들어진 직후 이름만 넣는다
        };
        _tree.AfterLabelEdit += (_, e) =>
        {
            _labelEditing = false;
            e.CancelEdit = true; // 트리가 입력 글자를 그대로 항목 글자로 쓰지 않게 한다
            if (e.Node?.Tag is not FavoriteItem item || e.Label == null) return; // null = Esc로 취소
            string name = e.Label.Trim();
            if (name.Length == 0 || name == item.Name) return;
            item.Name = name;
            var node = e.Node;
            BeginInvoke(() =>
            {
                if (node.TreeView == null) return;
                RefreshNodeText(node);
                UpdateFields();
                Commit();
            });
        };
    }

    /// <summary>선택 항목들을 새 그룹으로 묶는다. 그룹은 첫 항목 자리에 생기고, 이름 칸으로 바로 이동한다.</summary>
    private void GroupNodes(List<TreeNode> picked)
    {
        var nodes = TopNodes(picked);
        if (nodes.Count == 0) return;
        var first = nodes[0];
        var siblings = first.Parent?.Nodes ?? _tree.Nodes;
        int index = first.Index;
        var group = MakeNode(new FavoriteItem { Name = "새 그룹", Items = new List<FavoriteItem>() });

        _tree.BeginUpdate();
        foreach (var n in nodes) n.Remove();
        siblings.Insert(Math.Min(index, siblings.Count), group);
        foreach (var n in nodes) group.Nodes.Add(n);
        group.ExpandAll();
        RefreshGroups();
        _tree.EndUpdate();
        _tree.SetPicked(new[] { group }, group);
        UpdateFields();
        Commit();
        RenameSelected();
    }

    private void Ungroup(TreeNode group)
    {
        var siblings = group.Parent?.Nodes ?? _tree.Nodes;
        int index = group.Index;
        var children = group.Nodes.Cast<TreeNode>().ToList();
        _tree.BeginUpdate();
        group.Remove();
        foreach (var c in children)
        {
            c.Remove();
            siblings.Insert(index++, c);
        }
        RefreshGroups();
        _tree.EndUpdate();
        _tree.SetPicked(children, children.FirstOrDefault());
        UpdateFields();
        Commit();
    }

    /// <summary>항목들을 그룹 끝(또는 최상위 끝)으로 옮긴다.</summary>
    private void MoveNodes(List<TreeNode> nodes, TreeNode? target)
    {
        var list = TopNodes(nodes).Where(n => n != target && (target == null || !IsDescendant(target, n))).ToList();
        if (list.Count == 0) return;
        _tree.BeginUpdate();
        foreach (var n in list)
        {
            n.Remove();
            (target?.Nodes ?? _tree.Nodes).Add(n);
        }
        target?.Expand();
        RefreshGroups();
        _tree.EndUpdate();
        _tree.SetPicked(list, list[0]);
        list[0].EnsureVisible();
        UpdateFields();
        Commit();
    }

    /// <summary>선택한 폴더들의 경로에서 문자열을 바꾼다(드라이브·상위 폴더 변경 등).</summary>
    private void ReplacePaths(List<TreeNode> nodes)
    {
        var folders = FoldersIn(nodes).ToList();
        using var dlg = Native.WithSystemDpi(new ReplacePathDialog(CommonPrefix(folders.Select(f => f.Path ?? ""))));
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Find.Length == 0) return;
        int changed = 0;
        foreach (var f in folders)
        {
            if (f.Path == null || f.Path.IndexOf(dlg.Find, StringComparison.OrdinalIgnoreCase) < 0) continue;
            f.Path = f.Path.Replace(dlg.Find, dlg.ReplaceWith, StringComparison.OrdinalIgnoreCase);
            changed++;
        }
        foreach (var n in _tree.AllNodes()) if (!((FavoriteItem)n.Tag!).IsGroup) RefreshNodeText(n);
        UpdateFields();
        Commit();
        _status.ForeColor = Theme.Muted;
        _status.Text = $"경로 {changed}개를 바꿨습니다(저장 필요).";
    }

    private static string CommonPrefix(IEnumerable<string> paths)
    {
        string? prefix = null;
        foreach (var p in paths)
        {
            if (prefix == null) { prefix = p; continue; }
            int i = 0;
            while (i < prefix.Length && i < p.Length && char.ToUpperInvariant(prefix[i]) == char.ToUpperInvariant(p[i])) i++;
            prefix = prefix[..i];
        }
        if (string.IsNullOrEmpty(prefix)) return "";
        int cut = prefix.LastIndexOf('\\'); // 폴더 경계까지만
        return cut >= 0 ? prefix[..(cut + 1)] : prefix;
    }

    private void MoveSelected(int delta)
    {
        var sel = _tree.SelectedNode;
        if (sel == null) return;
        var siblings = sel.Parent?.Nodes ?? _tree.Nodes;
        int index = sel.Index + delta;
        if (index < 0 || index >= siblings.Count) return;
        sel.Remove();
        siblings.Insert(index, sel);
        _tree.SelectedNode = sel;
        Commit();
    }

    private const string DragFormat = "DirectFolders.Nodes";

    private static bool InvalidTarget(TreeNode? target, TreeNode[] dragged) =>
        target != null && dragged.Any(d => target == d || IsDescendant(target, d));

    private void TreeDragOver(object? sender, DragEventArgs e)
    {
        e.Effect = DragDropEffects.None;
        if (e.Data?.GetData(DragFormat) is not TreeNode[] dragged) return;
        var target = _tree.GetNodeAt(_tree.PointToClient(new Point(e.X, e.Y)));
        if (InvalidTarget(target, dragged)) return;
        e.Effect = DragDropEffects.Move;
        if (target != null) _tree.SelectedNode = target;
    }

    private void TreeDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DragFormat) is not TreeNode[] dragged || dragged.Length == 0) return;
        var target = _tree.GetNodeAt(_tree.PointToClient(new Point(e.X, e.Y)));
        if (InvalidTarget(target, dragged)) return;
        _tree.BeginUpdate();
        foreach (var d in dragged) d.Remove();
        if (target == null) foreach (var d in dragged) _tree.Nodes.Add(d);
        else if (((FavoriteItem)target.Tag!).IsGroup) { foreach (var d in dragged) target.Nodes.Add(d); target.Expand(); }
        else
        {
            var siblings = target.Parent?.Nodes ?? _tree.Nodes; // 대상 앞에 삽입
            int index = target.Index;
            foreach (var d in dragged) siblings.Insert(index++, d);
        }
        RefreshGroups();
        _tree.EndUpdate();
        _tree.SetPicked(dragged, dragged[0]);
        Commit();
    }

    private static bool IsDescendant(TreeNode node, TreeNode ancestor)
    {
        for (var p = node.Parent; p != null; p = p.Parent)
            if (p == ancestor) return true;
        return false;
    }

    // ───────── 실행 취소 / 다시 실행 ─────────
    // 변경이 끝날 때마다 Commit()이 창 전체 상태(즐겨찾기·앱별 규칙·일반 설정)를 JSON 스냅숏으로 남긴다.
    // Ctrl+Z는 직전 스냅숏으로, Ctrl+Y(Ctrl+Shift+Z)는 되돌린 것을 다시 적용한다.

    private sealed record Snapshot(string Data, int[] Selection, int RuleIndex);

    private const int UndoLimit = 200;
    private readonly List<Snapshot> _undo = new();
    private readonly List<Snapshot> _redo = new();
    private Snapshot? _current;
    private string? _undoKey;
    private bool _restoring;

    private Snapshot TakeSnapshot()
    {
        var state = new Settings
        {
            Hotkey = _menuHotkey.Text,
            MiddleClick = _middleClick.Checked,
            ShowOpenFolders = _showOpen.Checked,
            Theme = SelectedTheme,
            Items = Collect(_tree.Nodes),
            AppFolders = _rules.Items.Cast<ListViewItem>().Select(i => (AppFolderRule)i.Tag!).ToList(),
        };
        string data = System.Text.Json.JsonSerializer.Serialize(state, SettingsStore.WriteOptions);
        return new Snapshot(data, SelectionPath(), _rules.SelectedIndices.Count > 0 ? _rules.SelectedIndices[0] : -1);
    }

    private int[] SelectionPath()
    {
        var path = new List<int>();
        for (var n = _tree.SelectedNode; n != null; n = n.Parent) path.Insert(0, n.Index);
        return path.ToArray();
    }

    private TreeNode? NodeAt(int[] path)
    {
        TreeNodeCollection nodes = _tree.Nodes;
        TreeNode? node = null;
        foreach (int i in path)
        {
            if (i < 0 || i >= nodes.Count) break;
            node = nodes[i];
            nodes = node.Nodes;
        }
        return node;
    }

    /// <summary>
    /// 변경 직후 호출. 실제로 달라졌으면 직전 상태를 실행 취소 목록에 넣는다.
    /// key가 직전 호출과 같으면(같은 칸에 이어서 입력) 한 단계로 묶는다.
    /// </summary>
    private void Commit(string? key = null)
    {
        if (_restoring || _current == null) return;
        var now = TakeSnapshot();
        if (now.Data == _current.Data) return;
        if (key == null || key != _undoKey)
        {
            _undo.Add(_current);
            if (_undo.Count > UndoLimit) _undo.RemoveAt(0);
        }
        _undoKey = key;
        _redo.Clear();
        _current = now;
    }

    /// <summary>선택만 바뀐 경우: 되돌렸을 때 그 선택으로 돌아오도록 기준 스냅숏의 선택을 갱신한다.</summary>
    private void SelectionMoved()
    {
        _undoKey = null;
        if (_restoring || _current == null) return;
        // 변경 도중(항목 추가·삭제 직후 선택 이동)에는 갱신하지 않는다: 기준 스냅숏은 변경 전 선택을 유지해야 한다
        var now = TakeSnapshot();
        if (now.Data == _current.Data) _current = now;
    }

    private void Undo() => Step(_undo, _redo);

    private void Redo() => Step(_redo, _undo);

    private void Step(List<Snapshot> from, List<Snapshot> to)
    {
        if (from.Count == 0 || _current == null) return;
        Commit(); // 아직 기록되지 않은 변경이 있으면 먼저 반영
        if (from.Count == 0) return; // 위 Commit이 다시 실행 목록을 비운 경우
        to.Add(_current);
        _current = from[^1];
        from.RemoveAt(from.Count - 1);
        Restore(_current);
    }

    private void Restore(Snapshot snap)
    {
        _restoring = true;
        try
        {
            var state = System.Text.Json.JsonSerializer.Deserialize<Settings>(snap.Data, SettingsStore.ReadOptions) ?? new Settings();

            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            foreach (var item in state.Items) _tree.Nodes.Add(MakeNode(item));
            _tree.ExpandAll();
            _tree.EndUpdate();

            _rules.BeginUpdate();
            _rules.Items.Clear();
            foreach (var rule in state.AppFolders) _rules.Items.Add(MakeRuleItem(rule));
            _rules.EndUpdate();
            if (snap.RuleIndex >= 0 && snap.RuleIndex < _rules.Items.Count) _rules.Items[snap.RuleIndex].Selected = true;

            _menuHotkey.Text = state.Hotkey;
            _middleClick.Checked = state.MiddleClick;
            _showOpen.Checked = state.ShowOpenFolders;
            _theme.SelectedIndex = Math.Max(0, Array.FindIndex(Theme.All, p => string.Equals(p.Name, state.Theme, StringComparison.OrdinalIgnoreCase)));

            var node = NodeAt(snap.Selection) ?? (_tree.Nodes.Count > 0 ? _tree.Nodes[0] : null);
            _tree.SetPicked(node != null ? new[] { node } : Array.Empty<TreeNode>(), node);
            node?.EnsureVisible();
            UpdateFields();
            _status.Text = "";
        }
        finally
        {
            _restoring = false;
            _undoKey = null;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // 단축키 입력 칸에서는 Ctrl+Z 등도 단축키 조합으로 받아야 하므로 가로채지 않는다
        // 목록에서 이름을 편집하는 중에도 입력 칸 자체의 편집 키가 우선
        if (!_itemHotkey.Focused && !_menuHotkey.Focused && !_labelEditing)
        {
            if (keyData == (Keys.Control | Keys.Z)) { Undo(); return true; }
            if (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z)) { Redo(); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ───────── 저장 ─────────

    private string? _savedData;

    private string SelectedTheme => Theme.All[Math.Clamp(_theme.SelectedIndex, 0, Theme.All.Length - 1)].Name;

    /// <summary>검사 후 저장한다. 창은 닫지 않는다. 검사 실패·저장 실패면 false.</summary>
    private bool Save()
    {
        _status.ForeColor = Theme.Red;
        var items = Collect(_tree.Nodes);

        // 단축키 검사: 형식, 중복(메뉴 단축키 포함)
        var used = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? menuKey = string.IsNullOrWhiteSpace(_menuHotkey.Text) ? null : HotkeyParser.Normalize(_menuHotkey.Text);
        if (!string.IsNullOrWhiteSpace(_menuHotkey.Text) && menuKey == null)
        {
            _status.Text = $"메뉴 단축키 형식 오류: {_menuHotkey.Text}";
            return false;
        }
        if (menuKey != null) used[menuKey] = "메뉴 호출";
        foreach (var item in FavoriteItem.Flatten(items).Where(i => !string.IsNullOrWhiteSpace(i.Hotkey)))
        {
            var key = HotkeyParser.Normalize(item.Hotkey);
            if (key == null)
            {
                _status.Text = $"단축키 형식 오류: {item.Name} ({item.Hotkey})";
                return false;
            }
            if (used.TryGetValue(key, out var owner))
            {
                _status.Text = $"단축키 중복: {key} ({owner} / {item.Name})";
                return false;
            }
            used[key] = item.Name ?? key;
            item.Hotkey = key;
        }

        var result = new Settings
        {
            Hotkey = menuKey ?? "",
            MiddleClick = _middleClick.Checked,
            ShowOpenFolders = _showOpen.Checked,
            Theme = SelectedTheme,
            Items = items,
            AppFolders = _rules.Items.Cast<ListViewItem>().Select(i => (AppFolderRule)i.Tag!).ToList(),
        };
        // 창은 열어 둔 채 계속 편집하므로, 넘기는 설정은 복사본이어야 한다(목록 항목 객체를 공유하지 않게)
        if (SaveHandler?.Invoke(result.Clone()) != true) return false;

        // 저장하며 정규화된 값(단축키 표기)을 화면과 기준 상태에 반영
        _loadingFields = true;
        _menuHotkey.Text = result.Hotkey;
        _loadingFields = false;
        foreach (var node in _tree.AllNodes()) RefreshNodeText(node);
        UpdateFields();
        Commit();
        _savedData = TakeSnapshot().Data;
        _status.ForeColor = Theme.Muted;
        _status.Text = "저장했습니다.";
        return true;
    }

    /// <summary>저장하지 않은 변경이 있는가.</summary>
    private bool Dirty => _current != null && TakeSnapshot().Data != _savedData;

    private bool _closeWithoutPrompt;

    /// <summary>묻지 않고 닫는다(설정 파일이 밖에서 바뀌어 이 창 내용이 낡았을 때, 테마 변경 후 다시 열 때 등).</summary>
    public void CloseWithoutPrompt()
    {
        _closeWithoutPrompt = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_closeWithoutPrompt && e.CloseReason == CloseReason.UserClosing && Dirty)
        {
            var answer = MessageBox.Show(this, "저장하지 않은 변경 내용이 있습니다. 저장할까요?", AppInfo.Name,
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !Save()))
                e.Cancel = true;
        }
        base.OnFormClosing(e);
    }
}

/// <summary>클릭 후 조합키를 누르면 "Ctrl+Alt+D" 형식으로 입력되는 칸. Backspace/Delete로 지움.</summary>
internal sealed class ShortcutBox : TextBox
{
    public ShortcutBox()
    {
        ShortcutsEnabled = false;
    }

    protected override bool IsInputKey(Keys keyData) => true; // Tab·Enter 등도 받는다

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;
        var key = e.KeyCode;
        if (key is Keys.Back or Keys.Delete && e.Modifiers == Keys.None)
        {
            Text = "";
            return;
        }
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
        uint mods = 0;
        if (e.Control) mods |= Native.MOD_CONTROL;
        if (e.Alt) mods |= Native.MOD_ALT;
        if (e.Shift) mods |= Native.MOD_SHIFT;
        if ((Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0) mods |= Native.MOD_WIN;
        if (mods == 0 && !(key >= Keys.F1 && key <= Keys.F24)) return; // 수정자 없는 일반 키는 받지 않음
        Text = HotkeyParser.Format(mods, key);
        SelectionStart = Text.Length;
    }

    protected override void OnKeyPress(KeyPressEventArgs e) => e.Handled = true;
}
