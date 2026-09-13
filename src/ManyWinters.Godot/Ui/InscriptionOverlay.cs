using Godot;
using ManyWinters.Core.Continuity;

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
        _anotherBand.Visible = offerAnotherBand;
        Visible = true;
    }
}
