using Godot;
using ManyWinters.Core.Continuity;

namespace ManyWinters.Godot.Ui;

// Where inscriptions go once shown, in order and whole - the overlay carries only the title
// (InscriptionOverlay). The session-only first form of the chronicle in
// docs/chronicles-and-memory-architecture.md; the real one will be a view over graves and
// written records.
public partial class ChroniclePanel : FloatingPanel
{
    private const float Width = 460f;
    private const int TitleFontSize = 26;
    private const int LineFontSize = 16;
    private const int ChromeFontSize = 15;
    private const int LineSpacing = 4;
    private const int EntrySpacing = 18;

    private static readonly Color Ink = new(0.93f, 0.88f, 0.78f);

    public ChroniclePanel()
        : base("Chronicle")
    {
        CustomMinimumSize = new Vector2(Width, 0);
        Visible = false;
        Theme = InscriptionFont.BodyTheme(ChromeFontSize);
    }

    public override void _Ready()
    {
        base._Ready();
        Body.AddThemeConstantOverride("separation", EntrySpacing);
    }

    public void Add(Inscription inscription)
    {
        var entry = new VBoxContainer();
        entry.AddThemeConstantOverride("separation", LineSpacing);
        entry.AddChild(InscriptionFont.TitleLabel(inscription.Title, TitleFontSize, Ink));
        foreach (var line in inscription.Lines)
        {
            entry.AddChild(InscriptionFont.BodyLabel(line, LineFontSize, Ink));
        }

        Body.AddChild(entry);
    }

    public void Toggle() => Visible = !Visible;
}
