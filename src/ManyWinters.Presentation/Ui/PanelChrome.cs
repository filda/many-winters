using Godot;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Ui;

// The translucent card every panel over the game world sits on - inspector, chronicle, pause
// panel, status bar - so they read as one UI language.
public static class PanelChrome
{
    // How far the text sits from the paper's edge. The caller lays this in itself, because the
    // StyleBox cannot carry it without insetting the grain with it.
    public const int PaperPadding = 14;

    // How far a filled box holds text off its own left and right edge. Public, because anything
    // laying its own labels over such a box has to line up with it.
    public const int FilledPadding = 8;

    // How much room the cross in a panel's corner takes.
    private const int CrossSize = 20;

    private const int CrossFontSize = 16;

    // How far the paper's grain wanders from its own colour, as a fraction of full ink. The title
    // page's is barely there - a tooth to the paper rather than a pattern - and so is this.
    private const float GrainDeviation = 0.03f;

    private const int GrainTile = 128;

    // The title page's paper and the colour it darkens to towards its edges. Kept the same as the
    // title page, so the first page the player holds is the one the game opened on.
    private static readonly Color Paper = new(0.878f, 0.843f, 0.769f);
    private static readonly Color PaperEdge = new(0.753f, 0.698f, 0.596f);

    public static StyleBoxFlat Background() => new()
    {
        BgColor = new Color(0f, 0f, 0f, 0.6f),
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 10,
        ContentMarginBottom = 10,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
    };

    // Dried paper, for the player's own card and the pages it shares a look with - the game reads
    // as a chronicle, so what the player holds is a page rather than a smoked-glass overlay. Text
    // on it wants dark ink, not the light ink the panels over the world use.
    //
    // No content margin: the grain is laid in as a child, and a container insets its children by
    // exactly this, which left a clean unweathered frame around the dirty middle. The padding is
    // the caller's to add inside the grain instead.
    public static StyleBoxFlat Parchment() => new()
    {
        BgColor = new Color(Paper, 0.96f),
        BorderColor = new Color(0.42f, 0.33f, 0.23f, 0.55f),
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
    };

    // A box filled with one colour, padded the way a line of text on paper wants: what the panels
    // build their flat buttons and their meter bars out of.
    public static StyleBoxFlat Filled(Color color) => new()
    {
        BgColor = color,
        ContentMarginLeft = FilledPadding,
        ContentMarginRight = FilledPadding,
        ContentMarginTop = 3,
        ContentMarginBottom = 3,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
    };

