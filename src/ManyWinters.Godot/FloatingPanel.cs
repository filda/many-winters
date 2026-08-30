using Godot;

namespace ManyWinters.Godot;

// Self-contained window chrome: drag the title bar to move it, click the fold button to
// collapse the body away and leave just the title bar. Callers add their own content to
// Body; this class only owns the frame around it.
public partial class FloatingPanel : PanelContainer
{
    private const float TitleBarHeight = 28f;

    // Reserves room below the panel for the status bar (see StatusBar.BarHeight) plus a
    // little breathing room, so a tall body scrolls instead of drawing over/past it.
    private const float BottomClearance = 56f;

    private Button _collapseButton = null!;
    private VBoxContainer _bodyWrapper = null!;
    private ScrollContainer _scroll = null!;
    private bool _collapsed;
    private bool _dragging;
    private Vector2 _dragOffset;
    private readonly string _title;

    public VBoxContainer Body { get; private set; } = null!;

    public FloatingPanel(string title)
    {
        _title = title;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        var outer = new VBoxContainer();
        AddChild(outer);

        var titleBar = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, TitleBarHeight),
        };
        titleBar.GuiInput += OnTitleBarInput;
        outer.AddChild(titleBar);

        var titleLabel = new Label
        {
            Text = _title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        titleBar.AddChild(titleLabel);

        _collapseButton = new Button { Text = "-", CustomMinimumSize = new Vector2(24, 0) };
        _collapseButton.Pressed += ToggleCollapsed;
        titleBar.AddChild(_collapseButton);

        _bodyWrapper = new VBoxContainer();
        outer.AddChild(_bodyWrapper);

        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _bodyWrapper.AddChild(_scroll);

        Body = new VBoxContainer();
        _scroll.AddChild(Body);
    }

    // Keeps the body's height at whatever it naturally needs, capped to whatever room is
    // left between the panel and the bottom of the screen - so it scrolls internally
    // instead of drawing past the visible area once it's too tall to fit.
    public override void _Process(double delta)
    {
        if (_collapsed)
        {
            return;
        }

        var available = GetViewport().GetVisibleRect().Size.Y - Position.Y - TitleBarHeight - BottomClearance;
        var desired = Body.GetCombinedMinimumSize().Y;
        _scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(desired, Mathf.Max(available, 0f)));
    }

    private void ToggleCollapsed()
    {
        _collapsed = !_collapsed;
        _bodyWrapper.Visible = !_collapsed;
        _collapseButton.Text = _collapsed ? "+" : "-";
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
