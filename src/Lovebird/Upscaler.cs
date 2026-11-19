using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Lovebird;

// Copies OptiScaler into a game folder only when we can prove the file is ours.
// Every write goes into a manifest so restore can undo just those files.
internal static class Upscaler
{
    private const string ManifestName = ".lovebird_install.json";
    private const string OriginalIniBackup = "OptiScaler.ini.lovebird-original.bak";
    private const string PrevIniBackup = "OptiScaler.ini.lovebird-prev.bak";

    private static readonly string[] InjectionNames =
        { "dxgi.dll", "winmm.dll", "version.dll", "dbghelp.dll", "d3d12.dll", "wininet.dll", "winhttp.dll" };

    private static readonly string[] SupportFiles =
    {
        "amd_fidelityfx_dx12.dll", "amd_fidelityfx_framegeneration_dx12.dll",
        "amd_fidelityfx_upscaler_dx12.dll", "amd_fidelityfx_vk.dll",
        "dlssg_to_fsr3_amd_is_better.dll", "fakenvapi.dll", "fakenvapi.ini",
        "libxell.dll", "libxess.dll", "libxess_dx11.dll", "libxess_fg.dll"
    };

    private static readonly string[] SupportDirs = { "D3D12_Optiscaler", "Licenses" };

    // Two modes, matching the presets in profile.cfg.
    private const string Profile =
        "; Lovebird 1.0 — two presets (RTX 2070 Super / 2K)\n" +
        "[UPSCALE]\n" +
        "Dxgi = false\n" +
        "Dx12Upscaler = auto\n" +
        "FGInput = nofg\n" +
        "\n" +
        "[UPSCALE_FG]\n" +
        "Dxgi = false\n" +
        "Dx12Upscaler = auto\n" +
        "FGInput = upscaler\n" +
        "FGOutput = fsrfg\n";

    private static string PayloadRoot => Path.Combine(AppContext.BaseDirectory, "payload");
    private static string? _payloadOptiHash;

    public static string PresetName(LaunchMode mode) => mode == LaunchMode.Upscale ? "UPSCALE" : "UPSCALE_FG";

    private static string PayloadHash()
    {
        string file = Path.Combine(PayloadRoot, "OptiScaler.dll");
        if (!File.Exists(file)) throw new FileNotFoundException("OptiScaler.dll is missing from the payload", file);
        return _payloadOptiHash ??= Hash(file);
    }

