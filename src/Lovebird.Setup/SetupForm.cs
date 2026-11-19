using Lovebird;
using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Readers;
using System.Diagnostics;
using System.Reflection;

namespace Lovebird.Setup;

internal sealed class SetupForm : Form
{
    private Panel _welcome = null!, _pathPage = null!, _progressPage = null!, _donePage = null!, _chrome = null!;
    private TextBox _dest = null!, _log = null!;
    private ComboBox _drives = null!;
    private Label _driveInfo = null!, _progressLabel = null!, _doneLabel = null!;
    private ProgressBar _bar = null!;
    private RoundButton _next = null!, _install = null!, _launch = null!;
    private string _installedTo = "";
    private bool _drag;
    private Point _dragStart;

    public SetupForm()
    {
        Text = "Lovebird 1.0 — setup";
        ClientSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Bg;
        Font = Theme.Font(9f);
        DoubleBuffered = true;

        try
        {
            string ico = Path.Combine(AppContext.BaseDirectory, "assets", "Lovebird.ico");
            if (File.Exists(ico)) Icon = new Icon(ico);
        }
        catch { }

        BuildChrome();
        BuildWelcome();
        BuildPath();
        BuildProgress();
        BuildDone();
        Controls.Add(_donePage); Controls.Add(_progressPage); Controls.Add(_pathPage); Controls.Add(_welcome);
        Controls.Add(_chrome);
        ShowPage(_welcome);
    }

