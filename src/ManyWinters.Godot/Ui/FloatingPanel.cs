using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// Window chrome: drag the title bar to move it - unless the window keeps the middle of the screen
// (KeepCentred), which is a place rather than a starting point - and put it away with the cross in
// its corner (or with Escape, for the windows Main closes that way). Callers add content to Body;
// this class owns only the frame. There is no fold button - a panel that can also be half-shut is
// a second state nobody asked for.
//
// `onPaper` picks which of the two the frame is made of: a page like the player's own card
// (PanelChrome.Parchment, weathered, dark ink), or the dark card the panels over the world use.
// The panel applies its own chrome either way, so nobody has to remember to pair the right
// stylebox with the right ink.
public partial class FloatingPanel(string title, bool onPaper = false, int? titleFontSize = null) : PanelContainer
{
    private const float TitleBarHeight = 28f;
    private const int TitleFontSize = 15;

    // Room below the panel for the status bar (StatusBar.BarHeight) plus breathing space, so a
    // tall body scrolls instead of drawing over it.
    private const float BottomClearance = 56f;

    private Label _titleLabel = null!;
    private ScrollContainer _scroll = null!;
    private bool _dragging;
    private Vector2 _dragOffset;

    public VBoxContainer Body { get; private set; } = null!;

    // A window that holds the middle of the screen for as long as it is up, rather than one the
    // player puts where they like (WorkshopPanel). It stays centred through everything that moves
    // the ground under it - a fullscreen toggle, a window resize, its own content growing as the
    // pack changes - which is why it is checked every frame rather than placed once on opening.
    protected bool KeepCentred { get; init; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", onPaper ? PanelChrome.Parchment() : PanelChrome.Background());

        // On paper the grain goes down first and the padding moves inside it, or the weathering
        // would stop short of the edge and leave a clean frame (see PanelChrome.Parchment).
        if (onPaper)
        {
            // The window's own title is what decides how its page aged, so no two windows
            // are stained alike (see PanelChrome.Grain).
            AddChild(PanelChrome.Grain(title));
        }

        var outer = new VBoxContainer();
        AddChild(Wrapped(outer));

        var titleBar = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, TitleBarHeight),
        };

        // A centred window is not dragged: it sits where it sits, and a drag would only be undone
        // by the next frame's centring - a title bar that fights back is worse than one that does
        // nothing at all.
        if (!KeepCentred)
        {
            titleBar.GuiInput += OnTitleBarInput;
        }

        outer.AddChild(titleBar);

        _titleLabel = onPaper
            ? InscriptionFont.BodyLabel(title, titleFontSize ?? TitleFontSize, InscriptionFont.DarkInk)
            : new Label { Text = title };
        if (!onPaper && titleFontSize is { } size)
        {
            _titleLabel.AddThemeFontSizeOverride("font_size", size);
        }

        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleBar.AddChild(_titleLabel);

        // Room for a panel to put something of its own beside its title - actions that read on
        // the current selection rather than on the window as a whole (WorkshopPanel).
        BuildTitleBarExtras(titleBar);

        var cross = PanelChrome.CloseCross(onPaper ? InscriptionFont.DarkInk : InscriptionFont.Ink);
        cross.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        cross.Pressed += OnCloseRequested;
        titleBar.AddChild(cross);

        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        outer.AddChild(_scroll);

        Body = new VBoxContainer();
        _scroll.AddChild(Body);
    }

    // What the cross in the corner does. Putting the window away is all most of them need; one
    // with something to settle on the way out overrides this (WorkshopPanel starts the clock).
    protected virtual void OnCloseRequested() => Visible = false;

    // Nothing, unless a panel overrides it. Called while the title bar is still being built, so
    // it has to stand on its own rather than reach for fields the rest of _Ready has not created
    // yet.
    protected virtual void BuildTitleBarExtras(HBoxContainer titleBar)
    {
    }

    // What the window is called. A title that is a fact about the world - whose band this is -
    // changes with the world, so it is not fixed at construction.
    public void SetTitle(string text) => _titleLabel.Text = text;

    public override void _Process(double delta)
    {
        FitBody();

        // Back down to what the window actually needs. A Control nobody lays out grows to meet a
        // minimum size and then keeps that size when the minimum falls again - which is how the
        // workbench ended up a tall sheet of blank paper with one line of pack on it, hanging off
        // the bottom of the screen, whatever the body under it asked for.
        ResetSize();

        if (KeepCentred && Visible)
        {
            // After the body, never before: the window is centred on the height it has this
            // frame, or a pack that just grew would hang off the bottom of the screen until
            // something else moved it.
            Position = ScreenPlacement.Centred(GetCombinedMinimumSize(), GetViewport().GetVisibleRect().Size);
        }
    }

    // Body height is its natural size, capped to the room left over so an overlong body scrolls
    // internally. A centred window measures that room against the whole screen less the clearance
    // at both ends: measured from its own top, as a window the player placed is, its height would
    // decide its position and its position its height, and the two would chase each other.
    private void FitBody()
    {
        var screenHeight = GetViewport().GetVisibleRect().Size.Y;
        var available = KeepCentred
            ? screenHeight - TitleBarHeight - (BottomClearance * 2f)
            : screenHeight - Position.Y - TitleBarHeight - BottomClearance;
        var desired = Body.GetCombinedMinimumSize().Y;
        _scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(desired, Mathf.Max(available, 0f)));
    }

    // The padding a page needs inside its own grain; the dark card carries its own in the
    // stylebox and wants nothing here.
    private Control Wrapped(Control content)
    {
        if (!onPaper)
        {
            return content;
        }

        var padding = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            padding.AddThemeConstantOverride(side, PanelChrome.PaperPadding);
        }

        padding.AddChild(content);
        return padding;
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
