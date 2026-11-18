using System.Runtime.InteropServices;

namespace DirectFolders;

/// <summary>
/// 다중 선택 TreeView.
/// - Ctrl+클릭: 추가/해제, Shift+클릭: 범위, Ctrl+A: 전체
/// - 빈 곳(항목 이름 오른쪽 여백 포함)에서 끌기: 러버밴드(사각형) 선택. Ctrl을 누른 채 시작하면 기존 선택에 더한다.
/// - 항목 위에서 끌기는 기존처럼 ItemDrag(순서 이동). 여러 개 선택된 항목을 끌면 함께 이동하도록 Picked를 쓰면 된다.
/// - 우클릭: 선택 밖 항목이면 그 항목만 선택한 뒤 MenuRequested 발생.
/// SelectedNode는 "주 항목"(오른쪽 편집 칸 대상)이고, Picked는 선택된 전체(트리 순서).
/// </summary>
internal sealed class MultiSelectTree : TreeView
{
    private const int WM_PAINT = 0x000F, WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202,
        WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_CONTEXTMENU = 0x007B, WM_CAPTURECHANGED = 0x0215, WM_VSCROLL = 0x0115;

    private readonly HashSet<TreeNode> _picked = new();
    private TreeNode? _anchor;
    private bool _syncing, _keepOnSelect, _dragged;

    // 러버밴드
    private bool _band, _bandMoved;
    private Point _bandStart, _bandNow;
    private int _bandStartContentY;
    private HashSet<TreeNode> _bandBase = new();
    private Rectangle _bandDrawn;
    private readonly System.Windows.Forms.Timer _scroll = new() { Interval = 60 };

    // 선택된 행의 빈 곳에서 시작하는 끌기(항목 이름 위가 아니어도 이동할 수 있게)
    private TreeNode? _rowDrag;
    private Point _rowDragStart;

    public Color PickColor { get; set; } = Theme.Pick;

    /// <summary>이름 편집 중인 입력 칸의 글자를 바꾸고 전체 선택한다(항목 글자와 다른 값을 편집하게 할 때).</summary>
    public void SetEditText(string text)
    {
        if (!IsHandleCreated) return;
        IntPtr edit = SendMessage(Handle, 0x110F, 0, IntPtr.Zero); // TVM_GETEDITCONTROL
        if (edit == IntPtr.Zero) return;
        SetWindowText(edit, text);
        SendMessage(edit, 0x00B1, 0, (IntPtr)(-1)); // EM_SETSEL: 전체 선택
    }

    /// <summary>선택 집합이 바뀜.</summary>
    public event Action? PickedChanged;
    /// <summary>우클릭(클라이언트 좌표, 항목 위 여부) 또는 메뉴 키.</summary>
    public event Action<Point, bool>? MenuRequested;

    public MultiSelectTree()
    {
        _scroll.Tick += (_, _) => AutoScrollTick();
    }

    /// <summary>선택된 항목(트리 순서).</summary>
    public List<TreeNode> Picked
    {
        get
        {
            _picked.RemoveWhere(n => n.TreeView != this);
            return AllNodes().Where(_picked.Contains).ToList();
        }
    }

