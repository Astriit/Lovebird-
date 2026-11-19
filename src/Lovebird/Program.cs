using System.Diagnostics;

namespace Lovebird;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        if (args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            RunUninstall();
            return;
        }
        Application.Run(new MainForm());
    }

    // Uninstall only touches Lovebird, never the games.
    private static void RunUninstall()
    {
        if (MessageBox.Show("Remove Lovebird 1.0 from this PC?\nGames and their files will not be touched.",
                "Lovebird — uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        string dir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string start = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        string bat = Path.Combine(Path.GetTempPath(), "lovebird_uninstall.bat");
        int pid = Environment.ProcessId;

        string script = "@echo off\n" +
            ":wait\n" +
            $"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul\n" +
            "if errorlevel 1 goto go\n" +
            "ping -n 2 127.0.0.1 >nul\n" +
            "goto wait\n" +
            ":go\n" +
            $"rmdir /s /q \"{dir}\"\n" +
            $"del \"{Path.Combine(start, "Lovebird 1.0.lnk")}\" 2>nul\n" +
            $"del \"{Path.Combine(desktop, "Lovebird 1.0.lnk")}\" 2>nul\n" +
            "reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Lovebird 1.0\" /f >nul 2>&1\n" +
            "del \"%~f0\"\n";
        try
        {
            File.WriteAllText(bat, script);
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c start \"\" \"{bat}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not start the uninstall: " + ex.Message, "Lovebird",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
