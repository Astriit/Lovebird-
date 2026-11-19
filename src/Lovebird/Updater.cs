using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Lovebird;

internal sealed record UpdateInfo(string Tag, string? ArchiveUrl, string? ChecksumUrl, string HtmlUrl);

internal static class Updater
{
    private const string ReleasesUrl = "https://api.github.com/repos/Astriit/Lovebird-/releases/latest";

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Lovebird/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        using HttpResponseMessage response = await client.GetAsync(ReleasesUrl, ct);
        if (!response.IsSuccessStatusCode) return null;

        string json = await response.Content.ReadAsStringAsync(ct);
        Release? release = JsonSerializer.Deserialize<Release>(json);
        if (release == null) return null;

        Version? remote = ParseVersion(release.TagName);
        Version local = Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0));
        if (remote == null || remote <= local) return null;

        Asset? archive = release.Assets.FirstOrDefault(a =>
            a.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase));
        Asset? checksum = release.Assets.FirstOrDefault(a =>
            a.Name.Contains("SHA256", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains("checksum", StringComparison.OrdinalIgnoreCase));

        return new UpdateInfo(release.TagName, archive?.Url, checksum?.Url, release.HtmlUrl);
    }

    public static async Task DownloadAndInstallAsync(UpdateInfo info, IProgress<string> progress, CancellationToken ct)
    {
        if (info.ArchiveUrl == null)
            throw new InvalidDataException("The release has no .7z archive");

        string downloadDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lovebird", "Downloads");
        Directory.CreateDirectory(downloadDir);

        string archivePath = Path.Combine(downloadDir, Path.GetFileName(new Uri(info.ArchiveUrl).LocalPath));

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Lovebird/1.0");

        progress.Report("Downloading the update…");
        using (HttpResponseMessage response = await client.GetAsync(info.ArchiveUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            using Stream web = await response.Content.ReadAsStreamAsync(ct);
            using FileStream file = File.Create(archivePath);
            await web.CopyToAsync(file, 1024 * 256, ct);
        }

        if (info.ChecksumUrl != null)
        {
            progress.Report("Checking the download…");
            string sums = await client.GetStringAsync(info.ChecksumUrl, ct);
            string? expected = FindChecksum(sums, Path.GetFileName(archivePath));
            if (expected == null)
                throw new InvalidDataException("The release has no checksum for this archive");

            using var sha = SHA256.Create();
            using FileStream file = File.OpenRead(archivePath);
            string actual = Convert.ToHexString(sha.ComputeHash(file)).ToLowerInvariant();
            if (!string.Equals(actual, expected.ToLowerInvariant(), StringComparison.Ordinal))
                throw new InvalidDataException("The download did not match its checksum");
        }

        string extractDir = Path.Combine(downloadDir, "update");
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        Directory.CreateDirectory(extractDir);

        progress.Report("Unpacking the update…");
        using (Stream stream = File.OpenRead(archivePath))
        using (IArchive archive = ArchiveFactory.OpenArchive(stream, new ReaderOptions()))
        {
            foreach (IArchiveEntry entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                string rel = (entry.Key ?? "").Replace('/', Path.DirectorySeparatorChar)
                                              .Replace('\\', Path.DirectorySeparatorChar);
                if (rel.Split(Path.DirectorySeparatorChar).Any(p => p == ".."))
                    throw new InvalidDataException("Bad path in the update archive");

                string target = Path.Combine(extractDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using Stream src = entry.OpenEntryStream();
                using Stream dst = File.Create(target);
                src.CopyTo(dst, 1024 * 256);
            }
        }

        string setup = Path.Combine(extractDir, "Lovebird 1.0", "Lovebird Setup.exe");
        if (!File.Exists(setup))
            throw new FileNotFoundException("The update archive has no installer", setup);

        string script = Path.Combine(downloadDir, "run_update.cmd");
        await File.WriteAllTextAsync(script,
            "@echo off\r\n" +
            ":wait\r\n" +
            "tasklist /FI \"IMAGENAME eq Lovebird.exe\" | find /I \"Lovebird.exe\" >nul && (timeout /t 1 /nobreak >nul & goto wait)\r\n" +
            $"start \"\" \"{setup}\"\r\n" +
            "del \"%~f0\"\r\n", ct);

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });

        progress.Report("The installer will start as soon as Lovebird closes.");
    }

    private static string? FindChecksum(string sums, string fileName)
    {
        foreach (string line in sums.Split('\n'))
        {
            string[] parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 &&
                parts[1].TrimStart('*').Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return parts[0];
        }
        return null;
    }

    private static Version? ParseVersion(string tag)
    {
        string value = tag.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value[1..];
        int cut = value.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0) value = value[..cut];
        return Version.TryParse(value, out Version? version) ? Normalize(version) : null;
    }

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    private sealed class Release
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "";

        [JsonPropertyName("assets")]
        public List<Asset> Assets { get; set; } = new();
    }

    private sealed class Asset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string Url { get; set; } = "";
    }
}
