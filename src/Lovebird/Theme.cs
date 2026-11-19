using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Lovebird;

// Colors from my Hyprland/waybar setup. Keep these in sync with wal-colors.lua.
internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(11, 11, 13);
    public static readonly Color Panel = Color.FromArgb(17, 18, 22);
    public static readonly Color PanelHi = Color.FromArgb(24, 26, 31);
    public static readonly Color Card = Color.FromArgb(22, 23, 28);
    public static readonly Color Text = Color.FromArgb(200, 200, 200);   // #c8c8c8
    public static readonly Color Muted = Color.FromArgb(122, 126, 136);
    public static readonly Color Accent = Color.FromArgb(141, 207, 229); // #8dcfe5 from wal-colors.lua
    public static readonly Color Border = Color.FromArgb(42, 46, 54);
    public static readonly Color Good = Color.FromArgb(166, 227, 161);
    public static readonly Color Warn = Color.FromArgb(249, 226, 175);

    private static readonly InstalledFontCollection Installed = new();

    public static Font Font(float size, FontStyle style = FontStyle.Regular)
    {
        string family = "Consolas";
        foreach (string candidate in new[] { "JetBrains Mono", "Cascadia Mono", "Consolas", "Menlo" })
        {
            if (Installed.Families.Any(f => f.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                family = candidate;
                break;
            }
        }
        return new Font(family, size, style);
    }

    public static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
        var p = new GraphicsPath();
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
    {
        using var path = Rounded(r, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void StrokeRound(Graphics g, Rectangle r, int radius, Color color, float width = 1f)
    {
        using var path = Rounded(r, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    public static void String(Graphics g, string? text, Font font, Color color, Rectangle bounds,
        StringFormat? format = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, bounds, format ?? new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        });
    }

    public static string Shorten(string path, int max = 64)
    {
        if (string.IsNullOrEmpty(path)) return "—";
        return path.Length <= max ? path : "…" + path[^max..];
    }
}

// Flat button with rounded corners and a small hover state.
internal sealed class RoundButton : Button
{
    private bool _hover;
    public Color BaseColor { get; set; } = Theme.PanelHi;
    public Color HoverColor { get; set; } = Theme.Accent;
    public Color ForeColor2 { get; set; } = Theme.Text;
    public int Radius { get; set; } = 9;

    public RoundButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        Cursor = Cursors.Hand;
        Font = Theme.Font(9f, FontStyle.Bold);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        bool accent = HoverColor == Theme.Accent && BaseColor == Theme.Accent;
        Color fill = accent ? (_hover ? Color.FromArgb(164, 222, 239) : Theme.Accent)
                            : (_hover ? HoverColor : BaseColor);
        Theme.FillRound(e.Graphics, r, Radius, fill);
        if (!accent) Theme.StrokeRound(e.Graphics, r, Radius, _hover ? Theme.Accent : Theme.Border);
        Theme.String(e.Graphics, Text, Font, accent ? Theme.Bg : (_hover && !accent ? Theme.Bg : ForeColor2),
            new Rectangle(6, 0, Width - 12, Height), new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            });
    }
}
