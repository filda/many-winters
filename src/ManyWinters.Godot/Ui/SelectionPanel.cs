using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// The player's own panel: who is selected, how they are doing, and what may be done with them.
// Docked to the right edge rather than floating, because it is open for most of the game and a
// window the player has to keep shoving aside is one they end up closing.
//
// Set in the game's own face (InscriptionFont) on paper (PanelChrome.Parchment): the game reads
// as a chronicle, so what the player holds is a page rather than a smoked-glass overlay. That
// inverts the ink - dark on pale here, where the panels over the world are light on dark. The
// debug inspector deliberately keeps the engine's default look, because it is a tool; this is
// part of the game.
//
// It holds no opinions of its own - what an action is called, whether it can run and why not all
// arrive as ActionOffer (see PersonActions), and the person's own card as SelectionCard. This
// class draws them and reports which one was pressed; the column of actions itself is the same
// control the contextual menu draws (ActionList).
internal partial class SelectionPanel : PanelContainer
{
    // Internal, because the band's roster is the same page on the other edge of the screen and
    // mirrors both (BandPanel).
    internal const float Width = 300f;
    internal const float Margin = 16f;

    private const int NameFontSize = 28;
    private const int BodyFontSize = 15;
    private const int MeterHeight = 8;
    private const int SectionSpacing = 10;

    // Between the name and the age beside it - a word's worth, not a column gap.
    private const int HeadingSpacing = 8;

    private Label _name = null!;
    private Label _beside = null!;
    private Label _parents = null!;
    private Label _task = null!;
    private VBoxContainer _meters = null!;
    private ActionList _actions = null!;
    private Button _carried = null!;
    private Label _death = null!;
    private VBoxContainer _knowledge = null!;
    private VBoxContainer _personBody = null!;
    private Label _graveRecord = null!;
    private VBoxContainer _column = null!;

    private readonly List<MeterRow> _meterRows = [];

    // Which action the player pressed. Main runs it: the panel knows what an offer is, not what
    // executing one means for the rest of the game.
    internal event Action<ActionOffer>? ActionInvoked;

    // The player asked to see the pack itself. Main opens the workshop over it.
    internal event Action? PackRequested;

    // The cross in the corner: nobody is selected any more. Main holds the selection, so it is
    // Main that lets it go - this card only says the player asked for it.
    internal event Action? CloseRequested;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        AddThemeStyleboxOverride("panel", PanelChrome.Parchment());
        // Added first, so every label and button that follows sits on top of the grain.
        AddChild(PanelChrome.Grain("detail"));
        Theme = PanelChrome.PaperButtons(BodyFontSize);

        // Hugs its content. Nothing is recomputed per frame - a height that chases the content
        // every frame is a height that flickers - and the card is short now that everything
        // aimed at the world has left for the contextual menu.
        AnchorLeft = 1f;
        AnchorRight = 1f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetLeft = -(Width + Margin);
        OffsetRight = -Margin;
        OffsetTop = Margin;

