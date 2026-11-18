namespace DirectFolders;

/// <summary>선택한 즐겨찾기 경로의 일부를 일괄로 바꾼다(드라이브 문자·상위 폴더 이동 등).</summary>
internal sealed class ReplacePathDialog : Form
{
    private readonly TextBox _find = new();
    private readonly TextBox _replace = new();

    public string Find => _find.Text;
    public string ReplaceWith => _replace.Text;

    private int S(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public ReplacePathDialog(string initial)
    {
        Text = "경로 일괄 바꾸기";
        AutoScaleMode = AutoScaleMode.None;
        Theme.Apply(this, Theme.Tile);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var t = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(S(20), S(8), S(20), S(18)) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(480)));
        var findFrame = Kit.Input(this, _find);
        findFrame.Margin = Padding.Empty;
        var replaceFrame = Kit.Input(this, _replace);
        replaceFrame.Margin = Padding.Empty;
        t.Controls.Add(Kit.Caption(this, "찾을 문자열"));
        t.Controls.Add(findFrame);
        t.Controls.Add(Kit.Caption(this, "바꿀 문자열"));
        t.Controls.Add(replaceFrame);

        var ok = Kit.Button(this, "바꾸기", ButtonKind.Primary, 90);
        ok.DialogResult = DialogResult.OK;
        var cancel = Kit.Button(this, "취소", ButtonKind.Normal, 90);
        cancel.DialogResult = DialogResult.Cancel;
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right, Margin = new Padding(0, S(16), 0, 0), BackColor = Theme.Tile,
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        t.Controls.Add(buttons);
        Controls.Add(t);
        AcceptButton = ok;
        CancelButton = cancel;

        _find.Text = initial;
        _replace.Text = initial;
        _find.TextChanged += (_, _) => ok.Enabled = _find.Text.Length > 0;
        ok.Enabled = initial.Length > 0;
        Shown += (_, _) => { _replace.Focus(); _replace.SelectAll(); };
    }
}
