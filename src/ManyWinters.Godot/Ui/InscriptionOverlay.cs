using Godot;
using ManyWinters.Core.Continuity;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// An inscription across the whole screen - the band's arrival (Prologue), the end of its line
// (Epitaph). Only the title, in InscriptionFont's title face with an ink outline so it reads
// over anything; the lines under it wait in ChroniclePanel. The world is left undimmed - the
// survivors, or the graves, are what the words are about - and the camera keeps working; only
// clicks into the world are swallowed, since a command issued into a stopped clock would land
// the moment it restarts. Never a modal dialog.
//
// "Walk the land" dismisses it. "Another band comes" is offered only once nobody is left and
// stays a disabled placeholder until permaworld lands (docs/todo/todo.md): a new band into this
// same world, not a scene reload.
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
    private const float ColumnWidth = 900f;
    private const int Spacing = 36;

    private Label _title = null!;
    private Button _anotherBand = null!;

    // "Walk the land" was pressed: the world may move on.
    public event Action? Dismissed;

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
        // its second half lands on the buttons under it.
        _title.AutowrapMode = TextServer.AutowrapMode.Off;
        column.AddChild(_title);

        var ways = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        ways.AddThemeConstantOverride("separation", Spacing / 2);
        column.AddChild(ways);

        var walk = new Button { Text = "Walk the land" };
        walk.Pressed += () =>
        {
            Visible = false;
            Dismissed?.Invoke();
        };
        ways.AddChild(walk);

        _anotherBand = new Button
        {
            Text = "Another band comes",
            Disabled = true,
            TooltipText = "Not yet. A new band will arrive into this same land, near where the last one died.",
        };
        ways.AddChild(_anotherBand);
    }

    public void Show(Inscription inscription, bool offerAnotherBand)
    {
        _title.Text = inscription.Title;
        _title.AddThemeFontSizeOverride("font_size", FittedTitleSize(inscription.Title));
        _anotherBand.Visible = offerAnotherBand;
        Visible = true;
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
