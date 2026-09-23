using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Ui;

// Everything SelectionController shows about who is picked: the marker over their head, the
// card, the roster, and the full page behind the card. Built here, in the one order the screen
// draws them, so SelectionController only ever wires behaviour onto controls it did not itself
// create or attach.
internal sealed record SelectionUi(TextureRect Marker, SelectionPanel Panel, BandPanel BandPanel, PersonDetailPanel DetailPanel);

// Everything WorkshopController shows: the workbench itself and the naming question laid over
// it. The shield that blocks the world while it is open is MainUi's alone to show and hide (see
// its constructor), so it is not handed down here.
internal sealed record WorkshopUi(WorkshopPanel Panel, NamingPanel NamingPanel);

// The screen's own furniture: the status bar, the debug inspector, the chronicle, the
// inscription overlay, and the pause and help pages - plus every control SelectionController,
// WorkshopController, and WorldInputController operate. This is the one place that builds and
// attaches every one of them, in the order the screen draws them; the controllers above only
// wire behaviour onto what they are handed (see SelectionUi, WorkshopUi).
internal sealed partial class MainUi : CanvasLayer
{
    private const string SelectionMarkerTexturePath = "res://Content/people/selection_marker.png";

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

    public StatusBar StatusBar { get; }

    public ChroniclePanel Chronicle { get; }

    public InscriptionOverlay InscriptionOverlay { get; }

    public InspectorPanel Inspector { get; }

    public SelectionUi Selection { get; }

    public WorkshopUi Workshop { get; }

    public ContextMenu ContextMenu { get; }

    // True while any registered control that holds the clock is visible.
    public bool HoldsClock => _modals.Any(modal => modal.HoldsClock && modal.Control.Visible);

    // True while any registered control that should keep the pause page from opening on top of
    // it is visible.
    public bool BlocksPause => _modals.Any(modal => modal.BlocksPause && modal.Control.Visible);

    private readonly WorldState _world;

