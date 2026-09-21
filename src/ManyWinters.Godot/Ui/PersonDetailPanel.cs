using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// Everything the summary card knows about the selected person, laid out with room to breathe
// rather than squeezed into the strip down the right edge - the page its "Knows" line opens once
// there is more to a person than a fixed-width column can hold at once. Not a different set of
// facts: the same card data and the same actions, just given the whole of the window instead of
// one column of it.
//
// Holds the clock while it is up, the same way the workbench does: the player asked for the
// whole screen to read this, not to keep half an eye on a world still moving behind it. The owner
// holds the clock for whichever of the two is visible.
public partial class PersonDetailPanel : FloatingPanel
{
    private const float Width = 360f;
    private const int BodyFontSize = 15;
    private const int TitleFontSize = 18;
    private const int MeterHeight = 8;
    private const int SectionSpacing = 10;

    private Label _beside = null!;
    private Label _parents = null!;
    private Label _death = null!;
    private Label _task = null!;
    private Button _carried = null!;
    private ActionList _actions = null!;
    private VBoxContainer _meters = null!;
    private MeterRows _meterRows = null!;
    private VBoxContainer _knowledge = null!;
    private Label _knowledgeHeading = null!;

    private readonly List<Label> _knowledgeLines = [];

    // The player asked to see the pack itself. Main opens the workshop over it, the same as from
    // the summary card.
    internal event Action? PackRequested;

    // Which action the player pressed. The owner runs it the same way it does one pressed on the
    // summary card.
    internal event Action<ActionOffer>? ActionInvoked;

    public PersonDetailPanel()
        : base(string.Empty, onPaper: true, titleFontSize: TitleFontSize)
    {
        CustomMinimumSize = new Vector2(Width, 0);
        KeepCentred = true;
        Visible = false;
        Theme = PanelChrome.PaperButtons(BodyFontSize);
    }

    public override void _Ready()
    {
        base._Ready();
        Body.AddThemeConstantOverride("separation", SectionSpacing);

        // The scroll area under a floating panel's title bar sizes itself to its content rather
        // than stretching it, which left every left-aligned line hugging the left edge with the
        // rest of the panel's width sitting empty beside it. Asking for the width the panel itself
        // was given, less the page's own padding, fixes that.
        Body.CustomMinimumSize = new Vector2(Width - (PanelChrome.PaperPadding * 2), 0);

        _beside = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.FadedDarkInk);
        Body.AddChild(_beside);

        _parents = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.FadedDarkInk);
        Body.AddChild(_parents);

        _death = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        Body.AddChild(_death);

        _meters = new VBoxContainer();
        _meters.AddThemeConstantOverride("separation", SectionSpacing);
        Body.AddChild(_meters);
        _meterRows = new MeterRows(_meters, MeterHeight, BodyFontSize);

        // A button, not a line of text: the pack is the way into the workshop, where what is in
        // it can be worked.
        _carried = new Button { Alignment = HorizontalAlignment.Left, Flat = true };
        _carried.AddThemeColorOverride("font_color", InscriptionFont.FadedDarkInk);
        _carried.Pressed += () => PackRequested?.Invoke();
        Body.AddChild(_carried);

        _task = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        Body.AddChild(_task);

        Body.AddChild(PanelChrome.Rule());

        _actions = new ActionList();
        _actions.ActionInvoked += offer => ActionInvoked?.Invoke(offer);
        Body.AddChild(_actions);

        Body.AddChild(PanelChrome.Rule());

        _knowledge = new VBoxContainer();
        Body.AddChild(_knowledge);

        _knowledgeHeading = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.FadedDarkInk);
        _knowledge.AddChild(_knowledgeHeading);
    }

    // Opened fresh for whoever the card belongs to.
    internal void Open(SelectionCard card, IReadOnlyList<ActionOffer> offers)
    {
        Show(card, offers);
        Visible = true;
    }

    // Redrawn whenever the card behind it is, so the page stays true while it is left open on a
    // person who is still living their life behind it.
    internal void Show(SelectionCard card, IReadOnlyList<ActionOffer> offers)
    {
        SetTitle(card.Name);
        _beside.Text = card.Beside;
        _parents.Text = card.Parents;
        _parents.Visible = card.Parents.Length > 0;
        _death.Text = card.Death;
        _death.Visible = card.Death.Length > 0;
        _carried.Text = $"Pack: {card.Carried}";
        _task.Text = $"Doing: {card.Task}";
        _task.Visible = card.Task.Length > 0;

        _meterRows.Sync(card.Meters);
        _actions.Show(offers);
        SyncKnowledge(card.KnowledgeLabel, card.Knowledge);
    }

    // The clock is held while the page is out, the same as the workbench's, so the cross cannot
    // simply hide it - the clock has to be let go too.
    protected override void OnCloseRequested() => Close();

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Closed?.Invoke();
    }

    // Put away, so the world can start moving again.
    internal event Action? Closed;

    // A line per skill under its own heading, rather than one comma-spliced sentence - the list
    // the summary card's "Knows" line used to draw itself, before it grew long enough to need a
    // page of its own. Lines are kept and updated in place, not thrown away and rebuilt, for the
    // same reason the actions beside them are: this is redrawn on every tick the page is left
    // open, and freeing a label mid-frame only to add its replacement back is what made the page
    // blink.
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
