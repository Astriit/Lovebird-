using System.Diagnostics;

namespace Lovebird;

internal sealed class SettingsForm : Form
{
    public SettingsForm()
    {
        Text = "Lovebird — settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 360);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Font(9f);

        void Label(string text, int y)
        {
            Controls.Add(new Label { Text = text, Location = new Point(24, y), Size = new Size(510, 20),
                ForeColor = Theme.Muted, Font = Theme.Font(8.5f, FontStyle.Bold) });
        }
        TextBox Field(string value, int y)
        {
            var tb = new TextBox { Text = value, Location = new Point(24, y + 20), Size = new Size(512, 30),
                BackColor = Theme.Panel, ForeColor = Theme.Accent, BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true, Font = Theme.Font(8.5f) };
            Controls.Add(tb);
            return tb;
        }

        string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lovebird");
        Label("DATA AND CACHE FOLDER", 22);
        Field(data, 22);
        Label("OPTISCALER PAYLOAD", 86);
        Field(Path.Combine(AppContext.BaseDirectory, "payload"), 86);
        Label("ONLINE GAME BLOCKLIST (one game per line)", 150);
        Field(GameScanner.BlocklistPath, 150);

        var openData = new RoundButton { Text = "OPEN FOLDER", Location = new Point(24, 224), Size = new Size(160, 34) };
        openData.Click += (_, _) => SafeOpen(data);
        var openList = new RoundButton { Text = "OPEN LIST", Location = new Point(196, 224), Size = new Size(160, 34) };
        openList.Click += (_, _) => SafeOpen(GameScanner.BlocklistPath);
        var about = new RoundButton { Text = "ABOUT", Location = new Point(368, 224), Size = new Size(168, 34) };
        about.Click += (_, _) => MessageBox.Show(this,
            "Lovebird 1.0\n\nSingle-player launcher for OptiScaler.\n" +
            "Mode 01 — upscale only.\nMode 02 — upscale + FSR3 frame generation.\n\n" +
            "Colors from my Hyprland/Catppuccin setup (#8dcfe5).\n" +
            "In game: Insert — OptiScaler menu, End — toggle FG.\n\n" +
            "Never use this in games with online anti-cheat.", "Lovebird", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Controls.Add(openData); Controls.Add(openList); Controls.Add(about);

        var close = new RoundButton { Text = "CLOSE", Location = new Point(396, 292), Size = new Size(140, 36),
            BaseColor = Theme.Accent, HoverColor = Color.FromArgb(164, 222, 239), ForeColor2 = Theme.Bg };
        close.Click += (_, _) => Close();
        Controls.Add(close);
    }

    private static void SafeOpen(string path)
    {
        try
        {
            if (!File.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Lovebird", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
