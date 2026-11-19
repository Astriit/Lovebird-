using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lovebird;

// Covers come from the Steam CDN. Offline, we draw a placeholder instead of erroring out.
internal static class CoverService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    static CoverService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Lovebird/1.0");
    }

    private static string CacheDir
    {
        get
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Lovebird", "covers");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string KeyFor(GameInfo game) => game.SteamAppId > 0
        ? game.SteamAppId.ToString()
        : "n" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(game.Key)))[..16].ToLowerInvariant();

    public static async Task<Image> GetAsync(GameInfo game, CancellationToken ct = default)
    {
        string file = Path.Combine(CacheDir, KeyFor(game) + ".jpg");
        if (File.Exists(file) && new FileInfo(file).Length > 2000)
        {
            try { return Load(file); }
            catch { /* corrupted cache - download again */ }
        }

        long appId = game.SteamAppId;
        if (appId == 0) appId = await SearchSteamAsync(game.Name, ct);

        if (appId > 0)
        {
            string[] urls =
            {
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
                $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg"
            };
            foreach (string url in urls)
            {
                byte[] bytes = await DownloadAsync(url, ct);
                if (bytes.Length < 2000) continue;
                try
                {
                    File.WriteAllBytes(file, bytes);
                    return Load(file);
                }
                catch { /* keep going with the other URL */ }
            }
        }
        return Placeholder(game.Name);
    }

    private static Image Load(string file)
    {
        using var stream = File.OpenRead(file);
        using var tmp = Image.FromStream(stream);
        return new Bitmap(tmp);
    }

    private static async Task<byte[]> DownloadAsync(string url, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await Http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return Array.Empty<byte>();
            byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct);
            // Quick JPEG/PNG sanity check.
            if (bytes.Length < 2000) return Array.Empty<byte>();
            if (!(bytes[0] == 0xFF && bytes[1] == 0xD8) && !(bytes[0] == 0x89 && bytes[1] == 0x50)) return Array.Empty<byte>();
            return bytes;
        }
        catch { return Array.Empty<byte>(); }
    }

    private static async Task<long> SearchSteamAsync(string name, CancellationToken ct)
    {
        try
        {
            string url = "https://steamcommunity.com/actions/SearchApps/" + Uri.EscapeDataString(name);
            using HttpResponseMessage response = await Http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return 0;
            string json = await response.Content.ReadAsStringAsync(ct);
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return 0;

            string wanted = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            long best = 0;
            int bestScore = -1;
            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("appid", out JsonElement id) ||
                    !item.TryGetProperty("name", out JsonElement nm)) continue;
                string got = new string((nm.GetString() ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                int score = got == wanted ? 100 : got.Contains(wanted) || wanted.Contains(got) ? 60 : 0;
                if (score > bestScore && long.TryParse(id.ToString(), out long v))
                {
                    best = v;
                    bestScore = score;
                }
            }
            return bestScore >= 60 ? best : 0;
        }
        catch { return 0; }
    }

    private static Image Placeholder(string name)
    {
        var bmp = new Bitmap(300, 420);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Panel);
            using (var grad = new LinearGradientBrush(new Rectangle(0, 0, 300, 420),
                       Theme.PanelHi, Theme.Bg, 70f))
                g.FillRectangle(grad, new Rectangle(0, 0, 300, 420));

            using (var pen = new Pen(Theme.Border))
                for (int i = -420; i < 300; i += 26) g.DrawLine(pen, i, 420, i + 420, 0);

            Theme.StrokeRound(g, new Rectangle(10, 10, 279, 399), 12, Theme.Border, 2f);

            string initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2).Select(w => char.ToUpperInvariant(w[0])));
            if (initials.Length == 0) initials = "LB";
            using Font big = Theme.Font(54f, FontStyle.Bold);
            Theme.String(g, initials, big, Theme.Accent, new Rectangle(20, 130, 260, 100),
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            using Font small = Theme.Font(9f);
            Theme.String(g, "LOVEBIRD 1.0", small, Theme.Muted, new Rectangle(20, 355, 260, 30),
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
        return bmp;
    }
}