    public MainUi(WorldState world, PresentationSettings presentation)
    {
        _world = world;

        // First: it carries the buttons the windows below hang their own toggles on.
        StatusBar = new StatusBar();
        StatusBar.AddThemeStyleboxOverride("panel", PanelChrome.Background());
        AddChild(StatusBar);
        // Not ticked here: StatusBar builds _tickLabel in its own _Ready, which needs it inside
        // the tree - not yet true during this constructor (see this type's own _Ready).

        Inspector = new InspectorPanel(presentation);
        AddChild(Inspector);
        StatusBar.InspectorRequested += () => Inspector.Visible = !Inspector.Visible;

        // The player's own read of who is selected: the marker over their head, the card, and
        // the band's roster - built next so they sit above the status bar and under everything
        // that follows.
        var marker = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(SelectionMarkerTexturePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(presentation.SelectionMarkerScreenSize, presentation.SelectionMarkerScreenSize),
            Visible = false,
        };
        AddChild(marker);

        var selectionPanel = new SelectionPanel();
        AddChild(selectionPanel);

        var bandPanel = new BandPanel();
        AddChild(bandPanel);

        // Opposite the inspector, so the two can be open at once without covering each other.
        // Positioned in _Ready, not here: GetViewport() needs this CanvasLayer inside the tree,
        // which it is not yet - composition code attaches it only once this constructor returns.
        Chronicle = new ChroniclePanel();
        AddChild(Chronicle);
        StatusBar.ChronicleRequested += Chronicle.Toggle;
        RegisterModal(Chronicle, holdsClock: false, blocksPause: true);

        // The workbench. Laid in before the panel itself, so the shield sits under it and over
        // everything added earlier - the roster, the selected person's card, the status bar, the
        // world itself. The clock is stopped while the bench is out; it draws nothing, since the
        // world is what the player is working in the middle of and the camera keeps turning
        // over it.
        var workshopShield = new Control { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        workshopShield.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(workshopShield);

        var workshopPanel = new WorkshopPanel();
        workshopPanel.VisibilityChanged += () => workshopShield.Visible = workshopPanel.Visible;
        AddChild(workshopPanel);

        // Added after the workshop, so it lands on top of it rather than beside it - both are
        // centred on the same spot (PanelPlacement.Centred), which is what makes the one read as
        // a page laid over the other.
        var namingPanel = new NamingPanel();
        AddChild(namingPanel);

        Workshop = new WorkshopUi(workshopPanel, namingPanel);
        RegisterModal(workshopPanel, holdsClock: true, blocksPause: true);

        // The full page behind the selected person's card. Laid in after the workbench, so it
        // sits under it and over everything added earlier, and drawn to land later than the
        // workbench so it covers it - reading or acting on somebody here is meant to have the
        // player's whole attention, the same as working something over is.
        var detailShield = new Control { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        detailShield.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(detailShield);

        var detailPanel = new PersonDetailPanel();
        detailPanel.VisibilityChanged += () => detailShield.Visible = detailPanel.Visible;
        AddChild(detailPanel);

        Selection = new SelectionUi(marker, selectionPanel, bandPanel, detailPanel);
        RegisterModal(detailPanel, holdsClock: true, blocksPause: true);

        // After the windows, so a menu opened over one of them is on top of it; before the
        // inscription overlay and the pause panel, which are on top of everything.
        ContextMenu = new ContextMenu();
        AddChild(ContextMenu);

        // Built here so callers that only need Show/Record never construct a PausePanel or
        // HelpPanel of their own; the fields stay private since nothing outside this type reads
        // their visibility directly (see HoldsClock/BlocksPause).
        _pausePanel = new PausePanel();
        _helpPanel = new HelpPanel();

        // Last of all: inscriptions and the pause/help pages draw over everything else built
        // above.
        InscriptionOverlay = new InscriptionOverlay();
        // The clock stood still, so the next tick is due the moment the inscription comes down -
        // a full interval later read as the world taking a second to notice.
        InscriptionOverlay.Dismissed += () => ClockShouldResume?.Invoke();
        AddChild(InscriptionOverlay);
        RegisterModal(InscriptionOverlay, holdsClock: true, blocksPause: true);

        // The cross on the page is the other half of Space: both let the world go again.
        _pausePanel.Resumed += HidePause;
        AddChild(_pausePanel);
        RegisterModal(_pausePanel, holdsClock: true, blocksPause: false);

        // Last of all, so the controls can be read over whatever else is up. Opened and closed
        // by the "?" on the status bar or by Escape; like an inscription being dismissed,
        // letting it go primes the tick accumulator so the world starts again on the next frame
        // rather than a full interval later.
        // The shield under it swallows clicks anywhere on screen, not just over the page, the
        // same as the workbench's does.
        var helpShield = new Control { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        helpShield.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(helpShield);

        _helpPanel.VisibilityChanged += () => helpShield.Visible = _helpPanel.Visible;
        _helpPanel.Dismissed += () => ClockShouldResume?.Invoke();
        AddChild(_helpPanel);
        StatusBar.HelpRequested += _helpPanel.Toggle;
        RegisterModal(_helpPanel, holdsClock: true, blocksPause: true);
    }

    // Two things the constructor above cannot finish: Chronicle's position needs GetViewport(),
    // and StatusBar's tick label is built in StatusBar's own _Ready - both need this CanvasLayer
    // inside the tree, which is not yet true while this constructor runs.
    public override void _Ready()
    {
        Chronicle.Position = new Vector2(GetViewport().GetVisibleRect().Size.X - 476f, 16f);
        StatusBar.SetTick(_world.Clock.CurrentTick, _world.CurrentSeason);
    }

    // Every registration happens inside this constructor now that MainUi builds and attaches
    // every control itself; nothing outside this type calls it any more.
    private void RegisterModal(Control control, bool holdsClock, bool blocksPause) =>
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
