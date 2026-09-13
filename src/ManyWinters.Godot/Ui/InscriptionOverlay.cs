using Godot;
using ManyWinters.Core.Continuity;

namespace ManyWinters.Godot.Ui;

// An inscription carved across the whole screen - the band's arrival (Prologue), the end of
// its line (Epitaph): the title stands alone in the middle of the undimmed world, set in the
// title page's own face (InscriptionFont) with an ink outline so it reads over whatever the
// camera happens to show. Only the title: one sentence is what a moment like this can carry,
// and the lines under it wait in the chronicle (ChroniclePanel). The world behind it is left
// as it is on purpose - the survivors, or the graves, are what the words are about - and the
// camera keeps working while it is up; only clicks into the world are swallowed, since a
// command issued into a stopped clock would land the moment it starts again.
// Under it, the ways on. "Walk the land" closes it and leaves the world running - the
// survivors, or the graves, are worth looking at. "Another band comes" is only offered once
// nobody is left, and is not wired yet: it will bring a new band into this same world
// (docs/todo/todo.md, permaworld), which is why it is a disabled placeholder here rather than
// a scene reload that would throw the world away. Never a modal dialog: the player must always
// be able to get back to the land behind it.
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
