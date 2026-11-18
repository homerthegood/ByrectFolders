using System.Diagnostics;

namespace DirectFolders;

/// <summary>앱별 기본 폴더 규칙 하나를 편집하는 대화상자.</summary>
internal sealed class RuleDialog : Form
{
    public AppFolderRule Rule { get; private set; }

    private readonly ComboBox _app = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _title = new() { PlaceholderText = "예: Import, 내보내기" };
    private readonly TextBox _path = new();
    private readonly Label _error = new() { AutoSize = true, ForeColor = Theme.Red };

    private int S(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public RuleDialog(AppFolderRule rule)
    {
        Rule = rule;
        Text = "앱별 기본 폴더";
        AutoScaleMode = AutoScaleMode.None;
        Theme.Apply(this, Theme.Tile);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var t = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Padding = new Padding(S(20), S(8), S(20), S(18)) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(400)));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        int row = 0;
        void Field(string caption, Control inner, Control? side = null)
        {
            t.Controls.Add(Kit.Caption(this, caption), 0, row++);
            var frame = Kit.Input(this, inner);
            t.Controls.Add(frame, 0, row);
            if (side != null) t.Controls.Add(side, 1, row);
            else { frame.Margin = Padding.Empty; t.SetColumnSpan(frame, 2); }
            row++;
        }

        Field("앱(실행 파일)", _app);
        Field("대화상자", _kind);
        Field("제목 포함(선택)", _title);
        var browse = Kit.Button(this, "찾아보기", ButtonKind.Normal);
        browse.Anchor = AnchorStyles.Left;
        browse.Click += (_, _) => Browse();
        Field("폴더", _path, browse);
        _error.Margin = new Padding(S(2), S(6), 0, 0);
        t.Controls.Add(_error, 0, row);
        t.SetColumnSpan(_error, 2);
        row++;

        var ok = Kit.Button(this, "확인", ButtonKind.Primary, 90);
        var cancel = Kit.Button(this, "취소", ButtonKind.Normal, 90);
        cancel.DialogResult = DialogResult.Cancel;
        ok.Click += (_, _) => Accept();
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right, Margin = new Padding(0, S(12), 0, 0), BackColor = Theme.Tile,
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        t.Controls.Add(buttons, 0, row);
        t.SetColumnSpan(buttons, 2);
        Controls.Add(t);
        AcceptButton = ok;
        CancelButton = cancel;

        _kind.Items.AddRange(new object[] { "모두", "열기", "저장" });
        _kind.SelectedIndex = rule.DialogKind switch { "open" => 1, "save" => 2, _ => 0 };
        _app.Items.AddRange(RunningApps());
        _app.Text = rule.App;
        _title.Text = rule.Title ?? "";
        _path.Text = rule.Path;
    }

    /// <summary>창이 있는 실행 중 프로세스의 exe 이름 목록.</summary>
    private static object[] RunningApps()
    {
        try
        {
            return Process.GetProcesses()
                .Where(p => { try { return p.MainWindowHandle != IntPtr.Zero; } catch { return false; } })
                .Select(p => p.ProcessName + ".exe")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Cast<object>()
                .ToArray();
        }
        catch
        {
            return Array.Empty<object>();
        }
    }

    private void Browse()
    {
        using var dlg = new FolderBrowserDialog { UseDescriptionForTitle = true, Description = "기본 폴더 선택" };
        string current = Environment.ExpandEnvironmentVariables(_path.Text);
        if (Directory.Exists(current)) dlg.InitialDirectory = current;
        if (dlg.ShowDialog(this) == DialogResult.OK) _path.Text = dlg.SelectedPath;
    }

    private void Accept()
    {
        if (string.IsNullOrWhiteSpace(_app.Text)) { _error.Text = "앱을 입력하세요."; return; }
        if (string.IsNullOrWhiteSpace(_path.Text)) { _error.Text = "폴더를 입력하세요."; return; }
        Rule = new AppFolderRule
        {
            App = AppFolderRule.NormalizeApp(_app.Text),
            Dialog = _kind.SelectedIndex switch { 1 => "open", 2 => "save", _ => null },
            Title = string.IsNullOrWhiteSpace(_title.Text) ? null : _title.Text.Trim(),
            Path = _path.Text.Trim(),
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
