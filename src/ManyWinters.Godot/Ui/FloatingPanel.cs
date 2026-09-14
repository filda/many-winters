using Godot;

namespace ManyWinters.Godot.Ui;

// Window chrome: drag the title bar to move it. Callers add content to Body; this class owns only
// the frame. There is no fold button - every window here is opened and closed from the status bar,
// and a panel that can also be half-shut is a second state nobody asked for.
//
// `onPaper` picks which of the two the frame is made of: a page like the player's own card
// (PanelChrome.Parchment, weathered, dark ink), or the dark card the panels over the world use.
// The panel applies its own chrome either way, so nobody has to remember to pair the right
// stylebox with the right ink.
public partial class FloatingPanel(string title, bool onPaper = false) : PanelContainer
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

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", onPaper ? PanelChrome.Parchment() : PanelChrome.Background());

        // On paper the grain goes down first and the padding moves inside it, or the weathering
        // would stop short of the edge and leave a clean frame (see PanelChrome.Parchment).
        if (onPaper)
        {
            AddChild(PanelChrome.Grain());
        }

        var outer = new VBoxContainer();
        AddChild(Wrapped(outer));

        var titleBar = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, TitleBarHeight),
        };
        titleBar.GuiInput += OnTitleBarInput;
        outer.AddChild(titleBar);

        _titleLabel = onPaper
            ? InscriptionFont.BodyLabel(title, TitleFontSize, InscriptionFont.DarkInk)
            : new Label { Text = title };
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleBar.AddChild(_titleLabel);

        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        outer.AddChild(_scroll);

        Body = new VBoxContainer();
        _scroll.AddChild(Body);
    }

    // What the window is called. A title that is a fact about the world - whose band this is -
    // changes with the world, so it is not fixed at construction.
    public void SetTitle(string text) => _titleLabel.Text = text;

    // Body height is its natural size, capped to the room left above BottomClearance so an
    // overlong body scrolls internally.
    public override void _Process(double delta)
    {
        var available = GetViewport().GetVisibleRect().Size.Y - Position.Y - TitleBarHeight - BottomClearance;
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
