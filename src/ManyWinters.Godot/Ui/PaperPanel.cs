using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// Where a paper panel sits. Dragged: wherever the player puts it, by its title bar (BandPanel,
// ChroniclePanel). Centred: the middle of the screen for as long as it is up, which is a place
// rather than a starting point, so it is not dragged (WorkshopPanel). Docked: pinned to an edge
// by the panel's own anchors, open for most of the game, so it is not dragged either
// (SelectionPanel).
public enum PanelPlacement
{
    Dragged,
    Centred,
    Docked,
}

// Every page the player holds: weathered paper, the title in the title face, the cross in the
// corner, and a body that scrolls once it outgrows the screen. Callers add content to Body; this
// class owns the frame, and the frame is the same for all of them - there are no knobs for how a
// title looks, because a knob per panel is how every panel ended up looking different. The debug
// inspector is the one window that keeps the engine's look, and it has a frame of its own.
//
// There is no fold button - a panel that can also be half-shut is a second state nobody asked for.
public partial class PaperPanel(string title, float? fixedBodyHeight = null) : PanelContainer
{
    // One size for every panel's title. Protected, because a panel that lays its title inside
    // something of its own (SelectionPanel) has to know how tall a line of it is.
    protected const int TitleFontSize = 22;

    private const float TitleBarHeight = 28f;

    // Room below the panel for the status bar (StatusBar.BarHeight) plus breathing space, so a
    // tall body scrolls instead of drawing over it.
    private const float BottomClearance = 56f;

    private Label _titleLabel = null!;
    private HBoxContainer _titleBar = null!;
    private ScrollContainer _scroll = null!;
    private bool _dragging;
    private Vector2 _dragOffset;

    public VBoxContainer Body { get; private set; } = null!;

    // Centred stays centred through everything that moves the ground under it - a fullscreen
    // toggle, a window resize, its own content growing as the pack changes - which is why it is
    // checked every frame rather than placed once on opening.
    protected PanelPlacement Placement { get; init; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", PanelChrome.Parchment());

        // The grain goes down first and the padding moves inside it, or the weathering would stop
        // short of the edge and leave a clean frame. Which panel this is decides how its page
        // aged, so no two are stained alike - its type rather than its title, because a title can
        // be empty until somebody is shown on it.
        AddChild(PanelChrome.Grain(GetType().Name));

        var padding = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            padding.AddThemeConstantOverride(side, PanelChrome.PaperPadding);
        }

        AddChild(padding);

        var outer = new VBoxContainer();
        padding.AddChild(outer);

        _titleBar = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, TitleBarHeight),
        };

        // Only a panel the player places is dragged: a centred one would be pulled back by the
        // next frame's centring, and a docked one belongs to its edge - a title bar that fights
        // back is worse than one that does nothing at all.
        if (Placement == PanelPlacement.Dragged)
        {
            _titleBar.GuiInput += OnTitleBarInput;
        }

        outer.AddChild(_titleBar);

        _titleLabel = InscriptionFont.TitleLabel(title, TitleFontSize, InscriptionFont.DarkInk);
        var heading = Heading(_titleLabel);
        heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _titleBar.AddChild(heading);

        // Room for a panel to put something of its own beside its title - actions that read on
        // the current selection rather than on the window as a whole (WorkshopPanel).
        BuildTitleBarExtras(_titleBar);

        // Centred on the first line of the title bar rather than on the whole of it, so a heading
        // that runs to several lines (PersonDetailPanel's portrait) keeps the cross in the corner.
        var crossLine = new CenterContainer
        {
            CustomMinimumSize = new Vector2(0, TitleBarHeight),
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
        };
        _titleBar.AddChild(crossLine);

        var cross = PanelChrome.CloseCross(InscriptionFont.DarkInk);
        cross.Pressed += OnCloseRequested;
        crossLine.AddChild(cross);

        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        outer.AddChild(_scroll);

        Body = new VBoxContainer();
        _scroll.AddChild(Body);
    }

    // What the cross in the corner does. Putting the window away is all most of them need; one
    // with something to settle on the way out overrides this (WorkshopPanel starts the clock).
    protected virtual void OnCloseRequested() => Visible = false;

    // What goes in the title bar where the title is: the title itself, unless a panel sets it
    // inside something of its own - the summary card makes the whole line a button that opens the
    // person's page. Called while the title bar is still being built, like BuildTitleBarExtras.
    protected virtual Control Heading(Label titleLabel) => titleLabel;

    // Nothing, unless a panel overrides it. Called while the title bar is still being built, so
    // it has to stand on its own rather than reach for fields the rest of _Ready has not created
    // yet.
    protected virtual void BuildTitleBarExtras(HBoxContainer titleBar)
    {
    }

    // What the window is called. A title that is a fact about the world - whose band this is,
    // who is selected - changes with the world, so it is not fixed at construction.
    public void SetTitle(string text) => _titleLabel.Text = text;

    public override void _Process(double delta)
    {
        FitBody();

        // Back down to what the window actually needs. A Control nobody lays out grows to meet a
        // minimum size and then keeps that size when the minimum falls again - which is how the
        // workbench ended up a tall sheet of blank paper with one line of pack on it, hanging off
        // the bottom of the screen, whatever the body under it asked for.
        ResetSize();

        if (Placement == PanelPlacement.Centred && Visible)
        {
            // After the body, never before: the window is centred on the height it has this
            // frame, or a pack that just grew would hang off the bottom of the screen until
            // something else moved it.
            Position = ScreenPlacement.Centred(GetCombinedMinimumSize(), GetViewport().GetVisibleRect().Size);
        }
    }

    // Body height is its natural size, capped to the room left over so an overlong body scrolls
    // internally. A centred window measures that room against the whole screen less the clearance
    // at both ends: measured from its own top, as any other window is, its height would decide
    // its position and its position its height, and the two would chase each other.
    //
    // A panel given a fixed height (WorkshopPanel) skips all of this: it is a workbench with a
    // fixed shape, not a page that grows and shrinks with what is currently laid on it, so its
    // scroll area is exactly that height whatever the body inside asks for.
    private void FitBody()
    {
        if (fixedBodyHeight is { } fixedHeight)
        {
            _scroll.CustomMinimumSize = new Vector2(0, fixedHeight);
            return;
        }

        var screenHeight = GetViewport().GetVisibleRect().Size.Y;
        var titleBarHeight = _titleBar.GetCombinedMinimumSize().Y;
        var available = Placement == PanelPlacement.Centred
            ? screenHeight - titleBarHeight - (BottomClearance * 2f)
            : screenHeight - Position.Y - titleBarHeight - BottomClearance;
        var desired = Body.GetCombinedMinimumSize().Y;
        _scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(desired, Mathf.Max(available, 0f)));
    }

    private void OnTitleBarInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton:
                _dragging = mouseButton.Pressed;
                if (_dragging)
                {
                    _dragOffset = GetGlobalMousePosition() - Position;
                }

                break;
            case InputEventMouseMotion when _dragging:
                Position = GetGlobalMousePosition() - _dragOffset;
                break;
        }
    }
}
