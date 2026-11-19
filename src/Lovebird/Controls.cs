using System.Drawing.Drawing2D;

namespace Lovebird;

// Blurred wallpaper behind the library so the window feels like part of the desktop.
internal sealed class BackdropPanel : Panel
{
    public Image? Backdrop { get; set; }

    public BackdropPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Backdrop != null) e.Graphics.DrawImage(Backdrop, ClientRectangle);
        using var veil = new SolidBrush(Color.FromArgb(205, 18, 18, 18));
        e.Graphics.FillRectangle(veil, ClientRectangle);
    }
}

// Panel that paints a translucent surface over the backdrop.
internal sealed class GlassPanel : Panel
{
    public Color FillColor { get; set; } = Theme.Glass;
    public int Radius { get; set; }

    public GlassPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        if (Radius > 0) Theme.FillRound(e.Graphics, r, Radius, FillColor);
        else using (var brush = new SolidBrush(FillColor)) e.Graphics.FillRectangle(brush, r);
        base.OnPaint(e);
    }
}

// Same idea for the scrolling game grid.
internal sealed class GlassFlowLayoutPanel : FlowLayoutPanel
{
    public GlassFlowLayoutPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var brush = new SolidBrush(Color.FromArgb(235, 18, 18, 18));
        e.Graphics.FillRectangle(brush, ClientRectangle);
        base.OnPaint(e);
    }
}

// Window controls drawn by hand instead of using font glyphs.
internal sealed class WindowButton : Control
{
    public enum ButtonKind { Minimize, Maximize, Close }

    public ButtonKind Kind { get; set; }
    private bool _hover;

    public WindowButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        Size = new Size(38, 32);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Color hover = Kind == ButtonKind.Close ? Theme.Danger : Theme.PanelHi;
        if (_hover) Theme.FillRound(e.Graphics, r, 5, hover);

        using var pen = new Pen(Color.White, 1.5f);
        int cx = Width / 2;
        int cy = Height / 2;
        switch (Kind)
        {
            case ButtonKind.Minimize:
                e.Graphics.DrawLine(pen, cx - 6, cy, cx + 6, cy);
                break;
            case ButtonKind.Maximize:
                e.Graphics.DrawRectangle(pen, cx - 6, cy - 5, 12, 10);
                break;
            case ButtonKind.Close:
                e.Graphics.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                e.Graphics.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                break;
        }
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
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
    }

    protected override void OnClick(EventArgs e) { Reroll?.Invoke(this, EventArgs.Empty); base.OnClick(e); }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRound(e.Graphics, r, 10, Color.FromArgb(190, 32, 32, 32));
        Theme.StrokeRound(e.Graphics, r, 10, Theme.Border);

        using (var dot = new SolidBrush(Theme.Accent))
            e.Graphics.FillEllipse(dot, new Rectangle(14, Height / 2 - 4, 8, 8));

        using Font bold = Theme.Font(9f, FontStyle.Bold);
        using Font small = Theme.Font(8f);
        Theme.String(e.Graphics, Song, bold, Theme.Text, new Rectangle(30, 0, 165, Height));
        using (var pen = new Pen(Theme.Border))
            e.Graphics.DrawLine(pen, 200, 8, 200, Height - 8);
        Theme.String(e.Graphics, Line, small, Theme.Muted, new Rectangle(212, 0, Width - 224, Height));
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
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRound(e.Graphics, r, 8, Selected ? Theme.Accent : (_hover ? Theme.PanelHi : Theme.Card));
        Theme.StrokeRound(e.Graphics, r, 8, Selected ? Theme.Accent : Theme.Border, Selected ? 1.6f : 1f);

        var badge = new Rectangle(12, 16, 32, 30);
        Theme.FillRound(e.Graphics, badge, 6, Selected ? Color.FromArgb(255, 255, 255, 255) : Theme.PanelHi);
        using Font numFont = Theme.Font(9.5f, FontStyle.Bold);
        Theme.String(e.Graphics, Number, numFont, Selected ? Theme.Accent : Theme.Text, badge,
            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

        using Font titleFont = Theme.Font(10f, FontStyle.Bold);
        Theme.String(e.Graphics, Title, titleFont, Color.White, new Rectangle(54, 10, Width - 66, 26));
        using Font subFont = Theme.Font(8.5f);
        Theme.String(e.Graphics, Subtitle, subFont, Selected ? Color.FromArgb(220, 235, 255) : Theme.Muted,
            new Rectangle(54, 34, Width - 66, 24));
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
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Size = new Size(196, 278);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRound(g, r, 8, Selected ? Theme.Accent : (_hover ? Theme.PanelHi : Theme.Card));
        Theme.StrokeRound(g, r, 8, Selected ? Theme.Accent : Theme.Border, Selected ? 1.6f : 1f);

        var coverRect = new Rectangle(8, 8, Width - 16, 184);
        using (var clip = Theme.Rounded(coverRect, 6))
        {
            g.SetClip(clip);
            if (Cover != null) g.DrawImage(Cover, coverRect);
            else g.FillRectangle(new SolidBrush(Theme.PanelHi), coverRect);

            using var shade = new LinearGradientBrush(new Rectangle(0, 120, Width, 72),
                Color.FromArgb(0, 18, 18, 18), Color.FromArgb(220, 18, 18, 18), 90f);
            g.FillRectangle(shade, new Rectangle(0, 120, Width, 72));
            g.ResetClip();
        }
        Theme.StrokeRound(g, coverRect, 6, Theme.Border);

        string badge = Game.Source.ToUpperInvariant();
        using Font badgeFont = Theme.Font(7.5f, FontStyle.Bold);
        int bw = (int)g.MeasureString(badge, badgeFont).Width + 14;
        Theme.FillRound(g, new Rectangle(14, 14, bw, 20), 5, Color.FromArgb(215, 18, 18, 18));
        Theme.String(g, badge, badgeFont, Color.White, new Rectangle(21, 14, bw - 14, 20));

        using Font nameFont = Theme.Font(9.5f, FontStyle.Bold);
        (string line1, string line2) = Wrap2(g, Game.Name, nameFont, Width - 28);
        Theme.String(g, line1, nameFont, Color.White, new Rectangle(14, 202, Width - 28, 18));
        if (line2.Length > 0)
            Theme.String(g, line2, nameFont, Color.White, new Rectangle(14, 220, Width - 28, 18));

        using Font small = Theme.Font(8f);
        string app = Game.Source == "Steam" && Game.SteamAppId > 0 ? $"APP {Game.SteamAppId}" : "LOCAL";
        Theme.String(g, app, small, Theme.Muted, new Rectangle(14, 248, 90, 18));

        if (!string.IsNullOrEmpty(Status))
        {
            Color c = Status == "external" ? Theme.Warn : Theme.Good;
            int tw = (int)g.MeasureString(Status, small).Width + 14;
            var pill = new Rectangle(Width - tw - 14, 247, tw, 20);
            Theme.FillRound(g, pill, 5, Color.FromArgb(180, 30, 30, 30));
            Theme.String(g, Status, small, c,
                new Rectangle(pill.X + 7, pill.Y, pill.Width - 14, pill.Height),
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
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