    public IEnumerable<TreeNode> AllNodes()
    {
        var stack = new Stack<TreeNode>();
        for (int i = Nodes.Count - 1; i >= 0; i--) stack.Push(Nodes[i]);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            for (int i = n.Nodes.Count - 1; i >= 0; i--) stack.Push(n.Nodes[i]);
        }
    }

    private List<TreeNode> VisibleNodes()
    {
        var list = new List<TreeNode>();
        for (var n = Nodes.Count > 0 ? Nodes[0] : null; n != null; n = n.NextVisibleNode) list.Add(n);
        return list;
    }

    /// <summary>선택 집합을 바꾼다. primary가 있으면 SelectedNode도 그것으로.</summary>
    public void SetPicked(IEnumerable<TreeNode> nodes, TreeNode? primary)
    {
        var set = nodes.Where(n => n != null && n.TreeView == this).ToHashSet();
        foreach (var old in _picked.Where(n => !set.Contains(n)).ToList()) old.BackColor = Color.Empty;
        _picked.Clear();
        _picked.UnionWith(set);
        foreach (var n in _picked) n.BackColor = _picked.Count > 1 ? PickColor : Color.Empty;
        if (primary != null && primary.TreeView == this && SelectedNode != primary)
        {
            _syncing = true;
            try { SelectedNode = primary; } finally { _syncing = false; }
        }
        else if (primary == null && set.Count == 0 && SelectedNode != null)
        {
            _syncing = true;
            try
            {
                SelectedNode = null;
                OnAfterSelect(new TreeViewEventArgs(null)); // 선택 해제는 AfterSelect가 안 오므로 편집 칸을 직접 비운다
            }
            finally { _syncing = false; }
        }
        PickedChanged?.Invoke();
    }

    protected override void OnAfterSelect(TreeViewEventArgs e)
    {
        if (!_syncing && !_keepOnSelect)
        {
            _anchor = SelectedNode;
            SetPicked(SelectedNode != null ? new[] { SelectedNode } : Array.Empty<TreeNode>(), null);
        }
        base.OnAfterSelect(e);
    }

    protected override void OnItemDrag(ItemDragEventArgs e)
    {
        _dragged = true;
        base.OnItemDrag(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.A && e.Control)
        {
            SetPicked(AllNodes(), SelectedNode ?? (Nodes.Count > 0 ? Nodes[0] : null));
            e.Handled = e.SuppressKeyPress = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private static Point PointOf(IntPtr lParam)
    {
        long v = lParam.ToInt64();
        return new Point((short)(v & 0xffff), (short)((v >> 16) & 0xffff));
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_LBUTTONDOWN:
            {
                var pt = PointOf(m.LParam);
                var hit = HitTest(pt);
                if (hit.Node != null && hit.Location == TreeViewHitTestLocations.PlusMinus) break;
                bool onItem = hit.Node != null && hit.Location is TreeViewHitTestLocations.Label or TreeViewHitTestLocations.Image or TreeViewHitTestLocations.StateImage;
                var mods = ModifierKeys;
                if (onItem && (mods & Keys.Control) != 0) { Focus(); Toggle(hit.Node!); return; }
                if (onItem && (mods & Keys.Shift) != 0) { Focus(); RangeTo(hit.Node!); return; }
                if (!onItem && hit.Node != null && _picked.Contains(hit.Node) && (mods & (Keys.Control | Keys.Shift)) == 0)
                {
                    // 이미 선택된(파랗게 표시된) 행: 어디를 눌러도 끌어서 옮길 수 있다. 끌지 않고 떼면 그 항목만 선택.
                    Focus();
                    _rowDrag = hit.Node;
                    _rowDragStart = pt;
                    Capture = true;
                    return;
                }
                if (!onItem) { Focus(); BeginBand(pt, (mods & Keys.Control) != 0); return; }

                // 일반 클릭: 여러 개 선택된 항목 위라면 끌기(함께 이동)에 대비해 선택을 유지한 채 기본 처리
                var node = hit.Node!;
                _dragged = false;
                _keepOnSelect = _picked.Count > 1 && _picked.Contains(node);
                try { base.WndProc(ref m); }
                finally { _keepOnSelect = false; }
                if (node.TreeView != this) return;
                if (!_dragged)
                {
                    _anchor = node;
                    SetPicked(new[] { node }, node);
                }
                else if (SelectedNode == null || !_picked.Contains(SelectedNode))
                {
                    var list = Picked;
                    SetPicked(list, list.FirstOrDefault());
                }
                return;
            }
            case WM_MOUSEMOVE when _rowDrag != null:
            {
                var pt = PointOf(m.LParam);
                var d = SystemInformation.DragSize;
                if (Math.Abs(pt.X - _rowDragStart.X) < d.Width && Math.Abs(pt.Y - _rowDragStart.Y) < d.Height) return;
                var node = _rowDrag;
                _rowDrag = null;
                Capture = false;
                if (node.TreeView != this) return;
                _keepOnSelect = true; // 끄는 동안 대상 강조로 선택이 바뀌어도 선택 집합은 유지
                try { OnItemDrag(new ItemDragEventArgs(MouseButtons.Left, node)); }
                finally { _keepOnSelect = false; }
                if (SelectedNode == null || !_picked.Contains(SelectedNode))
                {
                    var list = Picked;
                    SetPicked(list, list.FirstOrDefault());
                }
                return;
            }
            case WM_LBUTTONUP when _rowDrag != null:
            {
                var node = _rowDrag;
                _rowDrag = null;
                Capture = false;
                if (node.TreeView == this)
                {
                    _anchor = node;
                    SetPicked(new[] { node }, node);
                }
                return;
            }
            case WM_CAPTURECHANGED when _rowDrag != null:
                _rowDrag = null;
                break;
            case WM_MOUSEMOVE when _band:
                UpdateBand(PointOf(m.LParam));
                return;
            case WM_LBUTTONUP when _band:
                EndBand(PointOf(m.LParam));
                return;
            case WM_CAPTURECHANGED when _band:
                EndBand(null);
                break;
            case WM_RBUTTONDOWN:
            {
                var pt = PointOf(m.LParam);
                var node = GetNodeAt(pt);
                Focus();
                if (node != null && !_picked.Contains(node))
                {
                    _anchor = node;
                    SetPicked(new[] { node }, node);
                }
                return; // 기본 처리(임시 강조·NM_RCLICK)는 건너뛴다
            }
            case WM_RBUTTONUP:
            {
                var pt = PointOf(m.LParam);
                MenuRequested?.Invoke(pt, GetNodeAt(pt) != null);
                return;
            }
            case WM_CONTEXTMENU:
                if (m.LParam.ToInt64() == -1) // 메뉴 키·Shift+F10
                {
                    var b = SelectedNode?.Bounds ?? Rectangle.Empty;
                    MenuRequested?.Invoke(new Point(b.Left, b.Bottom), SelectedNode != null);
                }
                return;
            case WM_PAINT:
                base.WndProc(ref m);
                if (_band && _bandMoved) DrawBand();
                return;
        }
        base.WndProc(ref m);
    }

    private void Toggle(TreeNode node)
    {
        var list = Picked;
        if (_picked.Contains(node))
        {
            list.Remove(node);
            var primary = SelectedNode == node ? list.LastOrDefault() : SelectedNode;
            SetPicked(list, primary);
        }
        else
        {
            list.Add(node);
            SetPicked(list, node);
        }
        _anchor = node;
    }

    private void RangeTo(TreeNode node)
    {
        var vis = VisibleNodes();
        int a = _anchor != null ? vis.IndexOf(_anchor) : -1, b = vis.IndexOf(node);
        if (a < 0 || b < 0) { _anchor = node; SetPicked(new[] { node }, node); return; }
        var range = vis.GetRange(Math.Min(a, b), Math.Abs(a - b) + 1);
        if ((ModifierKeys & Keys.Control) != 0) range = range.Union(_picked).ToList();
        SetPicked(range, node);
    }

    // ───────── 러버밴드 ─────────

    private int TopOffset()
    {
        var top = TopNode;
        if (top == null) return 0;
        int i = VisibleNodes().IndexOf(top);
        return Math.Max(0, i) * ItemHeight;
    }

    private void BeginBand(Point pt, bool additive)
    {
        _band = true;
        _bandMoved = false;
        _bandStart = _bandNow = pt;
        _bandStartContentY = pt.Y + TopOffset();
        _bandBase = additive ? new HashSet<TreeNode>(_picked) : new HashSet<TreeNode>();
        _bandDrawn = Rectangle.Empty;
        if (!additive && (_picked.Count > 0 || SelectedNode != null)) SetPicked(Array.Empty<TreeNode>(), null);
        Capture = true;
        _scroll.Start();
    }

    private Rectangle BandRect()
    {
        int startY = _bandStartContentY - TopOffset();
        return Rectangle.FromLTRB(Math.Min(_bandStart.X, _bandNow.X), Math.Min(startY, _bandNow.Y),
                                  Math.Max(_bandStart.X, _bandNow.X), Math.Max(startY, _bandNow.Y));
    }

    private void UpdateBand(Point pt)
    {
        _bandNow = pt;
        if (!_bandMoved)
        {
            var d = SystemInformation.DragSize;
            if (Math.Abs(pt.X - _bandStart.X) < d.Width && Math.Abs(pt.Y - _bandStart.Y) < d.Height) return;
            _bandMoved = true;
        }

        // 화면 밖으로 스크롤된 항목도 포함하도록 내용 좌표(전체 목록 기준 Y)로 비교
        int top = TopOffset();
        int y1 = Math.Min(_bandStartContentY, pt.Y + top), y2 = Math.Max(_bandStartContentY, pt.Y + top);
        var vis = VisibleNodes();
        var hits = new List<TreeNode>();
        for (int i = 0; i < vis.Count; i++)
        {
            int ry = i * ItemHeight;
            if (ry + ItemHeight > y1 && ry < y2) hits.Add(vis[i]);
        }
        var set = new HashSet<TreeNode>(_bandBase);
        set.UnionWith(hits);
        if (!set.SetEquals(_picked))
        {
            _syncing = true; // 끄는 동안 주 항목은 바꾸지 않는다(끝날 때 정함)
            try { SetPicked(set, null); } finally { _syncing = false; }
        }

        var rect = BandRect();
        var dirty = Rectangle.Union(_bandDrawn, rect);
        dirty.Inflate(2, 2);
        _bandDrawn = rect;
        Invalidate(dirty);
        Update();
    }

    private void DrawBand()
    {
        var r = Rectangle.Intersect(BandRect(), ClientRectangle);
        if (r.Width <= 0 || r.Height <= 0) return;
        using var g = CreateGraphics();
        using var fill = new SolidBrush(Color.FromArgb(40, Theme.Blue));
        using var pen = new Pen(Theme.Blue);
        g.FillRectangle(fill, r);
        g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
    }

    private void EndBand(Point? pt)
    {
        if (!_band) return;
        _band = false;
        _scroll.Stop();
        if (Capture) Capture = false;
        var dirty = _bandDrawn;
        dirty.Inflate(2, 2);
        Invalidate(dirty);

        if (!_bandMoved)
        {
            // 그냥 클릭: 행의 빈 곳이면 그 항목 선택, 목록 아래 빈 곳이면 선택 해제
            var node = pt is { } p ? GetNodeAt(p) : null;
            if (node != null)
            {
                if (_bandBase.Count > 0) Toggle(node);
                else { _anchor = node; SetPicked(new[] { node }, node); }
            }
            else if (_bandBase.Count == 0) SetPicked(Array.Empty<TreeNode>(), null);
            return;
        }
        var list = Picked;
        var primary = SelectedNode != null && _picked.Contains(SelectedNode) ? SelectedNode : list.FirstOrDefault();
        if (primary != null) _anchor = primary;
        SetPicked(list, primary);
    }

    private void AutoScrollTick()
    {
        if (!_band) { _scroll.Stop(); return; }
        if ((MouseButtons & MouseButtons.Left) == 0) return; // 버튼을 실제로 누르고 있을 때만
        var p = PointToClient(Cursor.Position);
        if (p.Y >= 0 && p.Y <= ClientSize.Height) return;
        SendMessage(Handle, WM_VSCROLL, p.Y < 0 ? 0 : 1, IntPtr.Zero); // SB_LINEUP / SB_LINEDOWN
        UpdateBand(p);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _scroll.Dispose();
        base.Dispose(disposing);
    }

    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, int w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowText(IntPtr h, string text);
}
