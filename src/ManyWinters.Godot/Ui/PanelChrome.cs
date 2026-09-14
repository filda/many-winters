using Godot;

namespace ManyWinters.Godot.Ui;

// The translucent card every panel over the game world sits on - inspector, chronicle, pause
// panel, status bar - so they read as one UI language.
public static class PanelChrome
{
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
    // on it wants InscriptionFont.DarkInk, not the light Ink the panels over the world use.
    //
    // No content margin: the grain (see Grain) is laid in as a child, and a container insets its
    // children by exactly this, which left a clean unweathered frame around the dirty middle. The
    // padding is the caller's to add inside the grain instead.
    public static StyleBoxFlat Parchment() => new()
    {
        BgColor = new Color(0.87f, 0.82f, 0.71f, 0.96f),
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

    // How far the text sits from the paper's edge. The caller lays this in itself, because the
    // StyleBox cannot carry it without insetting the grain with it.
    public const int PaperPadding = 14;

    // How far a filled box holds text off its own left and right edge. Public, because anything
    // laying its own labels over such a box (BandPanel's rows) has to line up with it.
    public const int FilledPadding = 8;

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
    // arrives with each reading (see MeterReading.Fill) - hunger changes colour as it worsens.
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
        return theme;
    }

    // The age on the page: broad blotches where it was handled, and the printer's hatching under
    // them, the same diagonal stroke the sprites are drawn with. Both are faint - past a certain
    // strength this stops being paper and becomes wallpaper, and the ink has to fight it.
    //
    // A node rather than part of the StyleBox, because a StyleBoxFlat cannot carry a texture. The
    // caller adds it first so its own content draws on top, and it never takes the mouse.
    public static Control Grain()
    {
        var grain = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        grain.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        grain.AddChild(Blotches());
        grain.AddChild(Hatching());
        return grain;
    }

    private static TextureRect Blotches()
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            // Low enough that the stains are the size of a thumb, not of a grain of sand; the
            // octaves put the sand back on top of them.
            Frequency = 0.012f,
            FractalOctaves = 5,
        };

        return Tiled(new NoiseTexture2D { Noise = noise, Width = 256, Height = 256, Seamless = true }, new Color(0.34f, 0.24f, 0.13f, 0.22f));
    }

    // Drawn rather than noised: a hatch is regular by nature, and a 16px tile of diagonal strokes
    // repeats seamlessly because the period divides the tile.
    private static TextureRect Hatching()
    {
        const int tile = 16;
        const int spacing = 4;

        var image = Image.CreateEmpty(tile, tile, false, Image.Format.Rgba8);
        image.Fill(new Color(1f, 1f, 1f, 0f));
        for (var y = 0; y < tile; y++)
        {
            for (var x = 0; x < tile; x++)
            {
                if ((x + y) % spacing == 0)
                {
                    image.SetPixel(x, y, Colors.White);
                }
            }
        }

        return Tiled(ImageTexture.CreateFromImage(image), new Color(0.30f, 0.22f, 0.12f, 0.07f));
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
