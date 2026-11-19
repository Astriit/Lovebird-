using System.Drawing.Drawing2D;
using System.Text.Json;

namespace Lovebird;

internal sealed class MainForm : Form
{
    private GlassPanel _top = null!, _side = null!, _header = null!, _modeRow = null!;
    private GlassPanel _action = null!, _status = null!, _loading = null!;
    private BackdropPanel _content = null!;
    private GlassFlowLayoutPanel _grid = null!;
    private TextBox _search = null!;
    private RoundButton _btnScan = null!, _btnSettings = null!, _btnAdd = null!, _btnRestore = null!, _btnLaunch = null!;
    private WindowButton _btnMin = null!, _btnMax = null!, _btnClose = null!;
    private readonly List<RoundButton> _filters = new();
    private ModeCard _mode1 = null!, _mode2 = null!;
    private IslandBar _island = null!;
    private Label _lblTitle = null!, _lblPath = null!, _lblAction = null!, _lblCount = null!, _lblLoading = null!;
    private Label _lblSideCount = null!;

    private readonly List<GameInfo> _games = new();
    private readonly Dictionary<string, Image> _covers = new();
    private GameInfo? _selected;
    private LaunchMode _mode = LaunchMode.Upscale;
    private string _filter = "ALL";
    private CancellationTokenSource _coverCts = new();
    private readonly SemaphoreSlim _coverGate = new(4);
    private Image? _bg;
    private bool _closing;
    private bool _drag;
    private Point _dragStart;

    private static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lovebird");
    private static string CacheFile => Path.Combine(DataDir, "library.json");

    public MainForm()
    {
        Text = "Lovebird 1.0";
        ClientSize = new Size(1280, 800);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        DoubleBuffered = true;
        MinimumSize = new Size(1050, 680);
        Icon = LoadIcon();

        try
        {
            string bgFile = Path.Combine(AppContext.BaseDirectory, "assets", "bg.jpg");
            if (File.Exists(bgFile)) _bg = Image.FromFile(bgFile);
        }
        catch { /* background is decorative */ }

        BuildUi();
        RollSong();
        Load += async (_, _) => await InitAsync();
        FormClosing += (_, _) => { _closing = true; _coverCts.Cancel(); };
    }

    private static Icon? LoadIcon()
    {
        try
        {
            string ico = Path.Combine(AppContext.BaseDirectory, "assets", "Lovebird.ico");
            if (File.Exists(ico)) return new Icon(ico);
        }
        catch { }
        return null;
    }

