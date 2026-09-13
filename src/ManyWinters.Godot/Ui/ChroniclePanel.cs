using Godot;
using ManyWinters.Core.Continuity;

namespace ManyWinters.Godot.Ui;

// Where the inscriptions go once they have been shown: the prologue, then whatever ends the
// band, in order, whole - the overlay only ever shows a title (see InscriptionOverlay), and
// this is where the rest of each is read. The first, session-only form of the chronicle
// docs/chronicles-and-memory-architecture.md describes; the real one will be a view over
// graves and written records rather than a list kept here.
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
