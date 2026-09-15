using Godot;
using ManyWinters.Core.Continuity;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// An inscription across the whole screen - the band's arrival (Prologue), the end of its line
// (Epitaph). The title, in InscriptionFont's title face with an ink outline so it reads over
// anything, and under it the inscription's closing words - which are the way on: the line
// itself is the button, framed in a thin stroke of ink, swelling a hair under the cursor
// (WordButton). The lines under those wait in ChroniclePanel. The world is left undimmed - the
// survivors, or the graves, are what the words are about - and the camera keeps working, while
// clicks into it are swallowed: a command issued into a stopped clock would land the moment it
// restarts. Never a modal dialog.
//
// "Another band comes" is offered only once nobody is left, framed the same way beside the
// closing words: a new band into this same world, not a scene reload.
public partial class InscriptionOverlay : Control
{
    private const int TitleFontSize = 60;

    // Where the title stops shrinking. Below this it has stopped being a title, and a word over
    // the edge of the screen says more than a whisper does.
    private const int MinTitleFontSize = 30;

    // How much of the screen's width the title may take. The rest is the outline (which
    // GetStringSize does not count) and room to breathe at both ends.
    private const float TitleWidthFraction = 0.88f;

    private const int ButtonFontSize = 18;

    // How much a word swells under the cursor. A hair: the line is an affordance, not a badge.
    private const int HoverGrowth = 2;

    private const float ColumnWidth = 900f;
    private const int Spacing = 36;

    private Label _title = null!;
    private Button _closing = null!;
    private Button _anotherBand = null!;

    // A click anywhere set the words down: the world may move on.
    public event Action? Dismissed;

    // "Another band comes" was pressed: a new band should replace the dead one in this world.
    public event Action? AnotherBandRequested;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        Theme = InscriptionFont.BodyTheme(ButtonFontSize);

        var centre = new CenterContainer();
        centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centre);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ColumnWidth, 0) };
        column.AddThemeConstantOverride("separation", Spacing);
        centre.AddChild(column);

        _title = InscriptionFont.OutlinedTitleLabel(string.Empty, TitleFontSize);
        // One line, always (see FittedTitleSize). Wrapped, an epitaph reads as two sentences and
        // its second half lands on the words under it.
        _title.AutowrapMode = TextServer.AutowrapMode.Off;
        column.AddChild(_title);

        _closing = WordButton(string.Empty, InscriptionFont.Ink, InscriptionFont.Ink);
        _closing.Pressed += () =>
        {
            Visible = false;
            Dismissed?.Invoke();
        };
        column.AddChild(_closing);

        var ways = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        ways.AddThemeConstantOverride("separation", Spacing / 2);
        column.AddChild(ways);

        _anotherBand = WordButton("Another band comes", new Color(InscriptionFont.Ink, 0.72f), InscriptionFont.Ink);
        _anotherBand.Pressed += () =>
        {
            Visible = false;
            AnotherBandRequested?.Invoke();
        };
        ways.AddChild(_anotherBand);

        // The growth is measured in the face the buttons actually draw with, which comes from
        // the overlay's theme - askable only on the tree, where that theme is reachable.
        ReserveGrowth(_closing);
        ReserveGrowth(_anotherBand);
    }

    public void Show(Inscription inscription, bool offerAnotherBand)
    {
        _title.Text = inscription.Title;
        _title.AddThemeFontSizeOverride("font_size", FittedTitleSize(inscription.Title));
        _closing.Text = inscription.Dismissal;
        // The growth is a hover state, not a memory: a line that was under the cursor when its
        // inscription was dismissed comes back resting.
        _closing.RemoveThemeFontSizeOverride("font_size");
        _anotherBand.RemoveThemeFontSizeOverride("font_size");
        _anotherBand.Visible = offerAnotherBand;
        Visible = true;
    }

    // The two choices on this screen are words, not controls - but bare words do not say they
    // can be taken, so each sits in a frame the way this page would draw one: a thin stroke of
    // the same light ink around a transparent middle. A filled slip of parchment was tried
    // first and read as an application button pasted onto the inscription; the bare line alone
    // read as prose. In the frame the words rest at their own ink and answer the cursor in
    // kind: the stroke fills to full, a breath of ink washes the middle, and the words swell a
    // hair. The closing words are the way on; the offer, when there is one, waits fainter
    // beside them.
    private static Button WordButton(string text, Color resting, Color lit)
    {
        var button = new Button { Text = text };
        button.AddThemeStyleboxOverride("normal", Frame(new Color(InscriptionFont.Ink, 0.45f), wash: 0f));
        button.AddThemeStyleboxOverride("hover", Frame(InscriptionFont.Ink, wash: 0.10f));
        button.AddThemeStyleboxOverride("pressed", Frame(InscriptionFont.Ink, wash: 0.18f));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        button.AddThemeColorOverride("font_color", resting);
        button.AddThemeColorOverride("font_focus_color", resting);
        button.AddThemeColorOverride("font_hover_color", lit);
        button.AddThemeColorOverride("font_pressed_color", lit);
        button.AddThemeColorOverride("font_hover_pressed_color", lit);
        button.AddThemeColorOverride("font_outline_color", InscriptionFont.Outline);
        button.AddThemeConstantOverride("outline_size", ButtonFontSize / 6);
        return button;
    }

    // A hand-drawn box: one thin line of ink, nothing in the middle (the wash is the hover's
    // arrival, at rest there is none).
    private static StyleBoxFlat Frame(Color border, float wash) => new()
    {
        BgColor = new Color(InscriptionFont.Ink, wash),
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 3,
        ContentMarginBottom = 3,
    };

    // The swell is a change of font size rather than of scale, so the letters stay crisp; the
    // line's slot is cut to the grown height plus its own frame up front, so nothing else on
    // the screen moves when a line swells.
    private static void ReserveGrowth(Button button)
    {
        var font = button.GetThemeFont("font");
        var frame = button.GetThemeStylebox("normal");
        button.CustomMinimumSize = new Vector2(0, font.GetHeight(ButtonFontSize + HoverGrowth) + frame.GetMinimumSize().Y);
        button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        button.MouseEntered += () => button.AddThemeFontSizeOverride("font_size", ButtonFontSize + HoverGrowth);
        button.MouseExited += () => button.RemoveThemeFontSizeOverride("font_size");
    }

    // Set smaller until the whole sentence fits across the screen, rather than wrapped or cut.
    // Measured rather than guessed at from the length: a band is named after its oldest member
    // (BandArrival.BandName), so the same epitaph is a different width every game and no phrasing
    // is short enough for all of them (see Epitaph). Against the screen, not ColumnWidth, which
    // is the measure of the paragraph the chronicle keeps rather than of the title.
    private int FittedTitleSize(string title)
    {
        var font = _title.GetThemeFont("font");
        var available = GetViewport().GetVisibleRect().Size.X * TitleWidthFraction;

        return TextFit.LargestThatFits(
            size => font.GetStringSize(title, HorizontalAlignment.Left, -1, size).X,
            TitleFontSize,
            MinTitleFontSize,
            available);
    }
}
