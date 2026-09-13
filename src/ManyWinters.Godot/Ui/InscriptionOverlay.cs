using Godot;
using ManyWinters.Core.Continuity;

namespace ManyWinters.Godot.Ui;

// An inscription carved across the whole screen - the band's arrival (Prologue), the end of
// its line (Epitaph): the world dims to a sepia wash and the title stands alone in the middle,
// set in the title page's own face (InscriptionFont). Only the title: one sentence is what a
// moment like this can carry, and the lines under it wait in the chronicle (ChroniclePanel).
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

    private static readonly Color Wash = new(0.16f, 0.12f, 0.08f, 0.88f);
    private static readonly Color Ink = new(0.93f, 0.88f, 0.78f);

    private Label _title = null!;
    private Button _anotherBand = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        Theme = InscriptionFont.BodyTheme(ButtonFontSize);

        var wash = new ColorRect { Color = Wash, MouseFilter = MouseFilterEnum.Stop };
        wash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(wash);

        var centre = new CenterContainer();
        centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centre);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ColumnWidth, 0) };
        column.AddThemeConstantOverride("separation", Spacing);
        centre.AddChild(column);

        _title = InscriptionFont.TitleLabel(string.Empty, TitleFontSize, Ink);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_title);

        var ways = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        ways.AddThemeConstantOverride("separation", Spacing / 2);
        column.AddChild(ways);

        var walk = new Button { Text = "Walk the land" };
        walk.Pressed += () => Visible = false;
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
