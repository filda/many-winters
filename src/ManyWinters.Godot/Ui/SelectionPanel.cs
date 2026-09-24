using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// The player's own panel: who is selected, how they are doing, and what may be done with them.
// Docked to the right edge rather than floating, because it is open for most of the game and a
// window the player has to keep shoving aside is one they end up closing.
//
// The same page as every other panel the player holds (PaperPanel), only docked rather than
// placed: the person's name is the page's title, in the same face and size as any other title.
//
// It holds no opinions of its own - what an action is called, whether it can run and why not all
// arrive as ActionOffer, and the person's own card as SelectionCard. This class draws them and
// reports which one was pressed; the column of actions itself is the same control the contextual
// menu draws.
internal partial class SelectionPanel : PaperPanel
{
    // Internal, because the band's roster is the same page on the other edge of the screen and
    // mirrors both.
    internal const float Width = 300f;
    internal const float Margin = 16f;

    private const int BodyFontSize = 15;
    private const int MeterHeight = 8;
    private const int SectionSpacing = 10;

    // Between the name and the age beside it - a word's worth, not a column gap.
    private const int HeadingSpacing = 8;

    // The whole line is one button - a Button is no container, so it is told how tall the title
    // face makes a line.
    private const int HeadingHeight = TitleFontSize + 8;

    private Button _heading = null!;
    private TextureRect _detailIcon = null!;
    private Label _beside = null!;
    private Label _task = null!;
    private VBoxContainer _meters = null!;
    private ActionList _actions = null!;
    private PackLine _carried = null!;
    private Label _death = null!;
    private VBoxContainer _personBody = null!;
    private Label _graveRecord = null!;

    private MeterRows _meterRows = null!;

    // Which action the player pressed. Main runs it: the panel knows what an offer is, not what
    // executing one means for the rest of the game.
    internal event Action<ActionOffer>? ActionInvoked;

    // The player asked to see the pack itself. Main opens the workshop over it.
    internal event Action? PackRequested;

    // The player pressed the name: everything the card knows about this person, laid out with
    // room to breathe instead of squeezed into this fixed-width column.
    internal event Action? DetailRequested;

    // The cross in the corner: nobody is selected any more. Main holds the selection, so it is
    // Main that lets it go - this card only says the player asked for it.
    internal event Action? CloseRequested;

    public SelectionPanel()
        : base(string.Empty)
    {
        Placement = PanelPlacement.Docked;
        Visible = false;
        Theme = PanelChrome.PaperButtons(BodyFontSize);

        // Pinned to the right edge at a fixed width, hugging its content downwards.
        AnchorLeft = 1f;
        AnchorRight = 1f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetLeft = -(Width + Margin);
        OffsetRight = -Margin;
        OffsetTop = Margin;
    }

    public override void _Ready()
    {
        base._Ready();
        Body.AddThemeConstantOverride("separation", SectionSpacing);

        // Width less the padding on both sides: the panel is anchored to a fixed width, and a
        // body that asks for the whole of it pushes the card off the right edge of the screen.
        Body.CustomMinimumSize = new Vector2(Width - (PanelChrome.PaperPadding * 2), 0);

        _death = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        Body.AddChild(_death);

        _personBody = new VBoxContainer();
        _personBody.AddThemeConstantOverride("separation", SectionSpacing);
        Body.AddChild(_personBody);

        _meters = new VBoxContainer();
        _personBody.AddChild(_meters);
        _meterRows = new MeterRows(_meters, MeterHeight, BodyFontSize);

        _carried = new PackLine(BodyFontSize, () => PackRequested?.Invoke());
        _personBody.AddChild(_carried.Root);

        _task = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        _personBody.AddChild(_task);

        _personBody.AddChild(PanelChrome.Rule());

        _actions = new ActionList();
        _actions.ActionInvoked += offer => ActionInvoked?.Invoke(offer);
        _personBody.AddChild(_actions);

        _graveRecord = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        _graveRecord.Visible = false;
        Body.AddChild(_graveRecord);
    }

    // Main holds the selection, so the cross only says the player asked for it to go.
    protected override void OnCloseRequested() => CloseRequested?.Invoke();

