using System.Diagnostics;

namespace DirectFolders;

internal sealed class TrayApp : ApplicationContext
{
    private const int MenuHotkeyId = 1;
    private const int ItemHotkeyBase = 100;
    private const int WmShowMenu = Native.WM_APP + 1;
    public const int WmExit = Native.WM_APP + 2;
    public const int WmOpenSettings = Native.WM_APP + 3;
    private const int WmStartupCheck = Native.WM_APP + 4;
    public const string HostCaption = "DirectFoldersHost";

    private readonly NotifyIcon _tray;
    private readonly HostWindow _host;
    private readonly MouseHook? _hook;
    private readonly IntPtr _hostHandle;
    private readonly Dictionary<int, string> _itemHotkeys = new();
    private volatile Settings _settings = new();
    private volatile bool _menuOpen;
    private SettingsForm? _settingsForm;
    private readonly DialogWatcher _dialogWatcher;

    public TrayApp()
    {
        _host = new HostWindow(this);
        _hostHandle = _host.Handle;

        _tray = new NotifyIcon
        {
            Icon = LoadFolderIcon(),
            Text = AppInfo.Name,
            Visible = true,
        };

        // 왼쪽 클릭 = 즐겨찾기 메뉴, 더블클릭 = 설정 창.
        // 더블클릭의 첫 클릭에 메뉴가 떠 버리지 않도록, 클릭 후 더블클릭 판정 시간만큼 기다렸다가 메뉴를 띄운다.
        _clickTimer.Interval = SystemInformation.DoubleClickTime;
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            ShowMenu(IntPtr.Zero, _clickPoint);
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || !Native.GetCursorPos(out var pt)) return;
            _clickPoint = pt;
            _clickTimer.Stop();
            _clickTimer.Start();
        };
        _tray.MouseDoubleClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            _clickTimer.Stop();
            OpenSettingsForm();
        };

        try
        {
            Startup.RefreshPath();
        }
        catch (Exception ex)
        {
            Log.Write("startup read failed: " + ex.Message);
        }
        Reload(notify: false); // 테마가 여기서 정해지므로 트레이 메뉴는 그 뒤에 만든다
        BuildTrayMenu();
        _dialogWatcher = new DialogWatcher(() => _settings);
        Native.PostMessage(_hostHandle, WmStartupCheck, IntPtr.Zero, IntPtr.Zero); // 메시지 루프 시작 후 첫 실행 안내

        try
        {
            _hook = new MouseHook
            {
                ShouldIntercept = root => !_menuOpen && _settings.MiddleClick && IsMenuTarget(root),
                // 훅 스레드에서 호출됨: 좌표는 lParam에 담아 UI 스레드로 넘긴다
                Intercepted = (root, pt) => Native.PostMessage(_hostHandle, WmShowMenu, root,
                    (IntPtr)(((long)pt.Y << 32) | (uint)pt.X)),
            };
        }
        catch (Exception ex)
        {
            Balloon("휠 클릭 훅 설치 실패: " + ex.Message, ToolTipIcon.Error);
        }
    }

    private readonly System.Windows.Forms.Timer _clickTimer = new();
    private Native.POINT _clickPoint;

    /// <summary>트레이 아이콘 우클릭 메뉴. 테마 색을 쓰므로 테마가 바뀌면 다시 만든다.</summary>
    private void BuildTrayMenu()
    {
        var menu = Kit.Menu(null, checkMargin: true);
        ToolStripMenuItem Item(string text, Action a) => Kit.MenuItem(null, text, a);
        menu.Items.Add(Item("설정...", () => OpenSettingsForm()));
        menu.Items.Add(Item("설정 파일 열기", OpenSettingsFile));
        menu.Items.Add(Item("설정 다시 읽기", () => Reload(notify: true)));
        menu.Items.Add(Item("최근 폴더 기록 지우기", () =>
        {
            FolderHistory.Clear();
            Balloon("최근 폴더 기록을 지웠습니다.", ToolTipIcon.Info);
        }));
        menu.Items.Add(new ToolStripSeparator());
        var startupItem = _startupItem = new ToolStripMenuItem("Windows 시작 시 실행") { CheckOnClick = true, Padding = Kit.MenuItemPadding(null) };
        try
        {
            startupItem.Checked = Startup.IsEnabled();
        }
        catch (Exception ex)
        {
            Log.Write("startup read failed: " + ex.Message);
        }
        startupItem.CheckedChanged += (_, _) =>
        {
            try
            {
                Startup.Set(startupItem.Checked);
            }
            catch (Exception ex)
            {
                Balloon("자동 실행 설정 실패: " + ex.Message, ToolTipIcon.Error);
            }
        };
        menu.Items.Add(startupItem);
        if (!Installer.IsRunningInstalledCopy)
            menu.Items.Add(Item("설치...", RunInstall));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("종료", ExitThread));

        var old = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = menu;
        old?.Dispose();
    }

    private static bool IsShellWindow(string cls) => cls is "Progman" or "WorkerW" or "CabinetWClass";

    private static bool IsMenuTarget(IntPtr root)
    {
        return IsShellWindow(Native.ClassOf(root)) || FileDialogNavigator.IsFileDialog(root);
    }

    // ───────── 설정·단축키 ─────────

    private void Reload(bool notify)
    {
        var loaded = SettingsStore.Load(out var error);
        if (error != null)
        {
            Balloon("설정 파일 오류: " + error, ToolTipIcon.Error);
            return;
        }
        Apply(loaded, notify ? "설정을 다시 읽었습니다." : null);
    }

    private void Apply(Settings settings, string? okMessage)
    {
        _settings = settings;
        if (Theme.Use(settings.Theme) && _tray.ContextMenuStrip != null) BuildTrayMenu(); // 테마가 바뀌면 트레이 메뉴 색도 다시
        var problems = RegisterHotkeys();
        if (problems.Count > 0)
            Balloon(string.Join("\n", problems), ToolTipIcon.Warning);
        else if (okMessage != null)
            Balloon(okMessage, ToolTipIcon.Info);
    }

    private List<string> RegisterHotkeys()
    {
        var problems = new List<string>();
        Native.UnregisterHotKey(_hostHandle, MenuHotkeyId);
        foreach (var id in _itemHotkeys.Keys) Native.UnregisterHotKey(_hostHandle, id);
        _itemHotkeys.Clear();

        if (!string.IsNullOrWhiteSpace(_settings.Hotkey))
        {
            if (!HotkeyParser.TryParse(_settings.Hotkey, out uint mods, out uint vk))
                problems.Add($"메뉴 단축키 형식 오류: {_settings.Hotkey}");
            else if (!Native.RegisterHotKey(_hostHandle, MenuHotkeyId, mods | Native.MOD_NOREPEAT, vk))
                problems.Add($"메뉴 단축키 등록 실패(사용 중): {_settings.Hotkey}");
        }

        int nextId = ItemHotkeyBase;
        foreach (var item in FavoriteItem.Flatten(_settings.Items))
        {
            if (string.IsNullOrWhiteSpace(item.Hotkey) || item.ExpandedPath == null) continue;
            if (!HotkeyParser.TryParse(item.Hotkey, out uint mods, out uint vk))
            {
                problems.Add($"단축키 형식 오류: {item.Name} ({item.Hotkey})");
                continue;
            }
            int id = nextId++;
            if (Native.RegisterHotKey(_hostHandle, id, mods | Native.MOD_NOREPEAT, vk))
                _itemHotkeys[id] = item.ExpandedPath;
            else
                problems.Add($"단축키 등록 실패(사용 중): {item.Name} ({item.Hotkey})");
        }
        return problems;
    }

    private void OpenSettingsForm(int tab = 0)
    {
        if (_settingsForm != null)
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized) _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Activate();
            return;
        }
        var form = _settingsForm = Native.WithSystemDpi(new SettingsForm(_settings.Clone()));
        form.SaveHandler = saved =>
        {
            try
            {
                string before = Theme.P.Name;
                SettingsStore.Save(saved);
                Apply(saved, null); // 창이 열린 채로 "저장했습니다"를 표시하므로 알림 풍선은 띄우지 않는다
                if (Theme.P.Name != before)
                {
                    // 테마는 새로 만드는 창부터 적용되므로 설정 창을 같은 탭으로 다시 연다
                    int current = form.CurrentTab;
                    form.BeginInvoke(() =>
                    {
                        form.CloseWithoutPrompt();
                        OpenSettingsForm(current);
                    });
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(form, "저장 실패: " + ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        };
        form.FormClosed += (_, _) => { if (_settingsForm == form) _settingsForm = null; };
        if (tab != 0) form.ShowTab(tab);
        form.Show();
        form.Activate();
    }

    // ───────── 메시지 ─────────

    internal void OnMessage(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            int id = (int)m.WParam;
            IntPtr target = Native.GetAncestor(Native.GetForegroundWindow(), Native.GA_ROOT);
            if (id == MenuHotkeyId)
            {
                if (Native.GetCursorPos(out var pt)) ShowMenuAfterModifiersReleased(target, pt);
            }
            else if (_itemHotkeys.TryGetValue(id, out var path))
            {
                Log.Write($"item hotkey target={target} class={Native.ClassOf(target)} path={path}");
                OpenSafe(target, path);
            }
        }
        else if (m.Msg == WmShowMenu)
        {
            long packed = m.LParam.ToInt64();
            var pt = new Native.POINT { X = unchecked((int)(packed & 0xFFFFFFFF)), Y = (int)(packed >> 32) };
            IntPtr root = m.WParam;

            // 탐색기·바탕화면에서 항목 위 휠 클릭이면 원래 동작(새 탭 열기 등)을 되살린다
            if (IsShellWindow(Native.ClassOf(root)) && ExplorerHitTest.IsOverItem(pt))
            {
                _ = Native.ReplayMiddleClickAsync();
                return;
            }
            ShowMenu(root, pt);
        }
        else if (m.Msg == WmExit)
        {
            ExitThread();
        }
        else if (m.Msg == WmOpenSettings)
        {
            OpenSettingsForm();
        }
        else if (m.Msg == WmStartupCheck)
        {
            OfferAutostart();
            OfferDirectFoldersImport();
        }
    }

    private ToolStripMenuItem? _startupItem;

    /// <summary>
    /// 처음 한 번: Windows 시작 시 자동 실행을 켤지 묻는다. 이미 켜져 있으면 묻지 않는다.
    /// 답과 상관없이 다시 묻지 않으며, 트레이 메뉴의 'Windows 시작 시 실행'으로 언제든 바꿀 수 있다.
    /// </summary>
    private void OfferAutostart()
    {
        var state = AppState.Load();
        if (state.AutostartAsked) return;
        state.AutostartAsked = true;
        state.Save();
        try
        {
            if (Startup.IsEnabled()) return;
        }
        catch (Exception ex)
        {
            Log.Write("startup read failed: " + ex.Message);
            return;
        }

        using var owner = TopOwner();
        var answer = MessageBox.Show(owner,
            $"Windows를 시작할 때 {AppInfo.Name}를 자동으로 실행할까요?\n\n나중에 트레이 아이콘 우클릭 > 'Windows 시작 시 실행'에서 바꿀 수 있습니다.",
            AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;
        try
        {
            Startup.Set(true);
            if (_startupItem != null) _startupItem.Checked = true; // CheckedChanged에서 같은 값으로 한 번 더 기록(무해)
            Log.Write("autostart enabled on first run");
        }
        catch (Exception ex)
        {
            Balloon("자동 실행 설정 실패: " + ex.Message, ToolTipIcon.Error);
        }
    }

    // ───────── Direct Folders 가져오기 ─────────

    /// <summary>
    /// 처음 한 번: Direct Folders 설정이 있고 새로 가져올 항목이 있으면 가져올지 묻는다.
    /// 답(예/아니요)과 상관없이 다시 묻지 않는다. 설정 창의 가져오기 버튼으로 언제든 다시 할 수 있다.
    /// </summary>
    private void OfferDirectFoldersImport()
    {
        var state = AppState.Load();
        if (state.DirectFoldersImportAsked) return;
        string? file = DirectFoldersImport.FindMenuFile();
        if (file == null) return; // Direct Folders가 나중에 설치될 수 있으므로 기록하지 않는다

        DirectFoldersImport.Result found;
        try
        {
            found = DirectFoldersImport.Load(file);
        }
        catch (Exception ex)
        {
            Log.Write("direct folders import read failed: " + ex.Message);
            return;
        }
        var newItems = DirectFoldersImport.NewItems(_settings.Items, found.Items);
        int newCount = FavoriteItem.Flatten(newItems).Count(i => !i.IsGroup && !i.Separator);
        var newRecent = found.RecentFolders.Where(r => !FolderHistory.Items.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();

        state.DirectFoldersImportAsked = true;
        state.Save();
        if (newCount == 0 && newRecent.Count == 0) return;

        string msg = $"Direct Folders 설정을 찾았습니다.\n{file}\n\n"
                   + $"즐겨찾기 {found.FolderCount}개 중 새 항목 {newCount}개"
                   + (found.FolderCount > newCount ? $" (이미 있는 {found.FolderCount - newCount}개 제외)" : "")
                   + $"\n최근 폴더 {newRecent.Count}개\n\n"
                   + "가져올까요? 즐겨찾기 목록 끝에 추가됩니다.\n"
                   + "나중에 설정 > 즐겨찾기 > 'Direct Folders에서 가져오기'로도 할 수 있습니다.";
        using var owner = TopOwner();
        if (MessageBox.Show(owner, msg, AppInfo.Name + " - 즐겨찾기 가져오기", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            var updated = _settings.Clone();
            if (newItems.Count > 0)
            {
                if (updated.Items.Count > 0 && !updated.Items[^1].Separator) updated.Items.Add(new FavoriteItem { Separator = true });
                updated.Items.AddRange(newItems);
                SettingsStore.Save(updated);
            }
            FolderHistory.Merge(newRecent);
            Apply(updated, $"Direct Folders에서 즐겨찾기 {newCount}개, 최근 폴더 {newRecent.Count}개를 가져왔습니다.");
            Log.Write($"direct folders import: {newCount} items, {newRecent.Count} recent, skipped {found.Skipped}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "가져오기 실패: " + ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>트레이 앱은 보이는 창이 없으므로, 메시지 상자가 다른 창 뒤에 숨지 않도록 임시 최상위 소유 창을 쓴다.</summary>
    private static Form TopOwner()
    {
        var f = new Form
        {
            TopMost = true, ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual, Size = new Size(1, 1), Opacity = 0,
        };
        // 메시지 상자는 소유 창 기준으로 가운데 배치되므로 커서가 있는 화면 가운데에 둔다
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        f.Location = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
        Native.WithSystemDpi(f);
        f.Show();
        Native.ForceForeground(f.Handle);
        return f;
    }

    /// <summary>
    /// 단축키의 Alt 등을 누른 채로 메뉴를 띄우면 키를 놓는 순간 메뉴가 닫힌다.
    /// 수정자 키가 모두 떨어질 때까지(최대 1.5초) 기다린 뒤 표시한다.
    /// </summary>
    private async void ShowMenuAfterModifiersReleased(IntPtr target, Native.POINT pt)
    {
        int[] mods = { 0x10, 0x11, 0x12, 0x5B, 0x5C }; // Shift, Ctrl, Alt, LWin, RWin
        for (int i = 0; i < 30 && mods.Any(vk => (Native.GetAsyncKeyState(vk) & 0x8000) != 0); i++)
            await Task.Delay(50);
        ShowMenu(target, pt);
    }

    private void ShowMenu(IntPtr target, Native.POINT pt)
    {
        if (_menuOpen) return;
        MenuPick? pick;
        _menuOpen = true;
        try
        {
            var sw = Stopwatch.StartNew();
            var openFolders = _settings.ShowOpenFolders ? ExplorerShell.GetOpenFolders() : new List<string>();
            Log.Write($"open folders: {openFolders.Count}개, {sw.ElapsedMilliseconds}ms");
            var model = new MenuModel
            {
                Items = _settings.Items,
                OpenFolders = openFolders,
                RecentFolders = FolderHistory.Items.Take(15).ToList(),
                RecentFiles = RecentFiles.Get(),
                CurrentFolder = CurrentFolderOf(target),
            };
            pick = FavoritesMenu.Show(model, _hostHandle, pt);
        }
        finally
        {
            _menuOpen = false;
        }
        Log.Write($"menu target={target} class={(target == IntPtr.Zero ? "-" : Native.ClassOf(target))} pick={(pick == null ? "(취소)" : $"{pick.Value.Kind} {pick.Value.Path}")}");
        if (pick is not { } p) return;

        switch (p.Kind)
        {
            case PickKind.Folder:
                OpenSafe(target, p.Path!);
                break;
            case PickKind.File:
                OpenFileSafe(target, p.Path!);
                break;
            case PickKind.AddHere:
                AddFavorite(p.Path!);
                break;
            case PickKind.Settings:
                Native.PostMessage(_hostHandle, WmOpenSettings, IntPtr.Zero, IntPtr.Zero); // 메뉴 루프 종료 후 열기
                break;
        }
    }

    /// <summary>"여기 추가" 대상: 탐색기 활성 탭 / 대화상자 주소 / 바탕화면.</summary>
    private static string? CurrentFolderOf(IntPtr target)
    {
        if (target == IntPtr.Zero || !Native.IsWindow(target)) return null;
        string cls = Native.ClassOf(target);
        if (cls == "CabinetWClass") return ExplorerShell.GetWindowFolder(target);
        if (cls is "Progman" or "WorkerW") return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (FileDialogNavigator.IsFileDialog(target)) return FileDialogNavigator.GetCurrentFolder(target);
        return null;
    }

    private void AddFavorite(string folder)
    {
        string defaultName = Path.GetFileName(folder.TrimEnd('\\'));
        if (string.IsNullOrEmpty(defaultName)) defaultName = folder;
        using var dlg = Native.WithSystemDpi(new AddFavoriteDialog(folder, defaultName));
        Native.ForceForeground(_hostHandle);
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var updated = _settings.Clone();
        updated.Items.Add(new FavoriteItem { Name = dlg.FavoriteName, Path = folder });
        try
        {
            SettingsStore.Save(updated);
            Apply(updated, $"즐겨찾기에 추가했습니다: {dlg.FavoriteName}");
            _settingsForm?.CloseWithoutPrompt(); // 열려 있던 설정 창이 이전 내용으로 덮어쓰지 않게 닫는다
        }
        catch (Exception ex)
        {
            Balloon("즐겨찾기 추가 실패: " + ex.Message, ToolTipIcon.Error);
        }
    }

    private async void OpenFileSafe(IntPtr target, string file)
    {
        try
        {
            if (!File.Exists(file) && !file.StartsWith(@"\\")) return;
            if (target != IntPtr.Zero && Native.IsWindow(target) && FileDialogNavigator.IsFileDialog(target))
            {
                // 대화상자: 파일이 있는 폴더로 이동하고 파일 이름 칸에 이름을 넣는다
                Native.ForceForeground(target);
                if (await FileDialogNavigator.NavigateToFileAsync(target, file))
                {
                    FolderHistory.Add(Path.GetDirectoryName(file)!);
                    return;
                }
            }
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Balloon("파일 열기 실패: " + ex.Message, ToolTipIcon.Error);
        }
    }

    private async void OpenSafe(IntPtr target, string path)
    {
        try
        {
            await OpenAsync(target, path);
        }
        catch (Exception ex)
        {
            Balloon("열기 실패: " + ex.Message, ToolTipIcon.Error);
        }
    }

    private async Task OpenAsync(IntPtr target, string path)
    {
        if (!Directory.Exists(path)) return;
        FolderHistory.Add(path);

        if (target != IntPtr.Zero && Native.IsWindow(target) && FileDialogNavigator.IsFileDialog(target))
        {
            Native.ForceForeground(target);
            if (await FileDialogNavigator.NavigateAsync(target, path)) return;
            Balloon("대화상자를 이동하지 못해 탐색기로 엽니다.", ToolTipIcon.Warning);
        }
        else if (target != IntPtr.Zero && Native.IsWindow(target) && Native.ClassOf(target) == "CabinetWClass")
        {
            // 탐색기 위에서 호출했으면 새 창·새 탭 없이 그 창에서 바로 이동
            if (ExplorerShell.NavigateWindow(target, path)) return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    // ───────── 기타 ─────────

    private void RunInstall()
    {
        var answer = MessageBox.Show(
            $"다음 위치에 설치합니다.\n{Installer.InstallDir}\n\n시작 메뉴 바로가기와 '앱 및 기능' 제거 항목을 등록합니다.\nWindows 시작 시 자동 실행도 설정할까요?",
            AppInfo.Name + " 설치", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (answer == DialogResult.Cancel) return;
        try
        {
            Installer.Install(autostart: answer == DialogResult.Yes);
            var state = AppState.Load(); // 설치 때 이미 물었으므로 설치본 첫 실행에서 다시 묻지 않는다
            state.AutostartAsked = true;
            state.Save();
            Process.Start(new ProcessStartInfo(Installer.InstalledExe, "--wait") { UseShellExecute = false });
            ExitThread(); // 설치된 사본이 이어서 실행된다
        }
        catch (Exception ex)
        {
            MessageBox.Show("설치 실패: " + ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void OpenSettingsFile()
    {
        SettingsStore.EnsureExists();
        try
        {
            Process.Start(new ProcessStartInfo(SettingsStore.FilePath) { UseShellExecute = true });
        }
        catch
        {
            Process.Start("notepad.exe", $"\"{SettingsStore.FilePath}\"");
        }
    }

    private void Balloon(string text, ToolTipIcon icon)
    {
        _tray?.ShowBalloonTip(3000, AppInfo.Name, text, icon);
    }

    internal static Icon LoadFolderIcon()
    {
        try
        {
            string shell32 = Path.Combine(Environment.SystemDirectory, "shell32.dll");
            return Icon.ExtractIcon(shell32, 3, SystemInformation.SmallIconSize.Width) ?? SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    protected override void ExitThreadCore()
    {
        _settingsForm?.CloseWithoutPrompt();
        _dialogWatcher.Dispose();
        _hook?.Dispose();
        Native.UnregisterHotKey(_hostHandle, MenuHotkeyId);
        foreach (var id in _itemHotkeys.Keys) Native.UnregisterHotKey(_hostHandle, id);
        _tray.Visible = false;
        _tray.Dispose();
        _host.DestroyHandle();
        base.ExitThreadCore();
    }

    /// <summary>단축키 수신, 팝업 메뉴 소유, 포그라운드 확보용 숨김 창.</summary>
    private sealed class HostWindow : NativeWindow
    {
        private readonly TrayApp _app;

        public HostWindow(TrayApp app)
        {
            _app = app;
            CreateHandle(new CreateParams
            {
                Caption = HostCaption,
                Style = unchecked((int)0x80000000), // WS_POPUP
                ExStyle = 0x80,                     // WS_EX_TOOLWINDOW
            });
        }

        protected override void WndProc(ref Message m)
        {
            _app.OnMessage(ref m);
            base.WndProc(ref m);
        }
    }
}

internal static class HotkeyParser
{
    public static bool TryParse(string? text, out uint mods, out uint vk)
    {
        mods = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (var raw in text.Split('+'))
        {
            string part = raw.Trim();
            switch (part.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= Native.MOD_CONTROL; break;
                case "alt": mods |= Native.MOD_ALT; break;
                case "shift": mods |= Native.MOD_SHIFT; break;
                case "win": mods |= Native.MOD_WIN; break;
                default:
                    if (vk != 0) return false;
                    Keys key;
                    if (part.Length == 1 && char.IsAsciiDigit(part[0]))
                        key = Keys.D0 + (part[0] - '0');               // "1" → D1 (숫자 키)
                    else if (part.All(char.IsAsciiDigit) || !Enum.TryParse(part, true, out key))
                        return false;                                  // "65" 같은 숫자 값은 거부
                    if (key is Keys.None or Keys.LButton or Keys.RButton or Keys.MButton) return false;
                    vk = (uint)key;
                    break;
            }
        }
        return vk != 0;
    }

    /// <summary>비교용 정규화 문자열("Ctrl+Alt+D" 형식). 파싱 실패 시 null.</summary>
    public static string? Normalize(string? text)
    {
        if (!TryParse(text, out uint mods, out uint vk)) return null;
        return Format(mods, (Keys)vk);
    }

    public static string Format(uint mods, Keys key)
    {
        var parts = new List<string>();
        if ((mods & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mods & Native.MOD_ALT) != 0) parts.Add("Alt");
        if ((mods & Native.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((mods & Native.MOD_WIN) != 0) parts.Add("Win");
        parts.Add(key is >= Keys.D0 and <= Keys.D9 ? ((char)('0' + (key - Keys.D0))).ToString() : key.ToString());
        return string.Join("+", parts);
    }
}
