using Godot;

namespace ManyWinters.Godot.Ui;

// The "Pack: ..." line on the person's card and on their page: the way into the workshop, where
// what is carried can be worked. Shared, because the two pages draw the same line and it has to
// read the same on both.
//
// Set in the same type as the lines around it and starting where they start, so it still reads as
// part of the card; but the whole line is a button that lights up under the cursor the way the
// name at the top of the card does, with a sack at the far end the way the name has its glass -
// both say "this opens something" in the same voice.
internal sealed class PackLine
{
    private const int IconSize = 18;

    // A Button is no container, so it is told how tall a line of body text and the box's own
    // padding above and below it make it.
    private const int ExtraHeight = 12;

    private readonly Label _text;

    internal PackLine(int fontSize, Action pressed)
    {
        // Pulled out past the column by the box's own padding on both sides, so the text inside it
        // starts flush with the labels above and below while the highlight still has room around
        // the words.
        Root = new MarginContainer();
        Root.AddThemeConstantOverride("margin_left", -PanelChrome.FilledPadding);
        Root.AddThemeConstantOverride("margin_right", -PanelChrome.FilledPadding);

        var button = new Button { Text = string.Empty, CustomMinimumSize = new Vector2(0, fontSize + ExtraHeight) };
        button.Pressed += pressed;
        Root.AddChild(button);

        var padding = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        padding.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        padding.AddThemeConstantOverride("margin_left", PanelChrome.FilledPadding);
        padding.AddThemeConstantOverride("margin_right", PanelChrome.FilledPadding);
        button.AddChild(padding);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        padding.AddChild(row);

        // The body face, not the bold one the theme gives buttons: this is a line of the card
        // that happens to open something, not a command. The caption in full ink like the meters'
        // "Fed" above it, what is carried in the quieter ink after it.
        var caption = InscriptionFont.BodyLabel("Pack:", fontSize, InscriptionFont.DarkInk);
        caption.AutowrapMode = TextServer.AutowrapMode.Off;
        caption.VerticalAlignment = VerticalAlignment.Center;
        caption.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(caption);

        _text = InscriptionFont.BodyLabel(string.Empty, fontSize, InscriptionFont.FadedDarkInk);
        _text.AutowrapMode = TextServer.AutowrapMode.Off;
        _text.ClipText = true;
        _text.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _text.VerticalAlignment = VerticalAlignment.Center;
        _text.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(_text);

        // Takes no mouse, so hovering it is hovering the line - the whole of it lights up together.
        row.AddChild(new TextureRect
        {
            Texture = Sack(),
            CustomMinimumSize = new Vector2(IconSize, IconSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
        });
    }

    internal Control Root { get; }

    internal void Show(string carried) => _text.Text = carried;

    // A sack tied at the neck, in the faded ink of the name's glass, until somebody paints both.
    // Solid rather than drawn in line like the glass: at this size a hollow sack is a round outline
    // with a knob on top, which reads as a stopwatch.
    private static ImageTexture Sack()
    {
        var image = Image.CreateEmpty(IconSize, IconSize, false, Image.Format.Rgba8);
        for (var y = 0; y < IconSize; y++)
        {
            for (var x = 0; x < IconSize; x++)
            {
                if (IsSack(x - (IconSize / 2f) + 0.5f, y - (IconSize / 2f) + 0.5f))
                {
                    image.SetPixel(x, y, InscriptionFont.FadedDarkInk);
                }
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    // In the square's own centred coordinates, (0, 0) being its middle and y growing downwards.
    private static bool IsSack(float dx, float dy)
    {
        // The bulge that holds everything, heavy at the bottom and cut flat where it sits.
        const float bodyCenter = 3f;
        const float bodyHalfWidth = 6.8f;
        const float bodyHalfHeight = 5f;
        var body = dy <= 7f
            && (((dx / bodyHalfWidth) * (dx / bodyHalfWidth)) + (((dy - bodyCenter) / bodyHalfHeight) * ((dy - bodyCenter) / bodyHalfHeight))) <= 1f;

        // The shoulders curving in from the bulge to the neck.
        const float shoulderTop = -2f;
        const float neckHalfWidth = 1.8f;
        var shoulders = dy is >= shoulderTop and <= bodyCenter
            && Mathf.Abs(dx) <= neckHalfWidth + (Mathf.Pow((dy - shoulderTop) / (bodyCenter - shoulderTop), 0.6f) * (bodyHalfWidth - neckHalfWidth));

        // The gathered neck, the cord round it standing proud of the cloth, and the cloth above
        // the cord fanning out into two ears.
        var neck = dy is >= -4.5f and < shoulderTop && Mathf.Abs(dx) <= 1f;
        var cord = dy is >= -3.5f and < -2.5f && Mathf.Abs(dx) <= 3.4f;
        var ears = dy is >= -8f and < -4.5f
            && Mathf.Abs(dx) <= 1f + (-4.5f - dy)
            && !(dy < -7f && Mathf.Abs(dx) < 1f);

        return body || shoulders || neck || cord || ears;
    }
}
