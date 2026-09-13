using Godot;

namespace ManyWinters.Godot.Ui;

// The two typefaces the game's own text is set in, so the prologue, the epitaph, the chronicle
// and the buttons under them read as pages of one book. Titles are IM Fell English, a digital
// cut of a seventeenth-century printer's type whose rough edges belong to a woodcut world; it
// comes in a single regular weight with old-style figures, so it is kept to titles and never
// asked to carry a number or a button. Everything else is Vollkorn, a sturdy text face with
// a real bold and lining figures, which is what a resource counter and a "Gather berries"
// button need. Both are SIL Open Font License (OFL.txt sits beside each) and ship inside the
// game, and the title page (art/generate_splash.py) is drawn from the same two files.
//
// The inspector is deliberately not styled with these: it is a debugging aid, not part of the
// game's face, and stays in the engine's default font.
public static class InscriptionFont
{
    private const string FontDirectory = "res://Content/fonts";

    // The ink every full-screen title is set in, and the outline that keeps it legible over
    // whatever the camera happens to show behind it (see OutlinedTitleLabel) - shared here so
    // InscriptionOverlay and PausePanel read as the same title rather than two that happen to
    // agree for now.
    public static readonly Color Ink = new(0.93f, 0.88f, 0.78f);
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

    // A centred title carrying the ink-and-outline look every full-screen title wears - the one
    // look InscriptionOverlay and PausePanel both need, so neither hardcodes it locally.
    public static Label OutlinedTitleLabel(string text, int size)
    {
        var label = TitleLabel(text, size, Ink);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeColorOverride("font_outline_color", Outline);
        label.AddThemeConstantOverride("outline_size", OutlineSize);
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
