using System.Drawing.Drawing2D;

namespace Lovebird;

// Blurred wallpaper behind the library so it feels like the desktop.
internal sealed class BackdropPanel : Panel
{
    public Image? Backdrop { get; set; }

    public BackdropPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Backdrop != null) e.Graphics.DrawImage(Backdrop, ClientRectangle);
        using var veil = new SolidBrush(Color.FromArgb(236, 11, 11, 13));
        e.Graphics.FillRectangle(veil, ClientRectangle);
    }
}

internal sealed class IslandBar : Control
{
    public string Song { get; set; } = "";
    public string Line { get; set; } = "";
    public event EventHandler? Reroll;

    public IslandBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
    }

    protected override void OnClick(EventArgs e) { Reroll?.Invoke(this, EventArgs.Empty); base.OnClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRound(e.Graphics, r, 11, Color.FromArgb(26, 28, 33));
        Theme.StrokeRound(e.Graphics, r, 11, Color.FromArgb(48, 52, 60));
        e.Graphics.FillEllipse(new SolidBrush(Theme.Accent), new Rectangle(15, Height / 2 - 4, 8, 8));
        using Font bold = Theme.Font(9.5f, FontStyle.Bold);
        using Font small = Theme.Font(8f);
        Theme.String(e.Graphics, "♪  " + Song, bold, Theme.Text, new Rectangle(32, 0, 210, Height));
        Theme.String(e.Graphics, "— " + Line, small, Theme.Muted, new Rectangle(238, 0, Width - 250, Height));
    }
}

internal sealed class ModeCard : Control
{
    public string Number { get; set; } = "01";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public bool Selected { get; set; }
    private bool _hover;

    public ModeCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRound(e.Graphics, r, 10, Selected ? Color.FromArgb(23, 34, 39) : (_hover ? Theme.PanelHi : Theme.Card));
        Theme.StrokeRound(e.Graphics, r, 10, Selected ? Theme.Accent : Theme.Border, Selected ? 1.6f : 1f);

        Theme.FillRound(e.Graphics, new Rectangle(14, 15, 34, 30), 7,
            Selected ? Theme.Accent : Color.FromArgb(36, 40, 47));
        using Font numFont = Theme.Font(10f, FontStyle.Bold);
        Theme.String(e.Graphics, Number, numFont, Selected ? Theme.Bg : Theme.Muted,
            new Rectangle(14, 15, 34, 30), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

        using Font titleFont = Theme.Font(10f, FontStyle.Bold);
        Theme.String(e.Graphics, Title, titleFont, Selected ? Theme.Accent : Theme.Text, new Rectangle(58, 10, Width - 70, 25));
        using Font subFont = Theme.Font(8.5f);
        Theme.String(e.Graphics, Subtitle, subFont, Theme.Muted, new Rectangle(58, 34, Width - 70, 24));
    }
}

internal sealed class GameCard : Control
{
    public GameInfo Game { get; set; } = new();
    public Image? Cover { get; set; }
    public bool Selected { get; set; }
    public string Status { get; set; } = "";
    private bool _hover;

    public GameCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        Size = new Size(196, 278);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, r, 10, Selected ? Color.FromArgb(25, 33, 38) : (_hover ? Theme.PanelHi : Theme.Card));
        Theme.StrokeRound(g, r, 10, Selected ? Theme.Accent : Theme.Border, Selected ? 1.6f : 1f);

        var coverRect = new Rectangle(7, 7, Width - 15, 202);
        using (var clip = Theme.Rounded(coverRect, 7))
        {
            g.SetClip(clip);
            if (Cover != null) g.DrawImage(Cover, coverRect);
            else g.FillRectangle(new SolidBrush(Color.FromArgb(22, 24, 30)), coverRect);

            using var shade = new LinearGradientBrush(new Rectangle(0, 130, Width, 80),
                Color.FromArgb(0, 11, 11, 13), Color.FromArgb(225, 11, 11, 13), 90f);
            g.FillRectangle(shade, new Rectangle(0, 130, Width, 80));
            g.ResetClip();
        }
        Theme.StrokeRound(g, coverRect, 7, Color.FromArgb(48, 52, 60));

        // source badge
        string badge = Game.Source.ToUpperInvariant();
        using Font badgeFont = Theme.Font(7.5f, FontStyle.Bold);
        int bw = (int)g.MeasureString(badge, badgeFont).Width + 14;
        Theme.FillRound(g, new Rectangle(14, 14, bw, 20), 6, Color.FromArgb(210, 11, 11, 13));
        Theme.String(g, badge, badgeFont, Theme.Accent, new Rectangle(21, 14, bw - 14, 20));

        using Font nameFont = Theme.Font(9.5f, FontStyle.Bold);
        (string line1, string line2) = Wrap2(g, Game.Name, nameFont, Width - 28);
        Theme.String(g, line1, nameFont, Theme.Text, new Rectangle(14, 216, Width - 28, 18));
        if (line2.Length > 0)
            Theme.String(g, line2, nameFont, Theme.Text, new Rectangle(14, 233, Width - 28, 18));

        using Font small = Theme.Font(8f);
        Theme.String(g, Game.Source == "Steam" && Game.SteamAppId > 0 ? $"APP {Game.SteamAppId}" : "OK",
            small, Theme.Muted, new Rectangle(14, 254, 90, 18));

        if (!string.IsNullOrEmpty(Status))
        {
            Color c = Status == "external" ? Theme.Warn : Theme.Good;
            g.FillEllipse(new SolidBrush(c), new Rectangle(Width - 74, 259, 7, 7));
            Theme.String(g, Status, small, c, new Rectangle(Width - 64, 250, 52, 22));
        }
    }

    private static (string, string) Wrap2(Graphics g, string text, Font font, float max)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string first = "";
        foreach (string word in words)
        {
            string test = first.Length == 0 ? word : first + " " + word;
            if (g.MeasureString(test, font).Width > max && first.Length > 0) break;
            first = test;
        }
        string second = string.Join(" ", words.Skip(first.Split(' ').Length));
        if (g.MeasureString(second, font).Width > max)
            second = second[..Math.Max(1, (int)(second.Length * max / g.MeasureString(second, font).Width))] + "…";
        return (first, second);
    }
}
