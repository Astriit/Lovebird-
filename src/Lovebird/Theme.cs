using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Lovebird;

// Dark theme in the same spirit as the Epic Games launcher.
internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(18, 18, 18);
    public static readonly Color Panel = Color.FromArgb(27, 27, 27);
    public static readonly Color PanelHi = Color.FromArgb(42, 42, 42);
    public static readonly Color Card = Color.FromArgb(36, 36, 36);
    public static readonly Color Text = Color.FromArgb(245, 245, 245);
    public static readonly Color Muted = Color.FromArgb(168, 168, 168);
    public static readonly Color Accent = Color.FromArgb(0, 120, 242);
    public static readonly Color AccentHover = Color.FromArgb(46, 144, 255);
    public static readonly Color Border = Color.FromArgb(51, 51, 51);
    public static readonly Color Good = Color.FromArgb(46, 204, 113);
    public static readonly Color Warn = Color.FromArgb(245, 166, 35);
    public static readonly Color Danger = Color.FromArgb(232, 17, 35);
    public static readonly Color Glass = Color.FromArgb(225, 24, 24, 24);
    public static readonly Color GlassDark = Color.FromArgb(235, 18, 18, 18);

    private static readonly InstalledFontCollection Installed = new();

    public static Font Font(float size, FontStyle style = FontStyle.Regular)
    {
        string family = "Arial";
        foreach (string candidate in new[] { "Segoe UI", "Inter", "Arial" })
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
    public Color HoverColor { get; set; } = Theme.PanelHi;
    public Color ForeColor2 { get; set; } = Theme.Text;
    public int Radius { get; set; } = 6;

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
        bool accent = BaseColor == Theme.Accent;
        Color fill = accent ? (_hover ? Theme.AccentHover : Theme.Accent) : (_hover ? HoverColor : BaseColor);
        Theme.FillRound(e.Graphics, r, Radius, fill);
        if (!accent) Theme.StrokeRound(e.Graphics, r, Radius, _hover ? Theme.Accent : Theme.Border);
        Theme.String(e.Graphics, Text, Font, accent || _hover ? Color.White : ForeColor2,
            new Rectangle(6, 0, Width - 12, Height), new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            });
    }
}
