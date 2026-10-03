using Godot;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Ui;

// The page about one person, given the whole of the window rather than one column of it: who they
// are and how they are on the left - likeness, name, measures, what they know - and on the right
// the workbench, where what they carry is worked over. The two sit on one page because what a
// person knows and what they can make of what they carry are one question, cause beside effect.
//
// Each half is a column with its own heading and its content straight under it, so the tall
// portrait on the left never pushes the pack on the right down to meet it.
//
// Holds the clock while it is up, the same way the pause page does: the player asked for the
// whole screen to work this over, not to keep half an eye on a world still moving behind it. The
// owner holds the clock for whichever of these pages is visible, and since nothing behind the page
// moves while it is open, it is redrawn only by what the player does on it.
//
// A page about somebody who has died is the left column alone: there is nothing left for them to
// carry or make, and the page narrows to what is left to say.
public partial class PersonDetailPanel : PaperPanel
{
    private const float LeftWidth = 360f;
    private const int BodyFontSize = 15;
    private const int MeterHeight = 8;
    private const int SectionSpacing = 10;

    // How tall the page is, whatever is on it: a fixed shape, so it does not lurch as the pack
    // fills and empties, each column scrolling within itself instead.
    private const float PageHeight = 330f;

    // Between the name and the age beside it - a word's worth, not a column gap.
    private const int HeadingSpacing = 8;

    // Big enough to tell one face from another, small enough to leave the column beside it room
    // for a meter.
    private const float PortraitSize = 104f;

    private readonly List<Label> _knowledgeLines = [];

    private PersonPortrait _portrait = null!;
    private Label _beside = null!;
    private Label _parents = null!;
    private Label _death = null!;
    private Label _task = null!;
    private MeterRows _meterRows = null!;
    private VBoxContainer _knowledge = null!;
    private Label _knowledgeHeading = null!;
    private VSeparator _rule = null!;

    public PersonDetailPanel()
        : base(string.Empty)
    {
        Bench = new WorkshopBench();
        Placement = PanelPlacement.Centred;
        Visible = false;
        Theme = PanelChrome.PaperButtons(BodyFontSize);
    }

    // Put away, so the world can start moving again.
    internal event Action? Closed;

    internal WorkshopBench Bench { get; }

    // Opened fresh for whoever the card belongs to.
    internal void Open(SelectionCard card)
    {
        Show(card);
        Visible = true;
    }

    // Redrawn whenever the owner changes something the page shows.
    internal void Show(SelectionCard card)
    {
        SetTitle(card.Name);
        _beside.Text = card.Beside;
        _portrait.Show(card.Look, card.IsAlive);
        _parents.Text = card.Parents;
        _parents.Visible = card.Parents.Length > 0;
        _death.Text = card.Death;
        _death.Visible = card.Death.Length > 0;
        _task.Text = card.Task;
        _task.Visible = card.Task.Length > 0;

        _meterRows.Sync(card.Fatigue is { } fatigue ? [.. card.Meters, fatigue] : card.Meters);
        SyncKnowledge(card.KnowledgeLabel, card.Knowledge);

        _rule.Visible = card.IsAlive;
        Bench.Root.Visible = card.IsAlive;
    }

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Closed?.Invoke();
    }

    // The whole page goes where the title would, the cross in the corner beside it, rather than
    // the person's heading in a title bar spanning both halves: a heading that spanned them would
    // be as tall as the portrait on both sides, and hold the workbench's pack down to match it.
    // The body under the title bar is left empty.
    protected override Control Heading(Label titleLabel)
    {
        var columns = new HBoxContainer { CustomMinimumSize = new Vector2(0, PageHeight) };
        columns.AddThemeConstantOverride("separation", SectionSpacing);

        columns.AddChild(PersonColumn(titleLabel));

        _rule = PanelChrome.VerticalRule();
        columns.AddChild(_rule);

        columns.AddChild(Bench.Root);
        return columns;
    }

    // The clock is held while the page is out, so the cross cannot simply hide it - the clock has
    // to be let go too.
    protected override void OnCloseRequested() => Close();

    // The person's half: their heading, then their measures and what they know straight under it.
    private VBoxContainer PersonColumn(Label titleLabel)
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(LeftWidth, 0) };
        column.AddThemeConstantOverride("separation", SectionSpacing);
        column.AddChild(PersonHeading(titleLabel));

        var meters = new VBoxContainer();
        column.AddChild(meters);
        _meterRows = new MeterRows(meters, MeterHeight, BodyFontSize);

        column.AddChild(PanelChrome.Rule());

        // Its own scroll, as the pack and the recipes have: a long list of skills stays inside its
        // column instead of lengthening the page.
        var knowledgeScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddChild(knowledgeScroll);

        _knowledge = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        knowledgeScroll.AddChild(_knowledge);

        _knowledgeHeading = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.FadedDarkInk);
        _knowledge.AddChild(_knowledgeHeading);

        return column;
    }

    // The heading opens the way a page about somebody does: their likeness in the top left corner
    // and who they are beside it - the name with age and sex on its own line, in quieter type, the
    // same as on the summary card, then whose child they are and how they died, then what they are
    // doing, where a page about somebody would name their trade. The name sits level with the top
    // of the face.
    private HBoxContainer PersonHeading(Label titleLabel)
    {
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", SectionSpacing);

        _portrait = new PersonPortrait(PortraitSize);
        head.AddChild(_portrait);

        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(column);

        var nameLine = new HBoxContainer();
        nameLine.AddThemeConstantOverride("separation", HeadingSpacing);
        column.AddChild(nameLine);

        titleLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        titleLabel.VerticalAlignment = VerticalAlignment.Bottom;
        nameLine.AddChild(titleLabel);

        _beside = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.FadedDarkInk);
        _beside.AutowrapMode = TextServer.AutowrapMode.Off;
        _beside.VerticalAlignment = VerticalAlignment.Bottom;
        nameLine.AddChild(_beside);

        _parents = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.FadedDarkInk);
        column.AddChild(_parents);

        _death = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        column.AddChild(_death);

        _task = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        column.AddChild(_task);

        return head;
    }

    // A line per skill under its own heading, rather than one comma-spliced sentence that stops
    // reading well once the list grows long. Lines are kept and updated in place, not thrown
    // away and rebuilt, so that a redraw never frees a label mid-frame only to add its
    // replacement back, which is what made the page blink.
    private void SyncKnowledge(string label, IReadOnlyList<string> known)
    {
        _knowledgeHeading.Text = known.Count > 0 ? $"{label}:" : $"{label}: {(label == "Knows" ? "nothing yet" : "nothing")}";

        while (_knowledgeLines.Count < known.Count)
        {
            var line = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.FadedDarkInk);
            _knowledge.AddChild(line);
            _knowledgeLines.Add(line);
        }

        for (var i = 0; i < _knowledgeLines.Count; i++)
        {
            var visible = i < known.Count;
            _knowledgeLines[i].Visible = visible;
            if (visible)
            {
                _knowledgeLines[i].Text = $"  {known[i]}";
            }
        }
    }
}
