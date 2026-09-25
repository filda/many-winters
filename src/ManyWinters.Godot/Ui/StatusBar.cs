using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Ui;

// Bottom-of-screen bar: transient notifications (left, auto-clearing), the buttons that open the
// game's windows, performance and tick/season (right), and the "?" that opens the controls page.
// Every window is opened and closed from here, the debug inspector included - none of them is on
// screen until the player asks for it.
public partial class StatusBar : PanelContainer
{
    // Room for the "?" button (which drives its own theme minimum height) and a centred line
    // of text inside PanelChrome's 10px top/bottom margins.
    private const float BarHeight = 48f;
    private const float NotificationSeconds = 4f;

    // Engine.GetFramesPerSecond only changes once a second; a few reads a second catch every
    // value, and the per-frame render monitors stay readable rather than a flicker of digits.
    private const double PerformanceRefreshSeconds = 0.25;

    private Label _notificationLabel = null!;
    private Button _bandButton = null!;
    private Button _chronicleButton = null!;
    private Button _inspectorButton = null!;
    private Label _performanceLabel = null!;
    private Label _tickLabel = null!;
    private double _sincePerformanceRefresh;
    private global::Godot.Timer _notificationTimer = null!;

    public override void _Ready()
    {
        // Both vertical anchors at 1 and OffsetTop = -BarHeight: SetAnchorsPreset without
        // offsets would collapse the rect to zero height.
        AnchorLeft = 0f;
        AnchorRight = 1f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetLeft = 0f;
        OffsetRight = 0f;
        OffsetTop = -BarHeight;
        OffsetBottom = 0f;

        var row = new HBoxContainer();
        AddChild(row);

        _notificationLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.AddChild(_notificationLabel);

        // Always there, unlike the chronicle button: the player can lose track of where their
        // people went from the first minute, and there is always a band.
        _bandButton = new Button { Text = "Band" };
        _bandButton.Pressed += () => BandRequested?.Invoke();
        row.AddChild(_bandButton);

        // Where each inscription can be read whole - the overlay only carries the title. Hidden
        // until there is one.
        _chronicleButton = new Button { Text = "Chronicle", Visible = false };
        _chronicleButton.Pressed += () => ChronicleRequested?.Invoke();
        row.AddChild(_chronicleButton);

        // The world's raw numbers and the levers that move them - a tool, not part of the game,
        // so it sits at the end of the row and starts shut.
        _inspectorButton = new Button { Text = "Inspector" };
        _inspectorButton.Pressed += () => InspectorRequested?.Invoke();
        row.AddChild(_inspectorButton);
        row.AddChild(new VSeparator());

        // Frame rate sits permanently next to the tick rather than behind a debug key, with
        // object and draw-call counts to tell whether a low number is the scene's size.
        _performanceLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
        row.AddChild(_performanceLabel);
        row.AddChild(new VSeparator());

        _tickLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
        row.AddChild(_tickLabel);

        var helpButton = new Button { Text = "?", CustomMinimumSize = new Vector2(28, 0) };
        helpButton.Pressed += () => HelpRequested?.Invoke();
        row.AddChild(helpButton);

        _notificationTimer = new global::Godot.Timer { WaitTime = NotificationSeconds, OneShot = true };
        _notificationTimer.Timeout += () => _notificationLabel.Text = string.Empty;
        AddChild(_notificationTimer);
    }

    public override void _Process(double delta)
    {
        _sincePerformanceRefresh += delta;
        if (_sincePerformanceRefresh < PerformanceRefreshSeconds)
        {
            return;
        }

        _sincePerformanceRefresh = 0;

        // The live counters vary second to second, so a still session keeps them blank rather
        // than letting a frame captured twice differ in its own corner.
        if (LaunchOptions.Still)
        {
            return;
        }

        // Objects counts what the renderer drew after frustum culling - what the camera sees,
        // not what exists.
        var objects = Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame);
        var drawCalls = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        _performanceLabel.Text = $"FPS: {Engine.GetFramesPerSecond():0}  Objects: {objects:0}  Draw calls: {drawCalls:0}";
    }

    // Transient feedback ("select a person first", "too far away"); the inspector shows only
    // the selection's persistent state.
    public void Notify(string message)
    {
        _notificationLabel.Text = message;
        _notificationTimer.Start();
    }

    public event Action? BandRequested;

    public event Action? ChronicleRequested;

    public event Action? InspectorRequested;

    public event Action? HelpRequested;

    public void ShowChronicleButton() => _chronicleButton.Visible = true;

    public void SetTick(long tick, Season season)
    {
        _tickLabel.Text = $"Tick: {tick}  Season: {season}";
    }
}
