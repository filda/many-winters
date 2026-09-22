using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Ui;

// The screen's own furniture: the status bar, the debug inspector, the chronicle, the
// inscription overlay, and the pause and help pages. What a page is for the world - whether it
// holds the clock, whether it blocks a second one opening on top of it - is decided in exactly
// one registry here rather than read off scattered `Control.Visible` checks.
//
// SelectionController, WorkshopController, and WorldInputController keep owning their own
// controls; they attach them to Canvas and register them here explicitly (RegisterModal)
// instead of this type reaching into them.
internal sealed class MainUi
{
    // Every full-screen page or window that asks for the player's whole attention, tagged with
    // what that means for it. `HoldsClock` says whether it stops the world while it is up (see
    // SimulationLoop.Update); `BlocksPause` says whether its being up should stop Space from
    // opening a second window on top of it (see TogglePause). The pause panel holds the clock
    // but is not its own blocker - TogglePause decides what pressing Space does to the one
    // already up, not whether it is allowed to be up at all.
    private readonly record struct ModalWindow(Control Control, bool HoldsClock, bool BlocksPause);

    private readonly List<ModalWindow> _modals = [];
    private readonly PausePanel _pausePanel;
    private readonly HelpPanel _helpPanel;

    // Raised whenever a clock-holding page this type owns comes down (pause dismissed, help
    // dismissed, an inscription dismissed). Composition code primes the tick accumulator; this
    // type has no simulation clock of its own to prime.
    public event Action? ClockShouldResume;

    public CanvasLayer Canvas { get; }

    public StatusBar StatusBar { get; }

    public ChroniclePanel Chronicle { get; private set; } = null!;

    public InscriptionOverlay InscriptionOverlay { get; private set; } = null!;

    public DebugInspector Inspector { get; }

    // True while any registered control that holds the clock is visible.
    public bool HoldsClock => _modals.Any(modal => modal.HoldsClock && modal.Control.Visible);

    // True while any registered control that should keep the pause page from opening on top of
    // it is visible.
    public bool BlocksPause => _modals.Any(modal => modal.BlocksPause && modal.Control.Visible);

    public MainUi(Node parent, WorldState world, PresentationSettings presentation)
    {
        Canvas = new CanvasLayer();
        parent.AddChild(Canvas);

        // First: it carries the buttons the windows below hang their own toggles on.
        StatusBar = new StatusBar();
        StatusBar.AddThemeStyleboxOverride("panel", PanelChrome.Background());
        Canvas.AddChild(StatusBar);
        StatusBar.SetTick(world.Clock.CurrentTick, world.CurrentSeason);

        Inspector = new DebugInspector(Canvas, StatusBar, presentation);

        // Built here so callers that only need Show/Record never construct a PausePanel or
        // HelpPanel of their own; the fields stay private since nothing outside this type reads
        // their visibility directly (see HoldsClock/BlocksPause).
        _pausePanel = new PausePanel();
        _helpPanel = new HelpPanel();
    }

    // Called once SelectionController has added its own pieces to the canvas - the chronicle
    // sits after the roster and the selection card in the canvas order today.
    public void AttachChronicle()
    {
        Chronicle = new ChroniclePanel
        {
            Position = new Vector2(Canvas.GetViewport().GetVisibleRect().Size.X - 476f, 16f),
        };
        Canvas.AddChild(Chronicle);
        StatusBar.ChronicleRequested += Chronicle.Toggle;
        RegisterModal(Chronicle, holdsClock: false, blocksPause: true);
    }

    // Called last, after the workbench, the detail page, and world input have all added their
    // own controls - inscriptions and the pause/help pages draw over everything else.
    public void AttachOverlaysAndPauseHelp()
    {
        InscriptionOverlay = new InscriptionOverlay();
        // The clock stood still, so the next tick is due the moment the inscription comes down -
        // a full interval later read as the world taking a second to notice.
        InscriptionOverlay.Dismissed += () => ClockShouldResume?.Invoke();
        Canvas.AddChild(InscriptionOverlay);
        RegisterModal(InscriptionOverlay, holdsClock: true, blocksPause: true);

        // The cross on the page is the other half of Space: both let the world go again.
        _pausePanel.Resumed += HidePause;
        Canvas.AddChild(_pausePanel);
        RegisterModal(_pausePanel, holdsClock: true, blocksPause: false);

        // Last of all, so the controls can be read over whatever else is up. Opened and closed
        // by the "?" on the status bar or by Escape; like an inscription being dismissed,
        // letting it go primes the tick accumulator so the world starts again on the next frame
        // rather than a full interval later.
        _helpPanel.Dismissed += () => ClockShouldResume?.Invoke();
        Canvas.AddChild(_helpPanel);
        StatusBar.HelpRequested += _helpPanel.Toggle;
        RegisterModal(_helpPanel, holdsClock: true, blocksPause: true);
    }

    // For controls SelectionController, WorkshopController, and WorldInputController keep
    // owning: their visibility still has to count toward HoldsClock/BlocksPause.
    public void RegisterModal(Control control, bool holdsClock, bool blocksPause) =>
        _modals.Add(new ModalWindow(control, holdsClock, blocksPause));

    // Space toggles the clock at the player's request - ignored while a page nobody asked to see
    // stacked behind it, since a pause opened there would only surface once that page comes down.
    public void TogglePause(string bandName, string sinceArrival, string population)
    {
        if (BlocksPause)
        {
            return;
        }

        if (_pausePanel.Visible)
        {
            HidePause();
            return;
        }

        _pausePanel.Show(bandName, sinceArrival, population);
    }

    private void HidePause()
    {
        _pausePanel.Hide();
        ClockShouldResume?.Invoke();
    }

    // Nothing else answers to Escape, and a menu or a page that can only be dismissed by
    // clicking one particular thing is one the player fights. The naming question, the workbench,
    // the context menu, and the detail page each dismiss themselves through their own owner;
    // this is only the piece that is purely about controls this type owns.
    public void HandleEscape()
    {
        if (_helpPanel.Visible)
        {
            _helpPanel.Dismiss();
        }
    }

    // F11 moves between the window and a borderless fullscreen - the whole screen, taskbar
    // included, with no native Windows chrome (WindowMode.Fullscreen rather than the exclusive
    // video-mode switch, which is the less forgiving kind on Windows). F alone zooms the camera
    // (FreeCameraRig), so the key is F11; the controls page lists it under Windows.
    public static void ToggleFullscreen()
    {
        var mode = DisplayServer.WindowGetMode();
        var inFullscreen = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        DisplayServer.WindowSetMode(inFullscreen ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
    }
}
