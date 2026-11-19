using Microsoft.Win32;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lovebird;

// Finds single-player games in Steam, Epic, GOG and plain folders.
internal static class GameScanner
{
    private static readonly string[] OnlineTokens =
    {
        "fortnite", "league of legends", "valorant", "apex legends", "pubg", "playerunknown",
        "overwatch", "destiny 2", "warframe", "world of warcraft", "final fantasy xiv", "ffxiv",
        "genshin impact", "honkai", "dota 2", "rainbow six", "call of duty", "battlefield",
        "rocket league", "the finals", "helldivers", "marvel rivals", "escape from tarkov",
        "dayz", "rust", "counter-strike 2", "counter-strike: global offensive", "csgo",
        "team fortress 2", "path of exile", "lost ark", "new world", "runescape", "eve online",
        "star citizen", "albion online", "black desert", "online", "multiplayer"
    };

    private static readonly string[] AntiCheatMarkers =
    {
        "easyanticheat", "battleye", "anticheatexpert", "vanguard", "ricochet",
        "punkbuster", "gameguard", "xigncode3", "nprotect", "aegis"
    };

    private static readonly string[] BadExeNames =
    {
        "unins", "uninstall", "setup", "crash", "launcher", "updater", "patch", "dxdiag",
        "vcredist", "directx", "redist", "dotnet", "activation", "repair", "config",
        "editor", "benchmark", "report", "error", "reg", "cmd", "player", "sdk",
        "chrome", "firefox", "msedge", "discord", "telegram", "spotify", "office",
        "winword", "excel", "powerpnt", "outlook", "photoshop", "premiere", "vlc",
        "qbittorrent", "transmission", "obs64", "steam", "epicgames", "galaxy"
    };

    private static readonly string[] BadPathParts =
    {
        "_commonredist", "redist", "dotnet", "directx", "thirdparty", "support",
        "crash", "plugins", "redistributable", "__pycache__", "docs", "manual"
    };

    private static readonly string[] IgnoredRootDirs =
    {
        "windows", "programdata", "$recycle.bin", "system volume information", "perflogs",
        "recovery", "users", "intel", "nvidia", "amd", "radeon", "msocache", "inetpub",
        "temp", "tmp", "one.driver", "windows.old", "swapfile.sys", "pagefile.sys",
        "program files", "program files (x86)", "games", "steamlibrary", "epic games", "gog galaxy"
    };

    public static string BlocklistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lovebird", "online_blocklist.txt");

    // Manual "add folder" path. Returns null with a short reason for the message box.
    public static GameInfo? FromFolder(string folder, out string error)
    {
        error = "";
        if (!Directory.Exists(folder)) { error = "Folder not found"; return null; }
        string exe = FindBestExe(folder, 2);
        if (string.IsNullOrEmpty(exe)) { error = "No game executable found in that folder"; return null; }
        var game = new GameInfo
        {
            Name = Path.GetFileName(folder.TrimEnd('\\', '/')),
            Source = "Folder",
            InstallDir = folder,
            ExePath = exe
        };
        if (IsOnline(game, LoadBlocklist())) { error = "Looks like an online game — skipping it"; return null; }
        return game;
    }

    public static List<GameInfo> Scan(IProgress<string>? progress = null)
    {
        var games = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);
        var blocked = LoadBlocklist();

        progress?.Report("Steam: reading libraries…");
        Safe(() => ScanSteam(games, blocked), progress);
        progress?.Report("Epic Games: reading manifests…");
        Safe(() => ScanEpic(games, blocked), progress);
        progress?.Report("GOG Galaxy: reading registry…");
        Safe(() => ScanGog(games, blocked), progress);
        progress?.Report("Drives: looking through folders…");
        Safe(() => ScanFolders(games, blocked), progress);

        return games.Values
            .Where(g => !string.IsNullOrWhiteSpace(g.ExePath) && File.Exists(g.ExePath))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Safe(Action action, IProgress<string>? progress)
    {
        try { action(); }
        catch (Exception ex) { progress?.Report($"skipped a source: {ex.Message}"); }
    }