    private void BuildUi()
    {
        // ---- bottom status ----
        _status = new GlassPanel { Dock = DockStyle.Bottom, Height = 28, FillColor = Theme.GlassDark };
        _status.Controls.Add(new Label
        {
            Text = "LOVEBIRD 1.0  •  OPTISCALER / MFG  •  SINGLE-PLAYER ONLY",
            Location = new Point(18, 5), Size = new Size(600, 18), ForeColor = Theme.Muted, Font = Theme.Font(8f)
        });
        _lblCount = new Label { Text = "0 GAMES", Location = new Point(1280 - 220, 5), Size = new Size(200, 18),
            ForeColor = Theme.Accent, Font = Theme.Font(8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _status.Controls.Add(_lblCount);
        Controls.Add(_status);

        // ---- top island / title bar ----
        _top = new GlassPanel { Dock = DockStyle.Top, Height = 66, FillColor = Theme.GlassDark };
        _top.Controls.Add(new Label { Text = "LOVEBIRD", Location = new Point(20, 15), Size = new Size(180, 36),
            ForeColor = Theme.Text, Font = Theme.Font(14f, FontStyle.Bold) });
        _island = new IslandBar { Location = new Point(405, 15), Size = new Size(470, 36) };
        _island.Reroll += (_, _) => RollSong();
        _top.Controls.Add(_island);

        _btnMin = new WindowButton { Kind = WindowButton.ButtonKind.Minimize,
            Location = new Point(1280 - 126, 17), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _btnMin.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _btnMax = new WindowButton { Kind = WindowButton.ButtonKind.Maximize,
            Location = new Point(1280 - 84, 17), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _btnMax.Click += (_, _) => WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal : FormWindowState.Maximized;
        _btnClose = new WindowButton { Kind = WindowButton.ButtonKind.Close,
            Location = new Point(1280 - 42, 17), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _btnClose.Click += (_, _) => Close();
        _top.Controls.Add(_btnMin); _top.Controls.Add(_btnMax); _top.Controls.Add(_btnClose);
        _top.MouseDown += TopMouseDown;
        _top.MouseMove += TopMouseMove;
        _top.MouseUp += (_, _) => _drag = false;
        Controls.Add(_top);

        // ---- sidebar ----
        _side = new GlassPanel { Dock = DockStyle.Left, Width = 232, FillColor = Theme.Glass };
        _side.Controls.Add(new Label { Text = "LIBRARY", Location = new Point(20, 18), Size = new Size(192, 22),
            ForeColor = Theme.Text, Font = Theme.Font(10f, FontStyle.Bold) });
        _search = new TextBox { Location = new Point(20, 50), Size = new Size(192, 32), BackColor = Theme.PanelHi,
            ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(9f), Text = "search…" };
        _search.TextChanged += (_, _) => ApplyFilter();
        _search.Enter += (_, _) => { if (_search.Text == "search…") _search.Text = ""; };
        _search.Leave += (_, _) => { if (_search.Text.Length == 0) _search.Text = "search…"; };
        _side.Controls.Add(_search);

        string[] filterNames = { "ALL", "STEAM", "EPIC", "GOG", "FOLDER" };
        for (int i = 0; i < filterNames.Length; i++)
        {
            string name = filterNames[i];
            var btn = new RoundButton { Text = name, Location = new Point(20, 100 + i * 42), Size = new Size(192, 34) };
            btn.Click += (_, _) =>
            {
                _filter = name;
                foreach (RoundButton b in _filters) b.BaseColor = b.Text == name ? Theme.Accent : Theme.PanelHi;
                ApplyFilter();
            };
            _filters.Add(btn);
            _side.Controls.Add(btn);
        }
        _filters[0].BaseColor = Theme.Accent;

        _side.Controls.Add(new Label { Text = "GAMES FOUND", Location = new Point(20, 328), Size = new Size(192, 18),
            ForeColor = Theme.Muted, Font = Theme.Font(8f) });
        _lblSideCount = new Label { Text = "0", Location = new Point(20, 348), Size = new Size(192, 44),
            ForeColor = Theme.Accent, Font = Theme.Font(28f, FontStyle.Bold) };
        _side.Controls.Add(_lblSideCount);

        _btnScan = new RoundButton { Text = "↻  RESCAN", Location = new Point(20, 590),
            Size = new Size(192, 36), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        _btnScan.Click += async (_, _) => await RefreshAsync();
        _btnAdd = new RoundButton { Text = "+  ADD FOLDER", Location = new Point(20, 634),
            Size = new Size(192, 36), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        _btnAdd.Click += AddFolder;
        _btnSettings = new RoundButton { Text = "⚙  SETTINGS", Location = new Point(20, 678),
            Size = new Size(192, 36), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        _btnSettings.Click += (_, _) => new SettingsForm().ShowDialog(this);
        _side.Controls.Add(_btnScan); _side.Controls.Add(_btnAdd); _side.Controls.Add(_btnSettings);
        Controls.Add(_side);

        // ---- main content ----
        _content = new BackdropPanel { Dock = DockStyle.Fill, Backdrop = _bg };

        _header = new GlassPanel { Location = new Point(0, 0), Size = new Size(1048, 84),
            FillColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _lblTitle = new Label { Text = "Select a game", Location = new Point(24, 12), Size = new Size(700, 30),
            ForeColor = Theme.Text, Font = Theme.Font(16f, FontStyle.Bold) };
        _lblPath = new Label { Text = "Loading the single-player library…", Location = new Point(24, 46),
            Size = new Size(700, 22), ForeColor = Theme.Muted, Font = Theme.Font(8.5f) };
        _btnRestore = new RoundButton { Text = "RESTORE", Location = new Point(1048 - 170, 24),
            Size = new Size(146, 36), Visible = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _btnRestore.Click += RestoreSelected;
        _header.Controls.Add(_lblTitle); _header.Controls.Add(_lblPath); _header.Controls.Add(_btnRestore);

        _modeRow = new GlassPanel { Location = new Point(0, 84), Size = new Size(1048, 104),
            FillColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _modeRow.Controls.Add(new Label { Text = "WORK MODE", Location = new Point(24, 8),
            Size = new Size(300, 20), ForeColor = Theme.Muted, Font = Theme.Font(8f, FontStyle.Bold) });
        _mode1 = new ModeCard { Location = new Point(24, 32), Size = new Size(300, 62),
            Number = "01", Title = "UPSCALE ONLY", Subtitle = "no frame generation", Selected = true };
        _mode2 = new ModeCard { Location = new Point(336, 32), Size = new Size(300, 62),
            Number = "02", Title = "UPSCALE + FG", Subtitle = "FSR3 frame generation" };
        _mode1.Click += (_, _) => SetMode(LaunchMode.Upscale);
        _mode2.Click += (_, _) => SetMode(LaunchMode.UpscaleFg);
        _modeRow.Controls.Add(_mode1); _modeRow.Controls.Add(_mode2);
        _modeRow.Controls.Add(new Label { Text = "In game: Insert — OptiScaler menu   •   End — toggle FG",
            Location = new Point(660, 52), Size = new Size(380, 24), ForeColor = Theme.Muted, Font = Theme.Font(8f),
            Anchor = AnchorStyles.Top | AnchorStyles.Right });

        _grid = new GlassFlowLayoutPanel { Location = new Point(0, 192), Size = new Size(1048, 500),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AutoScroll = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(18, 10, 18, 10) };

        _action = new GlassPanel { Location = new Point(0, 700), Size = new Size(1048, 78),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            FillColor = Theme.GlassDark };
        _btnLaunch = new RoundButton { Text = "LAUNCH  →", Location = new Point(24, 18), Size = new Size(240, 42),
            BaseColor = Theme.Accent, HoverColor = Theme.AccentHover, ForeColor2 = Theme.Bg,
            Font = Theme.Font(11f, FontStyle.Bold), Enabled = false };
        _btnLaunch.Click += LaunchSelected;
        _lblAction = new Label { Text = "Pick a game from the library", Location = new Point(284, 18),
            Size = new Size(740, 42), ForeColor = Theme.Muted, Font = Theme.Font(9f) };
        _action.Controls.Add(_btnLaunch); _action.Controls.Add(_lblAction);

        _content.Controls.Add(_header); _content.Controls.Add(_modeRow); _content.Controls.Add(_grid); _content.Controls.Add(_action);
        Controls.Add(_content);

        // ---- scanning overlay ----
        _loading = new GlassPanel { Dock = DockStyle.Fill, FillColor = Theme.GlassDark, Visible = false };
        _lblLoading = new Label { Text = "Scanning drives…", Location = new Point(0, 370), Size = new Size(1280, 40),
            ForeColor = Theme.Accent, Font = Theme.Font(12f), TextAlign = ContentAlignment.MiddleCenter };
        _loading.Controls.Add(_lblLoading);
        Controls.Add(_loading);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_island != null && _top != null && _btnClose != null)
        {
            int left = Math.Max(215, (_top.ClientSize.Width - _island.Width) / 2);
            int limit = _btnClose.Left - 14 - _island.Width;
            _island.Left = Math.Min(left, Math.Max(215, limit));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_bg != null) e.Graphics.DrawImage(_bg, new Rectangle(0, 0, Width, Height));
        using var veil = new SolidBrush(Color.FromArgb(205, 18, 18, 18));
        e.Graphics.FillRectangle(veil, ClientRectangle);
        e.Graphics.FillRectangle(new SolidBrush(Theme.Accent), 0, 0, Width, 2);
    }

    protected override void WndProc(ref Message m)
    {
        // Drag the borderless window by its top strip (but not by the window buttons).
        if (m.Msg == 0x0201 && _top != null) // WM_LBUTTONDOWN
        {
            Point p = PointToClient(MousePosition);
            if (p.Y < 66)
            {
                Control? child = GetChildAtPoint(p);
                if (child is not Button && child is not RoundButton)
                {
                    m.Msg = 0x00A1; // WM_NCLBUTTONDOWN
                    m.WParam = (IntPtr)0x02; // HTCAPTION
                }
            }
        }
        base.WndProc(ref m);
    }

    private void TopMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _drag = true; _dragStart = e.Location; }
    }

    private void TopMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_drag) return;
        Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
    }

    private void RollSong()
    {
        var song = Songs.RandomTrack();
        _island.Song = song.Title;
        _island.Line = song.Line;
        _island.Invalidate();
    }

    private async Task InitAsync()
    {
        Directory.CreateDirectory(DataDir);
        if (File.Exists(CacheFile))
        {
            try
            {
                var cache = JsonSerializer.Deserialize<LibraryCache>(File.ReadAllText(CacheFile));
                if (cache?.Games != null)
                {
                    _games.AddRange(cache.Games);
                    ApplyFilter();
                    _lblPath.Text = "Cache loaded — refreshing the list…";
                }
            }
            catch { /* bad cache - rescan */ }
        }
        await RefreshAsync();
        await CheckUpdatesAsync();
    }

    private async Task CheckUpdatesAsync()
    {
        try
        {
            UpdateInfo? info = await Updater.CheckAsync(CancellationToken.None);
            if (info == null) return;

            string ask = $"A new version is available: {info.Tag}\n\nDownload and install it now?";
            if (MessageBox.Show(this, ask, "Lovebird — update",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _btnLaunch.Enabled = false;
            var progress = new Progress<string>(s => Ui(() => _lblAction.Text = s));
            try
            {
                await Task.Run(() => Updater.DownloadAndInstallAsync(info, progress, CancellationToken.None));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not install the update: " + ex.Message,
                    "Lovebird — update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Close();
        }
        catch { /* no internet, or GitHub is down - just keep using the current build */ }
    }

    private async Task RefreshAsync()
    {
        _btnScan.Enabled = false;
        _loading.Visible = true;
        _lblLoading.Text = "Scanning drives…";
        _loading.BringToFront();
        Application.DoEvents();

        var progress = new Progress<string>(s => Ui(() => _lblLoading.Text = s));
        try
        {
            List<GameInfo> found = await Task.Run(() => GameScanner.Scan(progress));
            _games.Clear();
            _games.AddRange(found);
            try { File.WriteAllText(CacheFile, JsonSerializer.Serialize(new LibraryCache
            {
                ScannedAt = DateTime.Now, Games = _games
            }, new JsonSerializerOptions { WriteIndented = true })); }
            catch { /* cache is optional */ }
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _lblPath.Text = "Scan failed: " + ex.Message;
        }
        finally
        {
            _loading.Visible = false;
            _btnScan.Enabled = true;
        }
    }

    private void ApplyFilter()
    {
        _coverCts.Cancel();
        _coverCts = new CancellationTokenSource();
        string q = _search.Text.Trim().ToLowerInvariant();
        if (q == "search…") q = "";
        string sourceFilter = _filter == "FOLDER" ? "Folder" : _filter;

        var shown = _games.Where(g =>
            (_filter == "ALL" || g.Source.Equals(sourceFilter, StringComparison.OrdinalIgnoreCase)) &&
            (q.Length == 0 || g.Name.ToLowerInvariant().Contains(q) || g.InstallDir.ToLowerInvariant().Contains(q)))
            .ToList();

        _grid.Controls.Clear();
        foreach (GameInfo game in shown)
        {
            var card = new GameCard
            {
                Game = game,
                Cover = _covers.TryGetValue(game.Key, out Image? img) ? img : null,
                Status = Upscaler.State(game),
                Margin = new Padding(10, 10, 10, 10)
            };
            card.Click += (_, _) => SelectGame(game);
            card.DoubleClick += (_, _) => { SelectGame(game); LaunchSelected(null, EventArgs.Empty); };
            _grid.Controls.Add(card);
            if (card.Cover == null) _ = LoadCoverAsync(game, card, _coverCts.Token);
        }
        _lblCount.Text = $"{shown.Count} SHOWN • {_games.Count} TOTAL";
        _lblSideCount.Text = shown.Count.ToString();
        if (_selected != null)
        {
            GameInfo? still = shown.FirstOrDefault(g => g.Key == _selected.Key);
            if (still == null) { _selected = null; _btnLaunch.Enabled = false; }
        }
    }

    private async Task LoadCoverAsync(GameInfo game, GameCard card, CancellationToken ct)
    {
        try
        {
            await _coverGate.WaitAsync(ct);
            Image img;
            try { img = await CoverService.GetAsync(game, ct); }
            finally { _coverGate.Release(); }
            if (_closing || ct.IsCancellationRequested) { img.Dispose(); return; }
            lock (_covers) _covers[game.Key] = img;
            Ui(() =>
            {
                if (card.IsDisposed || _closing || card.Game.Key != game.Key) return;
                card.Cover = img;
                card.Invalidate();
            });
        }
        catch { /* network problems never break the UI */ }
    }

    private void SelectGame(GameInfo game)
    {
        _selected = game;
        foreach (GameCard card in _grid.Controls.OfType<GameCard>())
        {
            card.Selected = card.Game.Key == game.Key;
            card.Invalidate();
        }
        _lblTitle.Text = game.Name;
        _lblPath.Text = game.ExePath;
        string state = Upscaler.State(game);
        _btnRestore.Visible = state.Length > 0;
        _lblAction.Text = state.Length > 0
            ? $"{game.Source} • OptiScaler: {state} • mode {(_mode == LaunchMode.Upscale ? "01 UPSCALE" : "02 + FG")}"
            : $"{game.Source} • clean • mode {(_mode == LaunchMode.Upscale ? "01 UPSCALE" : "02 + FG")}";
        _btnLaunch.Enabled = true;
    }

    private void SetMode(LaunchMode mode)
    {
        _mode = mode;
        _mode1.Selected = mode == LaunchMode.Upscale;
        _mode2.Selected = mode == LaunchMode.UpscaleFg;
        _mode1.Invalidate(); _mode2.Invalidate();
        if (_selected != null) SelectGame(_selected);
    }

    private async void LaunchSelected(object? sender, EventArgs e)
    {
        if (_selected == null) return;
        GameInfo game = _selected;
        _btnLaunch.Enabled = false;
        var progress = new Progress<string>(s => Ui(() => _lblAction.Text = s));
        try
        {
            await Task.Run(() => Upscaler.Install(game, _mode, progress));
            Upscaler.Launch(game);
            _lblAction.Text = $"LAUNCHED: {game.Name} • {(_mode == LaunchMode.Upscale ? "01" : "02 + FG")}";
        }
        catch (Exception ex)
        {
            _lblAction.Text = "ERROR: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Lovebird — launch cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!_closing) { _btnLaunch.Enabled = true; SelectGame(game); }
        }
    }

    private async void RestoreSelected(object? sender, EventArgs e)
    {
        if (_selected == null) return;
        GameInfo game = _selected;
        if (MessageBox.Show(this, $"Remove the OptiScaler files from '{game.Name}' and restore the ini?",
                "Lovebird — restore", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _btnRestore.Enabled = false;
        var progress = new Progress<string>(s => Ui(() => _lblAction.Text = s));
        try { await Task.Run(() => Upscaler.Restore(game, progress)); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Lovebird — restore", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { if (!_closing) { _btnRestore.Enabled = true; SelectGame(game); } }
    }

    private void AddFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "Pick the game folder" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        GameInfo? game = GameScanner.FromFolder(dialog.SelectedPath, out string error);
        if (game == null)
        {
            MessageBox.Show(this, error, "Lovebird", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_games.Any(g => g.InstallDir.Equals(game.InstallDir, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "That game is already in the library", "Lovebird", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _games.Add(game);
        try { File.WriteAllText(CacheFile, JsonSerializer.Serialize(new LibraryCache
        { ScannedAt = DateTime.Now, Games = _games }, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
        ApplyFilter();
        SelectGame(game);
    }

    private void Ui(Action action)
    {
        if (_closing || IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); }
        catch { /* form is closing */ }
    }
}
