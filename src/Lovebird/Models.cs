using System.Text.Json.Serialization;

namespace Lovebird;

internal enum LaunchMode { Upscale, UpscaleFg }

internal sealed class GameInfo
{
    public string Name { get; set; } = "Unknown";
    public string Source { get; set; } = "Manual"; // Steam | Epic | GOG | Manual
    public string InstallDir { get; set; } = "";
    public string ExePath { get; set; } = "";
    public long SteamAppId { get; set; }
    public string Key => SteamAppId > 0 ? $"s{SteamAppId}" : $"{Source}:{InstallDir}".ToLowerInvariant();
    public string State { get; set; } = ""; // filled by the app, not serialized to disk
}

internal sealed class LibraryCache
{
    public int Version { get; set; } = 1;
    public DateTime ScannedAt { get; set; }
    public List<GameInfo> Games { get; set; } = new();
}

internal sealed class ManifestFile
{
    public string Path { get; set; } = "";      // relative to game dir
    public string Hash { get; set; } = "";
    public string Backup { get; set; } = "";    // relative backup path, if any
}

internal sealed class InstallManifest
{
    public string Version { get; set; } = "1.0";
    public string Mode { get; set; } = "";
    public string InjectionDll { get; set; } = "";
    public string InjectionHash { get; set; } = "";
    public string IniHashAfter { get; set; } = "";
    public string IniBackup { get; set; } = "";
    public bool IniCreated { get; set; }
    public DateTime InstalledAt { get; set; }
    public List<ManifestFile> Files { get; set; } = new();
}

internal static class Songs
{
    // The island up top rolls a new The Neighbourhood track on every start.
    private static readonly (string Title, string Line)[] Tracks =
    {
        ("Sweater Weather", "Cause I love the way you say come over"),
        ("Afraid", "I'm afraid of the way I'm thinking"),
        ("Judas", "I'm a Judas, I'm a fool"),
        ("Female Robbery", "She said, baby, I'm a thief"),
        ("Let It Go", "Let it go, let it go"),
        ("A Little Death", "A little death, a little death"),
        ("Something Good", "Something good is coming"),
        ("Wicked Ways", "I've got wicked ways"),
        ("Daddy Issues", "Daddy issues, daddy issues"),
        ("Be Mine", "Be mine, be mine"),
        ("Keep You", "I wanna keep you"),
        ("Honest", "Honest, honest"),
        ("One True Love", "One true love"),
        ("Heaven", "Heaven, heaven"),
        ("Soft", "Soft, soft"),
        ("Planet Theory", "Planet theory"),
        ("Cry Baby", "Cry baby"),
        ("Lovebomb", "Lovebomb"),
        ("Middle of Nowhere", "Middle of nowhere"),
        ("Bad Sun", "Bad sun"),
        ("Yellow Bucket", "Yellow bucket"),
        ("Jubilee", "Jubilee"),
        ("RIP 2 My Youth", "RIP 2 my youth"),
        ("Nobody Knows", "Nobody knows"),
        ("Alone", "Alone"),
        ("Devil's Advocate", "Devil's advocate"),
        ("Noise", "Noise"),
        ("The Neighbourhood", "We're the neighbourhood")
    };

    public static (string Title, string Line) RandomTrack()
    {
        var rnd = new Random(Guid.NewGuid().GetHashCode());
        return Tracks[rnd.Next(Tracks.Length)];
    }
}
