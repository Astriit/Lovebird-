using System.Text;
using System.Text.RegularExpressions;

namespace Lovebird;

// Same idea as the old apply_profile.ps1, without calling PowerShell.
internal static class IniEditor
{
    public static void Apply(string iniPath, string profileText, string preset)
    {
        List<(string Section, string Key, string Value)> values = Parse(profileText, preset);
        if (values.Count == 0) throw new InvalidOperationException($"Preset '{preset}' was not found in profile.cfg");

        var lines = File.ReadAllLines(iniPath).ToList();
        foreach ((string section, string key, string value) in values)
        {
            int start = 0;
            int end = lines.Count;
            if (!string.IsNullOrEmpty(section))
            {
                int s = lines.FindIndex(l => l.Trim().Equals($"[{section}]", StringComparison.OrdinalIgnoreCase));
                if (s < 0)
                {
                    lines.Add("");
                    lines.Add($"[{section}]");
                    lines.Add($"{key} = {value}");
                    continue;
                }
                start = s + 1;
                for (int i = start; i < lines.Count; i++)
                    if (Regex.IsMatch(lines[i].Trim(), @"^\[.+\]$")) { end = i; break; }
            }

            string pattern = @"^\s*" + Regex.Escape(key) + @"\s*=";
            int idx = -1;
            for (int i = start; i < end; i++)
                if (Regex.IsMatch(lines[i], pattern, RegexOptions.IgnoreCase)) { idx = i; break; }
            if (idx >= 0) lines[idx] = $"{key} = {value}";
            else if (!string.IsNullOrEmpty(section)) lines.Insert(start, $"{key} = {value}");
            else lines.Add($"{key} = {value}");
        }
        File.WriteAllLines(iniPath, lines, new UTF8Encoding(false));
    }

    private static List<(string, string, string)> Parse(string text, string preset)
    {
        var result = new List<(string, string, string)>();
        string current = "";
        foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = line[1..^1].Trim();
                continue;
            }
            if (!current.Equals(preset, StringComparison.OrdinalIgnoreCase)) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            string full = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            string section = "";
            string key = full;
            int slash = full.IndexOf('/');
            if (slash > 0)
            {
                section = full[..slash].Trim();
                key = full[(slash + 1)..].Trim();
            }
            if (key.Length > 0) result.Add((section, key, value));
        }
        return result;
    }
}
