namespace DirectFolders;

/// <summary>설정 창 탭별 도움말 팝업. 섹션 제목 + 항목 줄로 구성.</summary>
internal sealed class HelpDialog : Form
{
    public record Section(string Title, params string[] Lines);

    private int S(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public HelpDialog(string title, IEnumerable<Section> sections)
    {
        Text = "도움말 - " + title;
        AutoScaleMode = AutoScaleMode.None;
        Theme.Apply(this, Theme.Tile);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode is Keys.Escape or Keys.F1) Close(); };

        int width = S(520);
        var body = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Tile, Margin = Padding.Empty, Padding = Padding.Empty,
        };
        var headFont = Theme.UiFont(11f);
        bool first = true;
        foreach (var sec in sections)
        {
            body.Controls.Add(new Label
            {
                Text = sec.Title, AutoSize = true, Font = headFont, ForeColor = Theme.BlueText,
                Margin = new Padding(0, first ? 0 : S(14), 0, S(4)),
            });
            first = false;
            foreach (var line in sec.Lines)
            {
                // "•" 뒤 내어쓰기: 두 줄로 넘어가도 글머리 기호 오른쪽에 맞춘다
                var row = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Tile, Margin = new Padding(0, S(2), 0, S(2)) };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(16)));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.Controls.Add(new Label { Text = "•", AutoSize = true, ForeColor = Theme.Muted, Margin = Padding.Empty }, 0, 0);
                row.Controls.Add(new Label { Text = line, AutoSize = true, MaximumSize = new Size(width - S(16), 0), ForeColor = Theme.Text, Margin = Padding.Empty, UseMnemonic = false }, 1, 0);
                body.Controls.Add(row);
            }
        }

        // 내용이 길면 스크롤
        var scroll = new Panel { AutoScroll = true, BackColor = Theme.Tile, Margin = Padding.Empty };
        scroll.Controls.Add(body);
        int maxH = S(520);
        void Fit()
        {
            // 줄바꿈이 반영된 실제 높이로 맞춘다(생성자 시점의 예상 크기는 부정확)
            body.PerformLayout();
            int h = body.Height;
            scroll.Size = new Size(width + (h > maxH ? SystemInformation.VerticalScrollBarWidth + S(8) : 0), Math.Min(h, maxH));
        }
        Fit();
        Load += (_, _) => Fit();

        var close = Kit.Button(this, "닫기", ButtonKind.Primary, 90);
        close.DialogResult = DialogResult.Cancel;
        close.Margin = new Padding(0, S(16), 0, 0);
        close.Anchor = AnchorStyles.Right;

        var t = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(S(22), S(18), S(22), S(18)), BackColor = Theme.Tile };
        t.Controls.Add(scroll);
        t.Controls.Add(close);
        Controls.Add(t);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        CancelButton = close;
        AcceptButton = close;
    }
}
