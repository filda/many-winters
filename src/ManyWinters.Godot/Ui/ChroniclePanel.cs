using Godot;
using ManyWinters.Core.Continuity;

namespace ManyWinters.Godot.Ui;

// Where inscriptions go once shown, in order and whole - the overlay carries only the title.
// The session-only first form of the chronicle in docs/chronicles-and-memory-architecture.md;
// the real one will be a view over graves and written records.
public partial class ChroniclePanel : PaperPanel
{
    private const float Width = 460f;
    private const int EntryTitleFontSize = 26;
    private const int LineFontSize = 16;
    private const int ChromeFontSize = 15;
    private const int LineSpacing = 4;
    private const int EntrySpacing = 18;

    // Room for the scrollbar the body grows one of once there is more than a screenful.
    private const int ScrollbarWidth = 16;

    // What an entry may actually use. Without it every label wraps to its own minimum, which for
    // wrapped text is one character - the column of single letters this panel used to print.
    // Nothing up the chain hands a width down: the ScrollContainer sizes its content to the
    // content's own minimum.
    private const float TextWidth = Width - (PanelChrome.PaperPadding * 2) - ScrollbarWidth;

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
        var entry = new VBoxContainer { CustomMinimumSize = new Vector2(TextWidth, 0) };
        entry.AddThemeConstantOverride("separation", LineSpacing);
        entry.AddChild(InscriptionFont.TitleLabel(inscription.Title, EntryTitleFontSize, InscriptionFont.DarkInk));
        foreach (var line in inscription.Lines)
        {
            entry.AddChild(InscriptionFont.BodyLabel(line, LineFontSize, InscriptionFont.DarkInk));
        }

        Body.AddChild(entry);
    }

    public void Toggle() => Visible = !Visible;
}