    // A meter's bar: a hollow trough rather than the engine's blue, because the fill's own colour
    // arrives with each reading - hunger changes colour as it worsens.
    public static ProgressBar MeterBar(int height)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, height),
        };
        bar.AddThemeStyleboxOverride("background", Filled(new Color(InscriptionFont.DarkInk, 0.14f)));
        return bar;
    }

    // The game's body face with the buttons flattened, for the lists drawn on paper - the actions
    // on the selection panel, the people on the band's roster. A raised grey box per line is the
    // look the debug inspector wears, and a column of them turns a page about people into a
    // settings dialog; the row lights up under the cursor instead.
    public static Theme PaperButtons(int fontSize)
    {
        var clear = new Color(0f, 0f, 0f, 0f);
        var theme = InscriptionFont.BodyTheme(fontSize);
        theme.SetStylebox("normal", "Button", Filled(clear));
        theme.SetStylebox("hover", "Button", Filled(new Color(InscriptionFont.DarkInk, 0.10f)));
        theme.SetStylebox("pressed", "Button", Filled(new Color(InscriptionFont.DarkInk, 0.18f)));
        theme.SetStylebox("disabled", "Button", Filled(clear));
        theme.SetStylebox("focus", "Button", Filled(clear));
        theme.SetColor("font_color", "Button", InscriptionFont.DarkInk);
        theme.SetColor("font_hover_color", "Button", InscriptionFont.DarkInk);
        theme.SetColor("font_pressed_color", "Button", InscriptionFont.DarkInk);
        // Distinctly fainter than anything beside it, so the eye sorts what can be pressed from
        // what cannot before it reads a word.
        theme.SetColor("font_disabled_color", "Button", new Color(InscriptionFont.DarkInk, 0.38f));

        // Ink on paper, the way every other mark on the page is, rather than the engine's pale
        // thumb in a dark trough - which on parchment read as a hole cut through the page.
        theme.SetStylebox("scroll", "VScrollBar", ScrollBox(new Color(InscriptionFont.DarkInk, 0.08f)));
        theme.SetStylebox("scroll_focus", "VScrollBar", ScrollBox(new Color(InscriptionFont.DarkInk, 0.08f)));
        theme.SetStylebox("grabber", "VScrollBar", ScrollBox(new Color(InscriptionFont.DarkInk, 0.45f)));
        theme.SetStylebox("grabber_highlight", "VScrollBar", ScrollBox(new Color(InscriptionFont.DarkInk, 0.65f)));
        theme.SetStylebox("grabber_pressed", "VScrollBar", ScrollBox(new Color(InscriptionFont.DarkInk, 0.8f)));
        return theme;
    }

    // The way out, in the top right corner where every window keeps it. Carries its own face and
    // its own box rather than taking the ambient theme's, because the same cross sits on a page
    // of paper and on the dark card over the world - only the ink changes. Quiet at rest and full
    // under the cursor, like everything else pressable here; it never takes the focus, so Space
    // does not close a window the player only pointed at.
    public static Button CloseCross(Color ink)
    {
        var cross = new Button
        {
            Text = "×",
            TooltipText = "Close (Escape)",
            CustomMinimumSize = new Vector2(CrossSize, CrossSize),
            FocusMode = Control.FocusModeEnum.None,
            Theme = InscriptionFont.BodyTheme(CrossFontSize),
        };

        cross.AddThemeStyleboxOverride("normal", CrossBox(new Color(0f, 0f, 0f, 0f)));
        cross.AddThemeStyleboxOverride("hover", CrossBox(new Color(ink, 0.12f)));
        cross.AddThemeStyleboxOverride("pressed", CrossBox(new Color(ink, 0.20f)));
        cross.AddThemeStyleboxOverride("focus", CrossBox(new Color(0f, 0f, 0f, 0f)));
        cross.AddThemeColorOverride("font_color", new Color(ink, 0.55f));
        cross.AddThemeColorOverride("font_hover_color", ink);
        cross.AddThemeColorOverride("font_pressed_color", ink);
        return cross;
    }

    // The way out in the card's own top right corner, for a page whose head is not the first
    // thing on it. Added last to a PanelContainer, which sizes every child to the whole card: the
    // cross rides in a frame of that size and hangs itself in the corner of it, clear of whatever
    // the page is saying.
    public static Control CornerCross(Action closed)
    {
        var corner = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        corner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var cross = CloseCross(InscriptionFont.DarkInk);
        cross.AnchorLeft = 1f;
        cross.AnchorRight = 1f;
        cross.OffsetLeft = -(CrossSize + PaperPadding);
        cross.OffsetRight = -PaperPadding;
        cross.OffsetTop = PaperPadding;
        cross.OffsetBottom = PaperPadding + CrossSize;
        cross.Pressed += () => closed();
        corner.AddChild(cross);
        return corner;
    }

    // A hairline in the ink, between sections of a page - the engine's own separator draws a grey
    // bevel, which is not a mark paper makes.
    public static HSeparator Rule()
    {
        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(InscriptionFont.DarkInk, 0.28f) });
        return rule;
    }

    // The same hairline standing upright, between two columns of a page.
    public static VSeparator VerticalRule()
    {
        var rule = new VSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(InscriptionFont.DarkInk, 0.28f), Vertical = true });
        return rule;
    }

    // The age on the page, the way the title page wears it: broad blotches where it was handled, a
    // fine tooth over all of it, edges gone darker than the middle, and under the stains the same
    // scratched diagonal hatching the title page's plates are drawn with. All of it faint - past a certain strength this stops being paper and
    // becomes wallpaper, and the ink has to fight it.
    //
    // `of` is which page this is (the panel's type, "pause", "menu"): every panel is cut
    // from the same sheet, and the name is what decides how this one aged, so two pages open side
    // by side are not the same stain twice.
    //
    // A node rather than part of the StyleBox, because a StyleBoxFlat cannot carry a texture. The
    // caller adds it first so its own content draws on top, and it never takes the mouse.
    public static Control Grain(string of)
    {
        var paper = PaperWeathering.Of(of);

        var grain = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        grain.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        grain.AddChild(Blotches(paper));
        grain.AddChild(Hatching(paper));
        grain.AddChild(Tooth(paper));
        grain.AddChild(Vignette());
        return grain;
    }

    // A scroll bar's trough or thumb: a thin rounded stroke, as wide as its margins make it.
    private static StyleBoxFlat ScrollBox(Color color) => new()
    {
        BgColor = color,
        ContentMarginLeft = 3,
        ContentMarginRight = 3,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
    };

    // Tighter than Filled: a cross is one glyph, and a line of text's padding around it would
    // push it off the corner it belongs in.
    private static StyleBoxFlat CrossBox(Color fill) => new()
    {
        BgColor = fill,
        ContentMarginLeft = 4,
        ContentMarginRight = 4,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
    };

    private static TextureRect Blotches(PaperWeathering paper)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Seed = paper.Seed,
            // Low enough that the stains are the size of a thumb, not of a grain of sand; the
            // octaves put the sand back on top of them.
            Frequency = paper.BlotchFrequency,
            FractalOctaves = 5,
        };

        return Tiled(new NoiseTexture2D { Noise = noise, Width = 256, Height = 256, Seamless = true }, new Color(0.34f, 0.24f, 0.13f, paper.BlotchStrength));
    }

    // Drawn rather than noised: a hatch is strokes, and noise has no strokes in it.
    private static TextureRect Hatching(PaperWeathering paper)
    {
        var data = PaperScratches.Rgba(paper.HatchSpacing, paper.HatchRising, paper.Seed);
        var image = Image.CreateFromData(PaperScratches.Tile, PaperScratches.Tile, false, Image.Format.Rgba8, data);
        return Tiled(ImageTexture.CreateFromImage(image), new Color(0.30f, 0.22f, 0.12f, 0.24f));
    }

    // Noise per pixel, half of it a speck of ink and half a speck of bare paper, so the grain
    // roughens the page without darkening it overall. Uncorrelated pixels tile without a seam.
    private static TextureRect Tooth(PaperWeathering paper)
    {
        var random = new RandomNumberGenerator { Seed = (uint)paper.Seed };
        var image = Image.CreateEmpty(GrainTile, GrainTile, false, Image.Format.Rgba8);
        for (var y = 0; y < GrainTile; y++)
        {
            for (var x = 0; x < GrainTile; x++)
            {
                var speck = random.Randfn(0f, GrainDeviation);
                image.SetPixel(x, y, speck > 0f ? new Color(InscriptionFont.DarkInk, speck) : new Color(Paper.Lightened(0.5f), -speck));
            }
        }

        return Tiled(ImageTexture.CreateFromImage(image), Colors.White);
    }

    // Stretched over the whole page, whatever its shape, so a tall narrow panel darkens along its
    // long sides the way the wide title page does along its short ones. The ramp is the title
    // page's: clean to just past the middle, then darkening straight towards the corners.
    private static TextureRect Vignette()
    {
        var gradient = new Gradient
        {
            Offsets = [0f, 0.39f, 0.71f, 1f],
            Colors = [new Color(PaperEdge, 0f), new Color(PaperEdge, 0f), new Color(PaperEdge, 0.49f), new Color(PaperEdge, 0.8f)],
        };

        // Measured from the middle to a corner rather than to an edge, so the corner is where the
        // ramp ends instead of where it was cut off.
        var texture = new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 1f),
            Width = 128,
            Height = 128,
        };

        var rect = new TextureRect
        {
            Texture = texture,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return rect;
    }


    private static TextureRect Tiled(Texture2D texture, Color modulate)
    {
        var rect = new TextureRect
        {
            Texture = texture,
            StretchMode = TextureRect.StretchModeEnum.Tile,
            // Or the tile would set a floor under whatever panel this is laid into.
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = modulate,
        };
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return rect;
    }
}
