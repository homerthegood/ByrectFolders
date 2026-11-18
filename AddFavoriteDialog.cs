namespace DirectFolders;

/// <summary>"여기 추가": 즐겨찾기 이름 입력.</summary>
internal sealed class AddFavoriteDialog : Form
{
    private readonly TextBox _name = new();
    public string FavoriteName => _name.Text.Trim();

    private int S(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public AddFavoriteDialog(string folder, string defaultName)
    {
        Text = "즐겨찾기 추가";
        AutoScaleMode = AutoScaleMode.None;
        Theme.Apply(this, Theme.Tile);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        // 내용 크기에 맞춰 창 크기를 정한다(긴 경로·DPI 배율에도 버튼이 잘리지 않게)
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var t = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(S(20), S(8), S(20), S(18)) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(520)));

        // 경로는 한 줄 읽기 전용 칸: 길면 칸 안에서 스크롤, 전체 선택·복사 가능
        var path = new TextBox { Text = folder, ReadOnly = true, TabStop = false };
        var pathFrame = Kit.Input(this, path);
        pathFrame.Margin = Padding.Empty;
        new ToolTip().SetToolTip(path, folder);
        var nameFrame = Kit.Input(this, _name);
        nameFrame.Margin = Padding.Empty;

        t.Controls.Add(Kit.Caption(this, "폴더"));
        t.Controls.Add(pathFrame);
        t.Controls.Add(Kit.Caption(this, "이름"));
        t.Controls.Add(nameFrame);

        var ok = Kit.Button(this, "추가", ButtonKind.Primary, 90);
        ok.DialogResult = DialogResult.OK;
        var cancel = Kit.Button(this, "취소", ButtonKind.Normal, 90);
        cancel.DialogResult = DialogResult.Cancel;
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, S(16), 0, 0),
            BackColor = Theme.Tile,
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        t.Controls.Add(buttons);
        Controls.Add(t);
        AcceptButton = ok;
        CancelButton = cancel;

        _name.Text = defaultName;
        _name.TextChanged += (_, _) => ok.Enabled = FavoriteName.Length > 0;

        Load += (_, _) =>
        {
            // 크기가 확정된 뒤 커서 근처, 화면 안쪽에 배치
            var cursor = Cursor.Position;
            var area = Screen.FromPoint(cursor).WorkingArea;
            Location = new Point(
                Math.Clamp(cursor.X - Width / 2, area.Left, Math.Max(area.Left, area.Right - Width)),
                Math.Clamp(cursor.Y - Height / 2, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        };
        Shown += (_, _) =>
        {
            Activate();
            path.SelectionStart = folder.Length; // 경로 끝(폴더 이름 쪽)이 보이게
            _name.Focus();
            _name.SelectAll();
        };
    }
}
