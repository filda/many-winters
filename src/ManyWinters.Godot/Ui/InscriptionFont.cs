using Godot;

namespace ManyWinters.Godot.Ui;

// The two typefaces the game's own text is set in. Titles: IM Fell English, a digital cut of a
// seventeenth-century printer's type; one regular weight with old-style figures, so it never
// carries a number or a button. Everything else: Vollkorn, with a real bold and lining figures.
// Both are SIL OFL (OFL.txt beside each) and shared with the title page (art/generate_splash.py).
//
// The inspector is deliberately not styled with these: it is a debugging aid, not part of the
// game's face.
public static class InscriptionFont
{
    private const string FontDirectory = "res://Content/fonts";

    // Ink and outline of every full-screen title (OutlinedTitleLabel). Private now that the
    // panels have moved onto paper: the only light-on-dark text left is the inscription overlay,
    // which sets its own titles through here.
    private static readonly Color Ink = new(0.93f, 0.88f, 0.78f);
    // The other way round: dark ink on a pale ground, for anything set on paper rather than over
    // the world (see PanelChrome.Parchment). Brown rather than black - nobody wrote in black.
    public static readonly Color DarkInk = new(0.20f, 0.14f, 0.09f);

    // The same ink stepped back, for text on paper that labels rather than speaks.
    public static readonly Color FadedDarkInk = new(0.20f, 0.14f, 0.09f, 0.62f);

    private static readonly Color Outline = new(0.16f, 0.12f, 0.08f);
    private const int OutlineSize = 10;

    private static FontFile Title { get; } = ResourceLoader.Load<FontFile>($"{FontDirectory}/im-fell-english/IMFeENrm28P.ttf");

    private static FontFile Body { get; } = ResourceLoader.Load<FontFile>($"{FontDirectory}/vollkorn/Vollkorn-Regular.ttf");

    private static FontFile BodyBold { get; } = ResourceLoader.Load<FontFile>($"{FontDirectory}/vollkorn/Vollkorn-Bold.ttf");

    // For a whole control tree (an overlay, the chronicle panel): every Label and Button under
    // it takes the body face without being styled one by one, buttons in bold.
    public static Theme BodyTheme(int fontSize)
    {
        var theme = new Theme { DefaultFont = Body, DefaultFontSize = fontSize };
        theme.SetFont("font", "Button", BodyBold);
        return theme;
    }

    public static Label TitleLabel(string text, int size, Color ink) => Styled(text, Title, size, ink);

    public static Label BodyLabel(string text, int size, Color ink) => Styled(text, Body, size, ink);

    // A centred title in the shared ink-and-outline look InscriptionOverlay and PausePanel use.
    public static Label OutlinedTitleLabel(string text, int size)
    {
        var label = TitleLabel(text, size, Ink);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeColorOverride("font_outline_color", Outline);
        label.AddThemeConstantOverride("outline_size", OutlineSize);
        return label;
    }

    // A centred title in dark ink, for a title set on paper (PanelChrome.Parchment) rather than
    // over the world - an outline there would only fatten the letters.
    public static Label PaperTitleLabel(string text, int size)
    {
        var label = TitleLabel(text, size, DarkInk);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private static Label Styled(string text, Font font, int size, Color ink)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", ink);
        return label;
    }
}