    // The name and the two facts that introduce a person on one line, the way anyone would be
    // introduced: the title for what is theirs, quieter type for the rest.
    //
    // The whole line is the button, the same way a row of the band's own roster is - the
    // highlight the player already reads there says the same thing here: this name opens
    // something too. Both texts ride on the button's rect rather than being laid out by it (a
    // Button is no container), so a margin holds them where a button's own caption would sit.
    protected override Control Heading(Label titleLabel)
    {
        _heading = new Button
        {
            Text = string.Empty,
            CustomMinimumSize = new Vector2(0, HeadingHeight),
        };
        _heading.Pressed += () => DetailRequested?.Invoke();

        var headingPadding = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        headingPadding.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        headingPadding.AddThemeConstantOverride("margin_left", PanelChrome.FilledPadding);
        headingPadding.AddThemeConstantOverride("margin_right", PanelChrome.FilledPadding);
        _heading.AddChild(headingPadding);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", HeadingSpacing);
        headingPadding.AddChild(row);

        titleLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        titleLabel.VerticalAlignment = VerticalAlignment.Bottom;
        titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(titleLabel);

        _beside = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.FadedDarkInk);
        _beside.AutowrapMode = TextServer.AutowrapMode.Off;
        _beside.VerticalAlignment = VerticalAlignment.Bottom;
        _beside.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(_beside);

        // Pushes the icon below to the far end of the line, clear of a long name.
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        // A magnifying glass, the plain shape for "look closer": a hint that the line opens a
        // page, not a second way in of its own. Takes no mouse itself, so hovering it is hovering
        // the button underneath - the whole line lights up together rather than the icon alone.
        _detailIcon = new TextureRect
        {
            Texture = DetailGlass(),
            CustomMinimumSize = new Vector2(DetailIconSize, DetailIconSize),
            MouseFilter = MouseFilterEnum.Ignore,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
        };
        row.AddChild(_detailIcon);

        // Pulled out to the left by the box's own padding, so the name starts flush with the lines
        // under it while the highlight still has room around it - the same as the pack line. Not on
        // the right, where the cross sits beside it.
        var flush = new MarginContainer();
        flush.AddThemeConstantOverride("margin_left", -PanelChrome.FilledPadding);
        flush.AddChild(_heading);
        return flush;
    }

    internal void ShowPerson(SelectionCard card, IReadOnlyList<ActionOffer> offers)
    {
        Visible = true;
        _personBody.Visible = true;
        _graveRecord.Visible = false;
        _heading.Disabled = false;
        _detailIcon.Visible = true;

        SetTitle(card.Name);
        _beside.Text = card.Beside;
        _death.Text = card.Death;
        _death.Visible = card.Death.Length > 0;
        _carried.Show(card.Carried);
        _task.Text = $"Doing: {card.Task}";
        _task.Visible = card.Task.Length > 0;

        _meterRows.Sync(card.Meters);
        _actions.Show(offers);
    }

    internal void ShowGrave(string record)
    {
        Visible = true;
        _personBody.Visible = false;
        _graveRecord.Visible = true;
        // Nothing behind a grave for the detail page to say - the heading stops answering to a
        // press rather than opening a page about nobody.
        _heading.Disabled = true;
        _detailIcon.Visible = false;

        SetTitle("Grave");
        _beside.Text = string.Empty;
        _death.Visible = false;
        _graveRecord.Text = record;
    }

    internal void ClearSelection() => Visible = false;

    // A magnifying glass, drawn rather than loaded off disk - the same reasoning as the workshop's
    // own icons: nobody has painted this yet, and a plain shape in the page's own ink is a better
    // placeholder than a word small enough to look like a toolbar.
    private const int DetailIconSize = 18;

    private static ImageTexture DetailGlass()
    {
        const float lensCenter = -3f;
        const float lensRadius = 6f;
        const float ringThickness = 1.6f;

        var image = Image.CreateEmpty(DetailIconSize, DetailIconSize, false, Image.Format.Rgba8);
        for (var y = 0; y < DetailIconSize; y++)
        {
            for (var x = 0; x < DetailIconSize; x++)
            {
                var dx = x - (DetailIconSize / 2f) + 0.5f;
                var dy = y - (DetailIconSize / 2f) + 0.5f;

                var fromLensCenter = new Vector2(dx - lensCenter, dy - lensCenter).Length();
                // The ring the lens is drawn as, and the short diagonal handle running from its
                // rim to the corner - the one shape "look closer" is drawn with everywhere.
                var onRing = Mathf.Abs(fromLensCenter - lensRadius) <= ringThickness / 2f;
                var onHandle = dx is >= 3f and <= 8f && dy is >= 3f and <= 8f && Mathf.Abs(dx - dy) <= 1.7f;
                if (onRing || onHandle)
                {
                    image.SetPixel(x, y, InscriptionFont.FadedDarkInk);
                }
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