    public static string State(GameInfo game)
    {
        string dir = Path.GetDirectoryName(game.ExePath) ?? game.InstallDir;
        string manifestPath = Path.Combine(dir, ManifestName);
        try
        {
            if (File.Exists(manifestPath))
            {
                var m = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath));
                if (m != null)
                    return m.Mode == "UPSCALE_FG" ? "02 • FG" : "01 • UP";
            }
            string ini = Path.Combine(dir, "OptiScaler.ini");
            if (File.Exists(ini)) return "external";
        }
        catch { /* unreadable manifest - show as clean */ }
        return "";
    }

    public static void Install(GameInfo game, LaunchMode mode, IProgress<string> log)
    {
        string dir = Path.GetDirectoryName(game.ExePath) ?? game.InstallDir;
        if (!Directory.Exists(dir) || !File.Exists(game.ExePath))
            throw new DirectoryNotFoundException("Game folder not found: " + dir);
        if (!Directory.Exists(PayloadRoot))
            throw new DirectoryNotFoundException("Lovebird payload not found: " + PayloadRoot);

        string manifestPath = Path.Combine(dir, ManifestName);
        InstallManifest? old = null;
        if (File.Exists(manifestPath))
        {
            try { old = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath)); }
            catch { log.Report("old manifest was unreadable — treating this as an external install"); }
        }

        string? inject = null;
        bool reuse = false;
        if (old?.InjectionDll is string oldName && !string.IsNullOrEmpty(oldName))
        {
            string p = Path.Combine(dir, oldName);
            if (File.Exists(p) && Hash(p).Equals(old.InjectionHash, StringComparison.OrdinalIgnoreCase))
            {
                inject = oldName;
                reuse = true;
            }
        }
        if (inject == null)
        {
            foreach (string name in InjectionNames)
            {
                string p = Path.Combine(dir, name);
                if (File.Exists(p) && IsOptiScaler(p)) { inject = name; reuse = true; break; }
            }
        }
        if (inject == null)
        {
            foreach (string name in InjectionNames)
            {
                if (!File.Exists(Path.Combine(dir, name))) { inject = name; break; }
            }
        }
        if (inject == null)
            throw new InvalidOperationException(
                "All the usual DLL names (dxgi/winmm/version/...) are taken. " +
                "Nothing was overwritten — pick another injection method by hand.");

        log.Report(reuse
            ? $"OptiScaler is already installed as {inject} — leaving the DLL alone"
            : $"Installing OptiScaler as {inject}");

        var manifest = new InstallManifest
        {
            Mode = PresetName(mode),
            InjectionDll = inject,
            InstalledAt = DateTime.Now,
            // Keep files from the previous manifest even when this run skips copying them.
            Files = old?.Files?.ToList() ?? new List<ManifestFile>()
        };

        if (!reuse)
            CopySafe(Path.Combine(PayloadRoot, "OptiScaler.dll"), inject, dir, old, manifest.Files, log);

        foreach (string rel in SupportFiles)
            CopySafe(Path.Combine(PayloadRoot, rel), rel, dir, old, manifest.Files, log);

        foreach (string sub in SupportDirs)
        {
            string from = Path.Combine(PayloadRoot, sub);
            if (!Directory.Exists(from)) continue;
            foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                string rel = Path.Combine(sub, Path.GetRelativePath(from, file));
                CopySafe(file, rel, dir, old, manifest.Files, log);
            }
        }

        string ini = Path.Combine(dir, "OptiScaler.ini");
        manifest.IniCreated = old?.IniCreated ?? !File.Exists(ini);
        if (!File.Exists(ini))
            CopySafe(Path.Combine(PayloadRoot, "OptiScaler.ini"), "OptiScaler.ini", dir, old, manifest.Files, log);
        if (!File.Exists(ini))
            throw new FileNotFoundException("OptiScaler.ini was not created — check the payload");

        // One pristine copy, plus a snapshot of whatever was there before this launch.
        string originalBackup = Path.Combine(dir, OriginalIniBackup);
        if (!File.Exists(originalBackup)) File.Copy(ini, originalBackup, true);
        File.Copy(ini, Path.Combine(dir, PrevIniBackup), true);
        manifest.IniBackup = OriginalIniBackup;

        log.Report($"Applying {manifest.Mode} preset…");
        IniEditor.Apply(ini, Profile, manifest.Mode);
        manifest.IniHashAfter = Hash(ini);

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        log.Report("Done. In game: Insert — menu, End — toggle frame generation.");
    }

    public static void Restore(GameInfo game, IProgress<string> log)
    {
        string dir = Path.GetDirectoryName(game.ExePath) ?? game.InstallDir;
        string manifestPath = Path.Combine(dir, ManifestName);
        if (!File.Exists(manifestPath)) throw new FileNotFoundException("No Lovebird manifest here — nothing to restore", manifestPath);
        var manifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath))
            ?? throw new InvalidDataException("Manifest is unreadable");

        foreach (ManifestFile item in manifest.Files)
        {
            // The ini gets edited after we hash it, so handle it separately below.
            if (item.Path.Equals("OptiScaler.ini", StringComparison.OrdinalIgnoreCase)) continue;
            string path = Path.Combine(dir, item.Path);
            if (!File.Exists(path)) continue;
            if (!Hash(path).Equals(item.Hash, StringComparison.OrdinalIgnoreCase))
            {
                log.Report($"changed since install, left alone: {item.Path}");
                continue;
            }
            File.Delete(path);
            log.Report($"removed: {item.Path}");
        }

        string ini = Path.Combine(dir, "OptiScaler.ini");
        string backup = Path.Combine(dir, manifest.IniBackup);
        bool untouchedByUser = File.Exists(ini) && !string.IsNullOrEmpty(manifest.IniHashAfter) &&
            Hash(ini).Equals(manifest.IniHashAfter, StringComparison.OrdinalIgnoreCase);
        if (untouchedByUser && manifest.IniCreated)
        {
            File.Delete(ini);
            log.Report("removed the OptiScaler.ini we created");
        }
        else if (untouchedByUser && File.Exists(backup))
        {
            File.Copy(backup, ini, true);
            log.Report("OptiScaler.ini restored from backup");
        }
        else if (File.Exists(ini))
        {
            log.Report("OptiScaler.ini was changed — leaving it as is");
        }

        // The snapshot is always ours; keep the pristine copy if the ini was already there.
        string prevBackup = Path.Combine(dir, PrevIniBackup);
        if (File.Exists(prevBackup)) File.Delete(prevBackup);
        if (manifest.IniCreated && File.Exists(backup)) File.Delete(backup);

        File.Delete(manifestPath);
        foreach (string sub in SupportDirs)
        {
            string p = Path.Combine(dir, sub);
            try { if (Directory.Exists(p) && !Directory.EnumerateFileSystemEntries(p).Any()) Directory.Delete(p); }
            catch { /* leave a non-empty folder alone */ }
        }
        log.Report("Restore finished. Game files were left alone.");
    }

    public static void Launch(GameInfo game)
    {
        Process.Start(new ProcessStartInfo(game.ExePath)
        {
            WorkingDirectory = Path.GetDirectoryName(game.ExePath) ?? game.InstallDir,
            UseShellExecute = true
        });
    }

    private static void CopySafe(string src, string rel, string dir, InstallManifest? old,
        List<ManifestFile> files, IProgress<string> log)
    {
        if (!File.Exists(src)) { log.Report($"not in payload: {rel} — skipped"); return; }
        string dst = Path.Combine(dir, rel);
        string srcHash = Hash(src);

        if (File.Exists(dst))
        {
            string dstHash = Hash(dst);
            if (dstHash.Equals(srcHash, StringComparison.OrdinalIgnoreCase))
            {
                log.Report($"already there: {rel}");
                return;
            }
            bool ours = old?.Files.Any(f => f.Path.Equals(rel, StringComparison.OrdinalIgnoreCase) &&
                                              f.Hash.Equals(dstHash, StringComparison.OrdinalIgnoreCase)) == true;
            if (!ours)
            {
                log.Report($"foreign file skipped: {rel}");
                return;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        File.Copy(src, dst, true);
        files.Add(new ManifestFile { Path = rel, Hash = srcHash });
        log.Report($"copied: {rel}");
    }

    private static bool IsOptiScaler(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            string payload = Path.Combine(PayloadRoot, "OptiScaler.dll");
            if (File.Exists(payload) &&
                Math.Abs(new FileInfo(path).Length - new FileInfo(payload).Length) > 4096) return false;
            string hash = Hash(path);
            if (hash.Equals(PayloadHash(), StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(FileVersionInfo.GetVersionInfo(path).OriginalFilename, "OptiScaler.dll",
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