    private static HashSet<string> LoadBlocklist()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(BlocklistPath))
                foreach (string line in File.ReadAllLines(BlocklistPath))
                    if (!string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'))
                        set.Add(Norm(line));
        }
        catch { /* blocklist is optional */ }
        return set;
    }

    private static string Norm(string s) => Regex.Replace(s.ToLowerInvariant(), @"[^\p{L}\p{N}]", "");

    private static void Add(Dictionary<string, GameInfo> games, GameInfo game, HashSet<string> blocked)
    {
        if (string.IsNullOrWhiteSpace(game.Name) || string.IsNullOrWhiteSpace(game.ExePath)) return;
        if (!File.Exists(game.ExePath)) return;
        if (IsOnline(game, blocked)) return;

        game.InstallDir = Path.GetDirectoryName(game.ExePath) ?? game.InstallDir;
        string key = game.InstallDir.TrimEnd('\\', '/');
        if (!games.ContainsKey(key)) games[key] = game;
    }

    private static bool IsOnline(GameInfo game, HashSet<string> blocked)
    {
        string n = Norm(game.Name);
        if (blocked.Contains(n)) return true;
        if (OnlineTokens.Any(t => n.Contains(Norm(t)))) return true;

        try
        {
            var opts = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MaxRecursionDepth = 2,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };
            foreach (string dir in Directory.EnumerateDirectories(game.InstallDir, "*", opts))
            {
                string leaf = Path.GetFileName(dir.TrimEnd('\\', '/')).ToLowerInvariant();
                if (AntiCheatMarkers.Any(m => leaf.Contains(m))) return true;
            }
        }
        catch { /* if we cannot inspect, still allow the game */ }
        return false;
    }

    private static IEnumerable<DriveInfo> FixedDrives()
    {
        try
        {
            return DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).ToList();
        }
        catch { return Array.Empty<DriveInfo>(); }
    }

    private static void ScanSteam(Dictionary<string, GameInfo> games, HashSet<string> blocked)
    {
        string? steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (string.IsNullOrWhiteSpace(steam))
            steam = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
        if (string.IsNullOrWhiteSpace(steam))
            steam = new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam" }
                .FirstOrDefault(Directory.Exists);
        if (string.IsNullOrWhiteSpace(steam) || !Directory.Exists(steam)) return;

        var libs = new List<string> { steam };
        string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            string text = File.ReadAllText(vdf);
            foreach (Match m in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
            {
                string p = m.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(p) && !libs.Any(l => l.Equals(p, StringComparison.OrdinalIgnoreCase))) libs.Add(p);
            }
        }

        foreach (string lib in libs)
        {
            string apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (string acf in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                try
                {
                    string text = File.ReadAllText(acf);
                    string? id = Regex.Match(text, "\"appid\"\\s+\"(\\d+)\"").Groups[1].Value;
                    string name = Regex.Match(text, "\"name\"\\s+\"([^\"]+)\"").Groups[1].Value;
                    string install = Regex.Match(text, "\"installdir\"\\s+\"([^\"]+)\"").Groups[1].Value;
                    string state = Regex.Match(text, "\"StateFlags\"\\s+\"(\\d+)\"").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(install)) continue;
                    if (state == "0") continue; // explicitly not installed
                    string dir = Path.Combine(apps, "common", install);
                    string exe = FindBestExe(dir, 2);
                    if (string.IsNullOrEmpty(exe)) continue;
                    Add(games, new GameInfo
                    {
                        Name = name,
                        Source = "Steam",
                        InstallDir = dir,
                        ExePath = exe,
                        SteamAppId = long.TryParse(id, out long v) ? v : 0
                    }, blocked);
                }
                catch { /* broken manifest - skip */ }
            }
        }
    }

    private static void ScanEpic(Dictionary<string, GameInfo> games, HashSet<string> blocked)
    {
        foreach (DriveInfo drive in FixedDrives())
        {
            string manifests = Path.Combine(drive.Name, "ProgramData", "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(manifests)) continue;
            foreach (string file in Directory.EnumerateFiles(manifests, "*.item"))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
                    JsonElement root = doc.RootElement;
                    string name = root.TryGetProperty("DisplayName", out JsonElement dn) ? dn.GetString() ?? "" : "";
                    string install = root.TryGetProperty("InstallLocation", out JsonElement il) ? il.GetString() ?? "" : "";
                    string launch = root.TryGetProperty("LaunchExecutable", out JsonElement le) ? le.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(name) || !Directory.Exists(install)) continue;

                    string exe = "";
                    if (!string.IsNullOrWhiteSpace(launch))
                    {
                        string candidate = Path.GetFullPath(Path.Combine(install, launch));
                        if (File.Exists(candidate)) exe = candidate;
                    }
                    if (string.IsNullOrEmpty(exe)) exe = FindBestExe(install, 2);
                    if (string.IsNullOrEmpty(exe)) continue;
                    Add(games, new GameInfo { Name = name, Source = "Epic", InstallDir = install, ExePath = exe }, blocked);
                }
                catch { /* bad manifest - skip */ }
            }
        }
    }

    private static void ScanGog(Dictionary<string, GameInfo> games, HashSet<string> blocked)
    {
        string[] views = { @"SOFTWARE\WOW6432Node\GOG.com\Games", @"SOFTWARE\GOG.com\Games" };
        foreach (string view in views)
        {
            using RegistryKey? baseKey = Registry.LocalMachine.OpenSubKey(view) ?? Registry.CurrentUser.OpenSubKey(view);
            if (baseKey == null) continue;
            foreach (string sub in baseKey.GetSubKeyNames())
            {
                try
                {
                    using RegistryKey? key = baseKey.OpenSubKey(sub);
                    if (key == null) continue;
                    string name = key.GetValue("gameName")?.ToString() ?? "";
                    string path = key.GetValue("path")?.ToString() ?? "";
                    string exeRel = key.GetValue("exe")?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(name) || !Directory.Exists(path)) continue;
                    string exe = string.IsNullOrWhiteSpace(exeRel)
                        ? FindBestExe(path, 2)
                        : Path.GetFullPath(Path.Combine(path, exeRel));
                    if (!File.Exists(exe)) continue;
                    Add(games, new GameInfo { Name = name, Source = "GOG", InstallDir = path, ExePath = exe }, blocked);
                }
                catch { /* broken key - skip */ }
            }
        }
    }

    private static void ScanFolders(Dictionary<string, GameInfo> games, HashSet<string> blocked)
    {
        foreach (DriveInfo drive in FixedDrives())
        {
            var roots = new List<string>
            {
                drive.Name,
                Path.Combine(drive.Name, "Games"),
                Path.Combine(drive.Name, "Program Files"),
                Path.Combine(drive.Name, "Program Files (x86)"),
                Path.Combine(drive.Name, "SteamLibrary"),
                Path.Combine(drive.Name, "Epic Games"),
                Path.Combine(drive.Name, "GOG Galaxy")
            };
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (string dir in Directory.EnumerateDirectories(root))
                    {
                        string leaf = Path.GetFileName(dir.TrimEnd('\\', '/'));
                        if (IgnoredRootDirs.Any(x => x.Equals(leaf, StringComparison.OrdinalIgnoreCase))) continue;
                        string exe = FindBestExe(dir, 2);
                        if (string.IsNullOrEmpty(exe)) continue;
                        Add(games, new GameInfo
                        {
                            Name = leaf,
                            Source = "Folder",
                            InstallDir = dir,
                            ExePath = exe
                        }, blocked);
                    }
                }
                catch { /* inaccessible folder - skip */ }
            }
        }
    }

    private static string FindBestExe(string dir, int depth)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return "";
        try
        {
            var opts = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MaxRecursionDepth = depth,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };
            string folder = Path.GetFileName(dir.TrimEnd('\\', '/')).ToLowerInvariant().Replace(" ", "");
            var best = (Path: "", Score: -1);

            foreach (string file in Directory.EnumerateFiles(dir, "*.exe", opts))
            {
                string lower = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                string rel = Path.GetRelativePath(dir, file).ToLowerInvariant().Replace('/', '\\');
                if (BadExeNames.Any(b => lower.Contains(b))) continue;
                if (BadPathParts.Any(b => rel.Contains(b))) continue;

                var fi = new FileInfo(file);
                if (!fi.Exists || fi.Length < 300_000) continue;

                int score = 0;
                if (lower == folder) score += 70;
                else if (folder.Length > 3 && lower.Contains(folder)) score += 40;
                if (lower.Contains("win64-shipping") || lower.Contains("wingdk-shipping")) score += 50;
                else if (lower.Contains("win32-shipping")) score += 30;
                if (rel.Contains("binaries\\win64")) score += 25;
                if (lower.Contains("game") || lower.Contains(".exe")) score += 5;
                score += (int)Math.Min(20, fi.Length / 10_000_000);

                if (score > best.Score) best = (file, score);
            }
            return best.Path;
        }
        catch { return ""; }
    }
}