        // The padding lives here rather than in the StyleBox, so the grain above reaches the paper's
        // own edge instead of stopping at a clean frame (see PanelChrome.Parchment).
        var padding = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            padding.AddThemeConstantOverride(side, PanelChrome.PaperPadding);
        }

        AddChild(padding);

        // Width less the padding on both sides: the panel is anchored to a fixed width, and a
        // column that asks for the whole of it pushes the card off the right edge of the screen.
        _column = new VBoxContainer { CustomMinimumSize = new Vector2(Width - (PanelChrome.PaperPadding * 2), 0) };
        _column.AddThemeConstantOverride("separation", SectionSpacing);
        padding.AddChild(_column);

        // Name and the two facts that introduce a person on one line, the way anyone would be
        // introduced: the title face for what is theirs, quieter type for the rest.
        var heading = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        heading.AddThemeConstantOverride("separation", HeadingSpacing);
        _column.AddChild(heading);

        // The title face, as on an inscription or a grave: a person's name is the one part of
        // this card that is theirs rather than ours.
        _name = InscriptionFont.TitleLabel(string.Empty, NameFontSize, InscriptionFont.DarkInk);
        _name.AutowrapMode = TextServer.AutowrapMode.Off;
        _name.VerticalAlignment = VerticalAlignment.Bottom;
        heading.AddChild(_name);

        _beside = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.FadedDarkInk);
        _beside.AutowrapMode = TextServer.AutowrapMode.Off;
        _beside.VerticalAlignment = VerticalAlignment.Bottom;
        // Takes the rest of the line, so the cross ends up against the far edge of the card
        // rather than trailing the name.
        _beside.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.AddChild(_beside);

        var cross = PanelChrome.CloseCross(InscriptionFont.DarkInk);
        cross.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        cross.Pressed += () => CloseRequested?.Invoke();
        heading.AddChild(cross);

        _parents = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.FadedDarkInk);
        _column.AddChild(_parents);

        _death = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        _column.AddChild(_death);

        _personBody = new VBoxContainer();
        _personBody.AddThemeConstantOverride("separation", SectionSpacing);
        _column.AddChild(_personBody);

        _meters = new VBoxContainer();
        _personBody.AddChild(_meters);

        // A button, not a line of text: the pack is the way into the workshop, where what is in
        // it can be worked (see WorkshopPanel). Left-aligned and quiet, so it still reads as part
        // of the card rather than as a control shouting to be pressed.
        _carried = new Button { Alignment = HorizontalAlignment.Left, Flat = true };
        _carried.AddThemeColorOverride("font_color", InscriptionFont.FadedDarkInk);
        _carried.Pressed += () => PackRequested?.Invoke();
        _personBody.AddChild(_carried);

        _task = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        _personBody.AddChild(_task);

        _personBody.AddChild(Rule());

        _actions = new ActionList();
        _actions.ActionInvoked += offer => ActionInvoked?.Invoke(offer);
        _personBody.AddChild(_actions);

        _personBody.AddChild(Rule());

        _knowledge = new VBoxContainer();
        _personBody.AddChild(_knowledge);

        _graveRecord = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        _graveRecord.Visible = false;
        _column.AddChild(_graveRecord);
    }

    internal void ShowPerson(SelectionCard card, IReadOnlyList<ActionOffer> offers)
    {
        Visible = true;
        _personBody.Visible = true;
        _graveRecord.Visible = false;

        _name.Text = card.Name;
        _beside.Text = card.Beside;
        _parents.Text = card.Parents;
        _parents.Visible = card.Parents.Length > 0;
        _death.Text = card.Death;
        _death.Visible = card.Death.Length > 0;
        _carried.Text = $"Pack: {card.Carried}";
        _task.Text = $"Doing: {card.Task}";
        _task.Visible = card.Task.Length > 0;

        SyncKnowledge(card.KnowledgeLabel, card.Knowledge);

        SyncMeters(card.Meters);
        _actions.Show(offers);
    }

    internal void ShowGrave(string record)
    {
        Visible = true;
        _personBody.Visible = false;
        _graveRecord.Visible = true;

        _name.Text = "Grave";
        _beside.Text = string.Empty;
        _parents.Visible = false;
        _death.Visible = false;
        _graveRecord.Text = record;
    }

    internal void ClearSelection() => Visible = false;

    // A line per skill under its own heading, rather than one comma-spliced sentence. Rebuilt on
    // each refresh: a person learns something perhaps once a minute, so there is nothing here to
    // blink.
    private void SyncKnowledge(string label, IReadOnlyList<string> known)
    {
        foreach (var child in _knowledge.GetChildren())
        {
            child.QueueFree();
        }

        // "nothing yet" for the living, who may still learn; "nothing" for the dead, who will not.
        var heading = known.Count > 0 ? $"{label}:" : $"{label}: {(label == "Knows" ? "nothing yet" : "nothing")}";
        _knowledge.AddChild(InscriptionFont.BodyLabel(heading, BodyFontSize, InscriptionFont.FadedDarkInk));

        foreach (var skill in known)
        {
            var line = InscriptionFont.BodyLabel($"  {skill}", BodyFontSize, InscriptionFont.FadedDarkInk);
            _knowledge.AddChild(line);
        }
    }

    // Updated in place, never freed and rebuilt: the panel refreshes on every tick, and tearing
    // the bars down and putting them back is what made the card blink.
    private void SyncMeters(IReadOnlyList<MeterReading> readings)
    {
        while (_meterRows.Count < readings.Count)
        {
            _meterRows.Add(NewMeter());
        }

        for (var i = 0; i < _meterRows.Count; i++)
        {
            _meterRows[i].Apply(i < readings.Count ? readings[i] : null);
        }
    }

    private MeterRow NewMeter()
    {
        var container = new VBoxContainer();
        _meters.AddChild(container);

        var caption = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        container.AddChild(caption);

        var bar = PanelChrome.MeterBar(MeterHeight);
        container.AddChild(bar);

        return new MeterRow(container, caption, bar);
    }

    // A hairline in the ink; the engine's own separator draws a grey bevel.
    private static HSeparator Rule()
    {
        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(InscriptionFont.DarkInk, 0.28f) });
        return rule;
    }

    // One measure's caption and bar. Same shape as an action's row (ActionList): kept between
    // refreshes, and given new numbers.
    private sealed class MeterRow(VBoxContainer container, Label caption, ProgressBar bar)
    {
        public void Apply(MeterReading? reading)
        {
            container.Visible = reading is not null;
            if (reading is not { } shown)
            {
                return;
            }

            caption.Text = shown.Label;
            bar.Value = shown.Fraction;
            bar.AddThemeStyleboxOverride("fill", PanelChrome.Filled(shown.Fill));
        }
    }
}