    private void BuildChrome()
    {
        _chrome = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(13, 14, 17) };
        _chrome.Controls.Add(new Label { Text = "♥  LOVEBIRD 1.0  /  SETUP", Location = new Point(20, 10),
            Size = new Size(500, 24), ForeColor = Theme.Accent, Font = Theme.Font(11f, FontStyle.Bold) });
        var close = new RoundButton { Text = "×", Location = new Point(900 - 42, 8), Size = new Size(32, 28), Radius = 7 };
        close.Click += (_, _) => Close();
        _chrome.Controls.Add(close);
        _chrome.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _drag = true; _dragStart = e.Location; } };
        _chrome.MouseMove += (_, e) => { if (_drag) Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y); };
        _chrome.MouseUp += (_, _) => _drag = false;
    }

    private void BuildWelcome()
    {
        _welcome = Page();
        string iconFile = Path.Combine(AppContext.BaseDirectory, "assets", "bird_icon.png");
        if (File.Exists(iconFile))
            _welcome.Controls.Add(new PictureBox { Image = Image.FromFile(iconFile), Location = new Point(382, 78),
                Size = new Size(136, 136), SizeMode = PictureBoxSizeMode.Zoom });

        _welcome.Controls.Add(new Label { Text = "LOVEBIRD 1.0", Location = new Point(0, 232), Size = new Size(900, 42),
            ForeColor = Theme.Text, Font = Theme.Font(24f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
        _welcome.Controls.Add(new Label { Text = "single-player game launcher + OptiScaler", Location = new Point(0, 280),
            Size = new Size(900, 30), ForeColor = Theme.Accent, Font = Theme.Font(11f), TextAlign = ContentAlignment.MiddleCenter });

        DriveInfo source = SourceDrive();
        _driveInfo = new Label
        {
            Text = $"Setup was started from {source.Name}  •  {Gb(source.AvailableFreeSpace)} GB free\n\n" +
                   "This adds shortcuts, an entry in Programs and Features, and the full OptiScaler payload.\n" +
                   "Your games are not touched until you hit Launch in the app.",
            Location = new Point(170, 340), Size = new Size(560, 90), ForeColor = Theme.Muted,
            Font = Theme.Font(9f), TextAlign = ContentAlignment.MiddleCenter
        };
        _welcome.Controls.Add(_driveInfo);

        _next = new RoundButton { Text = "NEXT  →", Location = new Point(350, 470), Size = new Size(200, 42),
            BaseColor = Theme.Accent, HoverColor = Color.FromArgb(164, 222, 239), ForeColor2 = Theme.Bg,
            Font = Theme.Font(10f, FontStyle.Bold) };
        _next.Click += (_, _) => ShowPage(_pathPage);
        _welcome.Controls.Add(_next);
    }

    private void BuildPath()
    {
        _pathPage = Page();
        _pathPage.Controls.Add(new Label { Text = "WHERE TO INSTALL", Location = new Point(40, 70),
            Size = new Size(500, 26), ForeColor = Theme.Text, Font = Theme.Font(14f, FontStyle.Bold) });

        _drives = new ComboBox { Location = new Point(40, 120), Size = new Size(820, 32),
            BackColor = Theme.PanelHi, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat, Font = Theme.Font(9f),
            DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (DriveInfo d in FixedDrives().OrderByDescending(d => d.AvailableFreeSpace))
            _drives.Items.Add($"{d.Name}  —  {Gb(d.AvailableFreeSpace)} GB free");
        if (_drives.Items.Count > 0) _drives.SelectedIndex = 0;
        _drives.SelectedIndexChanged += (_, _) =>
        {
            string root = _drives.SelectedItem?.ToString()?[..3] ?? @"C:\";
            _dest.Text = Path.Combine(root, "Games", "Lovebird");
            _driveInfo.Text = "Drive picked. Any destination works — the folder will be created if needed.";
        };
        _pathPage.Controls.Add(_drives);

        _dest = new TextBox { Location = new Point(40, 172), Size = new Size(700, 34),
            BackColor = Theme.PanelHi, ForeColor = Theme.Accent, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(9f) };
        _pathPage.Controls.Add(_dest);
        var browse = new RoundButton { Text = "BROWSE", Location = new Point(752, 172), Size = new Size(108, 34) };
        browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Lovebird install folder" };
            if (dlg.ShowDialog(this) == DialogResult.OK) _dest.Text = dlg.SelectedPath;
        };
        _pathPage.Controls.Add(browse);

        _driveInfo = new Label { Text = "Drive picked. Any destination works — the folder will be created if needed.",
            Location = new Point(40, 218), Size = new Size(820, 30), ForeColor = Theme.Muted, Font = Theme.Font(8.5f) };
        _pathPage.Controls.Add(_driveInfo);

        var back = new RoundButton { Text = "← BACK", Location = new Point(40, 500), Size = new Size(160, 38) };
        back.Click += (_, _) => ShowPage(_welcome);
        _install = new RoundButton { Text = "INSTALL  →", Location = new Point(640, 500), Size = new Size(220, 38),
            BaseColor = Theme.Accent, HoverColor = Color.FromArgb(164, 222, 239), ForeColor2 = Theme.Bg,
            Font = Theme.Font(10f, FontStyle.Bold) };
        _install.Click += async (_, _) => await InstallAsync();
        _pathPage.Controls.Add(back); _pathPage.Controls.Add(_install);
    }

    private void BuildProgress()
    {
        _progressPage = Page();
        _progressPage.Controls.Add(new Label { Text = "INSTALLING", Location = new Point(40, 70),
            Size = new Size(500, 26), ForeColor = Theme.Text, Font = Theme.Font(14f, FontStyle.Bold) });
        _progressLabel = new Label { Text = "Getting things ready…", Location = new Point(40, 112),
            Size = new Size(820, 24), ForeColor = Theme.Accent, Font = Theme.Font(9f) };
        _progressPage.Controls.Add(_progressLabel);
        _bar = new ProgressBar { Location = new Point(40, 144), Size = new Size(820, 18),
            ForeColor = Theme.Accent, BackColor = Theme.PanelHi, Style = ProgressBarStyle.Continuous };
        _progressPage.Controls.Add(_bar);
        _log = new TextBox { Location = new Point(40, 184), Size = new Size(820, 300), Multiline = true,
            ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Theme.Panel, ForeColor = Theme.Muted,
            BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(8.5f) };
        _progressPage.Controls.Add(_log);
    }

    private void BuildDone()
    {
        _donePage = Page();
        _donePage.Controls.Add(new Label { Text = "✓  ALL DONE", Location = new Point(0, 100), Size = new Size(900, 50),
            ForeColor = Theme.Good, Font = Theme.Font(22f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
        _doneLabel = new Label { Text = "", Location = new Point(120, 170), Size = new Size(660, 90),
            ForeColor = Theme.Muted, Font = Theme.Font(9f), TextAlign = ContentAlignment.MiddleCenter };
        _donePage.Controls.Add(_doneLabel);
        _launch = new RoundButton { Text = "LAUNCH LOVEBIRD", Location = new Point(220, 320), Size = new Size(230, 42),
            BaseColor = Theme.Accent, HoverColor = Color.FromArgb(164, 222, 239), ForeColor2 = Theme.Bg,
            Font = Theme.Font(10f, FontStyle.Bold) };
        _launch.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(Path.Combine(_installedTo, "Lovebird.exe")) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Lovebird", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            Close();
        };
        var finish = new RoundButton { Text = "CLOSE", Location = new Point(470, 320), Size = new Size(210, 42) };
        finish.Click += (_, _) => Close();
        _donePage.Controls.Add(_launch); _donePage.Controls.Add(finish);
    }

    private Panel Page() => new() { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };

    private void ShowPage(Panel page)
    {
        foreach (Panel p in new[] { _welcome, _pathPage, _progressPage, _donePage }) p.Visible = p == page;
        page.BringToFront();
    }

    private async Task InstallAsync()
    {
        string dest = _dest.Text.Trim().TrimEnd('\\', '/');
        if (dest.Length == 2 && dest[1] == ':') dest += '\\'; // C: -> C:\
        if (dest.Length < 4) { MessageBox.Show(this, "Pick an install folder", "Lovebird", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (!Path.IsPathRooted(dest)) { MessageBox.Show(this, "Use a full path (for example D:\\Games\\Lovebird)", "Lovebird", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        try
        {
            Directory.CreateDirectory(dest);
            string marker = Path.Combine(dest, "lovebird.installed");
            if (Directory.EnumerateFileSystemEntries(dest).Any() && !File.Exists(marker))
                throw new InvalidOperationException("That folder is not empty. Pick another one — I don't want to touch other files.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Lovebird — setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ShowPage(_progressPage);
        _bar.Value = 0;
        _log.Clear();
        var progress = new Progress<(int Pct, string Text)>(p =>
        {
            _bar.Value = Math.Max(0, Math.Min(100, p.Pct));
            _progressLabel.Text = p.Text;
            _log.AppendText(p.Text + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        });

        try
        {
            await Task.Run(() => Extract(dest, progress));
            string exe = Path.Combine(dest, "Lovebird.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("Lovebird.exe was not extracted", exe);
            File.WriteAllText(Path.Combine(dest, "lovebird.installed"), $"Lovebird 1.0\n{DateTime.Now:O}\n");
            RegisterUninstall(dest, exe);
            string icon = Path.Combine(dest, "assets", "Lovebird.ico");
            try
            {
                Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Lovebird 1.0.lnk"), exe, icon, dest);
                Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Lovebird 1.0.lnk"), exe, icon, dest);
            }
            catch { /* shortcuts are nice to have, not critical */ }

            _installedTo = dest;
            _doneLabel.Text = $"Lovebird 1.0 installed to:\n{dest}\n\nShortcuts are in place. Remove it from Programs and Features when needed.";
            ShowPage(_donePage);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Lovebird — install error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ShowPage(_pathPage);
        }
    }

    private static void Extract(string dest, IProgress<(int, string)> progress)
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        string name = asm.GetManifestResourceNames().First(n => n.EndsWith("payload.7z", StringComparison.OrdinalIgnoreCase));
        using Stream resource = asm.GetManifestResourceStream(name)
            ?? throw new InvalidDataException("The installer has no payload inside");
        using IArchive archive = ArchiveFactory.OpenArchive(resource, new ReaderOptions());
        List<IArchiveEntry> entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        long total = Math.Max(1, entries.Sum(e => e.Size));
        long done = 0;
        int index = 0;

        foreach (IArchiveEntry entry in entries)
        {
            string rel = (entry.Key ?? "").Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (rel.Split(Path.DirectorySeparatorChar).Any(p => p == ".."))
                throw new InvalidDataException("Bad path in the archive: " + rel);
            string target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using (Stream src = entry.OpenEntryStream())
            using (Stream outStream = File.Create(target))
                src.CopyTo(outStream, 1024 * 256);
            done += entry.Size;
            index++;
            progress.Report(((int)(done * 100 / total), $"{index}/{entries.Count}  •  {rel}"));
        }
        progress.Report((100, "Extraction finished"));
    }

    private static void RegisterUninstall(string dir, string exe)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Lovebird 1.0");
        key.SetValue("DisplayName", "Lovebird 1.0");
        key.SetValue("DisplayVersion", "1.0");
        key.SetValue("Publisher", "Lovebird");
        key.SetValue("InstallLocation", dir);
        key.SetValue("UninstallString", $"\"{exe}\" --uninstall");
        key.SetValue("DisplayIcon", Path.Combine(dir, "assets", "Lovebird.ico"));
        key.SetValue("NoModify", 1);
        key.SetValue("NoRepair", 1);
    }

    private static void Shortcut(string path, string target, string icon, string workDir)
    {
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return;
        object shell = Activator.CreateInstance(shellType)!;
        object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path })!;
        shellType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
        shellType.InvokeMember("Arguments", BindingFlags.SetProperty, null, link, new object[] { "" });
        shellType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { workDir });
        shellType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { icon + ",0" });
        shellType.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "Lovebird 1.0" });
        shellType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, Array.Empty<object>());
    }

    private static DriveInfo SourceDrive()
    {
        string? root = Path.GetPathRoot(AppContext.BaseDirectory);
        DriveInfo? found = DriveInfo.GetDrives().FirstOrDefault(d => d.Name.Equals(root, StringComparison.OrdinalIgnoreCase) && d.IsReady);
        return found ?? DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed)
               ?? new DriveInfo(@"C:\");
    }

    private static IEnumerable<DriveInfo> FixedDrives() =>
        DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

    private static string Gb(long bytes) => (bytes / 1024.0 / 1024.0 / 1024.0).ToString("F1");
}
