using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Maps;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Views;
using ManyWinters.Godot.Fog;
using ManyWinters.Godot.Terrain;
using ManyWinters.Godot.Interaction;
using ManyWinters.Godot.Ui;

namespace ManyWinters.Godot;

public partial class Main : Node3D
{
    // Every tunable number this scene runs on lives in these two, not in constants here - what
    // is a rule of the world itself belongs in SimulationRules (via WorldConfiguration) instead.
    private readonly SimulationPacing _pacing = SimulationPacing.Default;
    private readonly PresentationSettings _presentation = PresentationSettings.Default;

    private WorldState _world = null!;
    private RevealableExploration _exploration = null!;
    private WorldPresenter _presenter = null!;
    private FogOfWarRenderer _fogOfWar = null!;
    private GroundClouds _groundClouds = null!;
    private CloudFogMask _cloudFogMask = null!;
    private TerrainRenderer _terrain = null!;
    private FreeCameraRig _cameraRig = null!;
    private Position _campCenter;

    private FloatingPanel _inspector = null!;
    private Label _infoLabel = null!;
    private Label _buildingsLabel = null!;
    private Label _gravesLabel = null!;
    private SelectionController _selection = null!;
    private StatusBar _statusBar = null!;
    private InscriptionOverlay _inscriptionOverlay = null!;
    private PausePanel _pausePanel = null!;
    private HelpPanel _helpPanel = null!;
    private ChroniclePanel _chronicle = null!;
    private WorkshopController _workshopController = null!;

    // Every full-screen page or window that asks for the player's whole attention, tagged with
    // what that means for it, in one place - so a page added here only has to be added here, and
    // not hunted down separately everywhere something else already checks the others (which is
    // how the pause panel twice ended up stackable behind one of these). `HoldsClock` says
    // whether it stops the world while it is up (see _Process); `BlocksPause` says whether its
    // being up should stop Space from opening a second window on top of it (see TogglePause). The
    // pause panel holds the clock but is not its own blocker - TogglePause decides what pressing
    // Space does to the one already up, not whether it is allowed to be up at all.
    private IEnumerable<ModalWindow> ModalWindows =>
    [
        new ModalWindow(_inscriptionOverlay, HoldsClock: true, BlocksPause: true),
        new ModalWindow(_pausePanel, HoldsClock: true, BlocksPause: false),
        new ModalWindow(_helpPanel, HoldsClock: true, BlocksPause: true),
        new ModalWindow(_workshopController.ModalControl, HoldsClock: true, BlocksPause: true),
        new ModalWindow(_selection.DetailModalControl, HoldsClock: true, BlocksPause: true),
        new ModalWindow(_chronicle, HoldsClock: false, BlocksPause: true),
    ];

    private bool AnyClockHoldingWindowVisible => ModalWindows.Any(window => window.HoldsClock && window.Control.Visible);

    private bool AnyPauseBlockingWindowVisible => ModalWindows.Any(window => window.BlocksPause && window.Control.Visible);

    private readonly record struct ModalWindow(Control Control, bool HoldsClock, bool BlocksPause);

    private WorldInputController _worldInput = null!;
    private EndingAnnouncements _endingAnnouncements = new();
    private double _tickAccumulator;
    private OcclusionFader _occlusionFader = null!;
    private OrderCoordinator _orderCoordinator = null!;

    // Captured in _Ready, the one moment BandArrival.Of really means "just arrived"; TogglePause
    // calls BandArrival.Of again later only for its live population counts.
    private long _bandArrivalTick;

    // Above the game's own UI canvas (SetUpUi), which is built three quarters of the way through
    // the load: both sit on the default layer otherwise, and the status bar - added to the tree
    // later - drew over the bottom of the title page for the rest of the load.
    private const int LoadingCanvasLayer = 100;

    // The title page, held up while the world is built. On its own CanvasLayer so it covers the
    // game's own UI layer, and freed rather than hidden once there is a world to look at.
    private CanvasLayer? _loadingCanvas;
    private LoadingScreen? _loadingScreen;

    // Nothing below is built yet: the camera, the presenter and the terrain are all still null,
    // and every per-frame and input path that touches them has to stand down until they are not.
    private bool _loading;

    // Async so the loading screen can be drawn between the steps: without yielding a frame, all
    // of this runs inside one frame and the player sees a frozen window and then a world.
    public override async void _Ready()
    {
        _loading = true;

        _loadingCanvas = new CanvasLayer { Layer = LoadingCanvasLayer };
        AddChild(_loadingCanvas);
        _loadingScreen = new LoadingScreen();
        _loadingCanvas.AddChild(_loadingScreen);

        await Building(0, "Reading the winter's rules");
        var configuration = WorldConfiguration.LoadFromJson(catalog => ContentFiles.ReadJsonTree($"res://Content/{catalog}"));

        await Building(10, "Raising the land");
        var map = MapLoader.LoadDefault(configuration);
        _world = map.World;

        // Everyone idles for up to IdleTask.MaxPauseTicks before their first wander leg, and with
        // the prologue holding the clock a band that then stood still read as stuck. Running those
        // ticks before the views exist keeps the same deterministic world, watched from a few
        // ticks in; it costs the band that much hunger before the player can act.
        await Building(35, "Waking the band");
        _world.Advance(IdleTask.MaxPauseTicks + 1);

        await Building(45, "Hanging the sky");
        _exploration = new RevealableExploration(_world.Exploration);
        _campCenter = map.CampCenter;
        GetViewport().PhysicsObjectPicking = true;
        SetUpLighting();
        SetUpSky();

        await Building(55, "Laying the ground");
        SetUpTerrain();

        await Building(70, "Placing the camera");
        SetUpCamera();

        await Building(75, "Drawing the pages");
        // Built ahead of the UI rather than with the rest of the band below: SetUpUi constructs
        // the workshop controller, which needs the order coordinator (and, through it, the
        // presenter) already in hand.
        _presenter = new WorldPresenter(this, _world, _exploration, _cameraRig.RigGlobalPosition, _cameraRig.ViewRadius, OnPersonClicked, OnResourceNodeClicked, OnBuildingClicked, OnGraveSelected, OnItemPileClicked, OnMissedClick, _terrain.SampleHeight);
        _occlusionFader = new OcclusionFader(_cameraRig, _presenter, _presentation);
        _orderCoordinator = new OrderCoordinator(_world, _presenter, _presentation);
        SetUpUi();
        _orderCoordinator.WorldChanged += OnOrderCoordinatorWorldChanged;
        _orderCoordinator.OrderFailed += _statusBar.Notify;

        await Building(85, "Gathering the clouds");
        CloudScatter.Scatter(this, _terrain.Half);
        _cloudFogMask = new CloudFogMask(this, _cameraRig.Camera);

        await Building(90, "Setting out the band");
        _fogOfWar = new FogOfWarRenderer(_exploration, _terrain.Half, _cameraRig.Camera, _cloudFogMask);
        _groundClouds = new GroundClouds(this, _fogOfWar, _terrain.Half, _terrain.SampleHeight);

        await Building(100, "The band arrives");
        var arrival = BandArrival.Of(_world);
        _bandArrivalTick = arrival.ArrivalTick;

        _loadingCanvas.QueueFree();
        _loadingCanvas = null;
        _loadingScreen = null;
        _loading = false;

        ShowInscription(Prologue.Write(arrival), offerAnotherBand: false);

        GD.Print($"Main ready. World has {_world.People.Count} people and {_world.Entities.Count(e => e.Category == EntityCategory.Growable)} resource nodes at tick {_world.Clock.CurrentTick}.");
        // Answers "am I running the build I think I am" (a stale process after hot-reload or a
        // forgotten relaunch) with one log line; derived from the assembly, not bumped by hand.
        GD.Print($"Build tag: {BuildTag.For(AssemblyBuildTimeUtc())}");
    }

    // Says what is about to be built, then lets the frame draw before building it - the other way
    // round and every line the player reads names the step that has just finished.
    private async Task Building(float progress, string step)
    {
        _loadingScreen!.Show(progress, step);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    // Last write time of the running assembly - a build stamp needing no build-time code
    // generation. Built from BaseDirectory because Assembly.Location is empty here: Godot loads
    // the assembly from a stream so the file can be overwritten while the editor holds it. Null
    // when there is nothing to stat; BuildTag renders that as "unknown" rather than a guess.
    private static DateTimeOffset? AssemblyBuildTimeUtc()
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Main).Assembly.GetName().Name}.dll");

        return File.Exists(assemblyPath)
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(assemblyPath), TimeSpan.Zero)
            : null;
    }

    public override void _Process(double delta)
    {
        // Half the world does not exist yet; every line below reaches into it.
        if (_loading)
        {
            return;
        }

        _cameraRig.HandleInput((float)delta);
        // Every frame, not per tick: the camera and the selected person's interpolated position
        // move continuously between ticks, so what stands in the way changes continuously too.
        _occlusionFader.Update(_selection.Person);
        // Main._Process still drives this directly - moving it under selection's own refresh
        // waits for Plan 8's frame-loop extraction.
        _selection.UpdateMarker();
        // Also every frame: hover is taken on mouse movement but can be lost without any - a
        // person can walk out from under a resting cursor (see HoverArbiter).
        _presenter.RevalidateHover();
        // Also every frame: the mask camera tracks the main camera's continuous movement.
        _cloudFogMask.Update();

        // Time stands still while any window that holds the clock is up (see ModalWindows) - an
        // inscription, a pause the player asked for, the controls page, the workbench, the detail
        // page. Each is read or worked on instead of played through, not while playing.
        if (AnyClockHoldingWindowVisible)
        {
            return;
        }

        _tickAccumulator += delta;
        if (_tickAccumulator < _pacing.TickIntervalSeconds)
        {
            return;
        }

        _tickAccumulator -= _pacing.TickIntervalSeconds;
        if (_selection.Person is { } selectedPerson)
        {
            _world.Execute(new GrantIdleGraceCommand(selectedPerson, _pacing.SelectedPersonIdleGraceTicks));
        }

        _world.Advance(1);
        _presenter.RefreshExploration(_cameraRig.RigGlobalPosition, _cameraRig.ViewRadius);
        _fogOfWar.Refresh();
        _groundClouds.Refresh();
        _orderCoordinator.ResolvePending();
        _statusBar.SetTick(_world.Clock.CurrentTick, _world.CurrentSeason);
        _selection.Refresh();
        RefreshBuildingsLabel();
        RefreshGravesLabel();
        AnnounceEndingIfAny();

        foreach (var person in _world.People)
        {
            _presenter.SetPersonAlive(person.Id, person.IsAlive);
            // A person who dies mid-stride still tweens to that tick's final position over the
            // next second - one last visible step. Snapping (overSeconds: 0) once dead pins the
            // corpse there with nothing left to glide.
            _presenter.SetPersonPosition(person.Id, person.Position, person.IsAlive ? (float)_pacing.TickIntervalSeconds : 0f);
        }

        foreach (var node in _world.Entities)
        {
            if (node.Growth is not { } growth)
            {
                continue;
            }

            if (!growth.IsAlive)
            {
                // Nodes that withered from climate stress (see WorldState.Advance); felling
                // removes its own view immediately.
                _presenter.RemoveResourceNodeView(node.Id);
                continue;
            }

            _presenter.SetResourceNodeHasFruit(node.Id, growth.RemainingAmount > 0);
        }

        GD.Print($"Tick {_world.Clock.CurrentTick}: {_world.People.Count(p => p.IsAlive)} of {_world.People.Count} people alive.");
    }

    public override void _Input(InputEvent @event)
    {
        // Nothing to toggle, pause or dismiss until _Ready has finished building it.
        if (_loading)
        {
            return;
        }

        // A name for a new thing is being typed (see TextEntry): every letter belongs to the
        // field, so the keys the game answers to on its own are left alone. Escape and F11 are
        // not letters and still work - one puts the workbench away, the other is the window's.
        var typing = TextEntry.HasTheKeyboard(GetViewport());

        if (!typing && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.T })
        {
            _cameraRig.ToggleProjection();
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            ToggleFullscreen();
        }

        if (!typing && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
        {
            // Space is Godot's default ui_accept: unless eaten here it also activates whichever
            // Control last took focus (Chronicle, the "?" button) on top of toggling the pause.
            TogglePause();
            GetViewport().SetInputAsHandled();
        }

        // Nothing else answers to Escape, and a menu or a page that can only be dismissed by
        // clicking one particular thing is one the player fights. Both, not one or the other:
        // the controls page swallows the clicks that would open a menu, so only one can be up.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            _worldInput.CloseContextMenu();
            _workshopController.HandleEscape();

            if (_helpPanel.Visible)
            {
                _helpPanel.Dismiss();
            }

            _selection.CloseDetail();
        }

        _worldInput.Handle(@event, GetViewport());
    }

    // F11 moves between the window and a borderless fullscreen - the whole screen, taskbar
    // included, with no native Windows chrome (WindowMode.Fullscreen rather than the exclusive
    // video-mode switch, which is the less forgiving kind on Windows). F alone zooms the camera
    // (FreeCameraRig), so the key is F11; the controls page lists it under Windows.
    private static void ToggleFullscreen()
    {
        var mode = DisplayServer.WindowGetMode();
        var inFullscreen = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        DisplayServer.WindowSetMode(inFullscreen ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
    }

    // _UnhandledInput, not _Input: _Input fires before the UI gets the event, so wheel/drag over
    // a Control would also zoom/rotate the camera underneath. That alone is not enough - a
    // ScrollContainer with nothing left to scroll lets the wheel fall through - so the camera also
    // ignores everything while the cursor is over any Control at all.
    public override void _UnhandledInput(InputEvent @event)
    {
        // The camera rig is built two thirds of the way through the load, and until then one
        // mouse movement over the title page was enough to throw here every frame.
        if (_loading)
        {
            return;
        }

        _worldInput.HandleUnhandled(@event, GetViewport());
    }

    private void SetUpLighting()
    {
        AddChild(new DirectionalLight3D
        {
            Rotation = new Vector3(Mathf.DegToRad(-45), Mathf.DegToRad(-45), 0),
        });
    }

    private void SetUpSky()
    {
        SkySetup.Create(this);
    }

    private void SetUpTerrain()
    {
        _terrain = TerrainSetup.Create(this, OnGroundInputEvent);
    }

    private void SetUpCamera()
    {
        var campX = (float)_campCenter.X;
        var campZ = (float)_campCenter.Y;
        var campPosition = new Vector3(campX, _terrain.SampleHeight(campX, campZ), campZ);
        _cameraRig = new FreeCameraRig(
            this,
            campPosition,
            _presentation.InitialZoomDistance,
            _presentation.MinZoom,
            _presentation.MaxZoom,
            _terrain.SampleHeight);
    }

    private void SetUpUi()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        // The status bar first: it carries the buttons the windows below hang their own toggles on.
        SetUpStatusBar(canvas);
        SetUpInspectorWindow(canvas);
        SetUpSelectionController(canvas);
        SetUpChronicle(canvas);
        SetUpWorkshopController(canvas);
        // Attached rather than built with the rest of selection above: the detail page has to
        // land after the workbench in the canvas so it draws on top of it.
        _selection.AttachDetailPanel(canvas);
        _selection.WorkshopRequested += _workshopController.Toggle;
        SetUpWorldInputController(canvas);
        SetUpInscriptionOverlay(canvas);
        SetUpPausePanel(canvas);
        SetUpHelpPanel(canvas);
    }

    private void SetUpSelectionController(CanvasLayer canvas)
    {
        _selection = new SelectionController(canvas, _world, _presenter, _cameraRig, _presentation);
        _selection.Refreshed += RefreshInfoLabel;
        // Letting the detail page go primes the tick accumulator, so the world starts again on
        // the next frame rather than a full interval later - as dismissing the controls page does.
        _selection.Closed += () => _tickAccumulator = _pacing.TickIntervalSeconds;
        _statusBar.BandRequested += _selection.ToggleBandPanel;
    }

    // The workbench, opened from the pack line on the selected person's card. Like the pause
    // page it holds the clock while it is up (see _Process): working a thing over is meant to be
    // unhurried.
    private void SetUpWorkshopController(CanvasLayer canvas)
    {
        _workshopController = new WorkshopController(canvas, _world, _orderCoordinator);
        // Letting the workbench go primes the tick accumulator, so the world starts again on the
        // next frame rather than a full interval later - as dismissing the controls page does.
        _workshopController.Closed += () => _tickAccumulator = _pacing.TickIntervalSeconds;
        // A word the band coined outlives whoever coined it, so it goes in the chronicle rather
        // than only into the panel that asked for it.
        _workshopController.InscriptionRecorded += RecordInscription;
    }

    // Opposite the inspector, so the two can be open at once without covering each other.
    private void SetUpChronicle(CanvasLayer canvas)
    {
        _chronicle = new ChroniclePanel
        {
            Position = new Vector2(GetViewport().GetVisibleRect().Size.X - 476f, 16f),
        };
        canvas.AddChild(_chronicle);
        _statusBar.ChronicleRequested += _chronicle.Toggle;
    }

    // After the windows, so a menu opened over one of them is on top of it; before the
    // inscription overlay and the pause panel, which are on top of everything.
    private void SetUpWorldInputController(CanvasLayer canvas)
    {
        _worldInput = new WorldInputController(canvas, _world, _cameraRig, _selection, _orderCoordinator, _statusBar, _presentation);
        // Presenter is already built (see _Ready) by the time this runs; the two-step handoff
        // exists for the construction cycle - the presenter is itself built with this
        // controller's click callbacks - not because the presenter is unready here.
        _worldInput.AttachPresenter(_presenter);
        _selection.ActionInvoked += _worldInput.PerformAction;
    }

    // Added after the contextual menu so it draws over everything else on the canvas, the
    // inspector included.
    private void SetUpInscriptionOverlay(CanvasLayer canvas)
    {
        _inscriptionOverlay = new InscriptionOverlay();
        // The clock stood still, so the next tick is due the moment the inscription comes down -
        // a full interval later read as the world taking a second to notice.
        _inscriptionOverlay.Dismissed += () => _tickAccumulator = _pacing.TickIntervalSeconds;
        _inscriptionOverlay.AnotherBandRequested += OnAnotherBandRequested;
        canvas.AddChild(_inscriptionOverlay);
    }

    // After the inscription overlay: the two never show at once today (ticking, and with it every
    // death, is on hold while either is up), but this is the one that should draw on top.
    private void SetUpPausePanel(CanvasLayer canvas)
    {
        _pausePanel = new PausePanel();
        // The cross on the page is the other half of Space: both let the world go again.
        _pausePanel.Resumed += TogglePause;
        canvas.AddChild(_pausePanel);
    }

    // Last of all, so the controls can be read over whatever else is up. Opened and closed by
    // the "?" on the status bar or by Escape; like an inscription being dismissed, letting it go
    // primes the tick accumulator so the world starts again on the next frame rather than a full
    // interval later.
    private void SetUpHelpPanel(CanvasLayer canvas)
    {
        _helpPanel = new HelpPanel();
        _helpPanel.Dismissed += () => _tickAccumulator = _pacing.TickIntervalSeconds;
        canvas.AddChild(_helpPanel);
        _statusBar.HelpRequested += _helpPanel.Toggle;
    }

    // Space toggles the clock at the player's request - ignored while an inscription holds it,
    // which is not the player's to override. Unpausing primes the tick accumulator like an
    // inscription dismissal does (SetUpInscriptionOverlay), so the world resumes next frame.
    private void TogglePause()
    {
        // None of these are the player's to override (see ModalWindows): a pause asked for behind
        // a page nobody can see would only surface once that page comes down, as an extra pause
        // panel nobody asked to see waiting behind it.
        if (AnyPauseBlockingWindowVisible)
        {
            return;
        }

        if (_pausePanel.Visible)
        {
            _pausePanel.Hide();
            _tickAccumulator = _pacing.TickIntervalSeconds;
            return;
        }

        var band = BandArrival.Of(_world);
        var sinceArrival = DurationText.For(_world.Clock.CurrentTick - _bandArrivalTick, _world.Configuration.Rules.TicksPerYear, _world.Configuration.Rules.TicksPerSeason);
        _pausePanel.Show(band.BandName, sinceArrival, PopulationSummary.Of(band.People, band.Men, band.Women, band.Children));
    }

    // The fate is read off the world every tick and shown the first tick it changes (see
    // EndingAnnouncements): once when the last man or woman dies, once more when the last
    // person does.
    private void AnnounceEndingIfAny()
    {
        if (!_endingAnnouncements.ShouldAnnounce(BandEnding.FateOf(_world.People)))
        {
            return;
        }

        if (BandEnding.Of(_world) is { } ending)
        {
            var nobodyIsLeft = ending.Fate == BandFate.Ended;

            // The roster and the selection are about the dead, and the world under this epitaph
            // waits for a successor band: put the old band's windows away now, the way that
            // band's arrival would have (OnAnotherBandRequested), not only then.
            if (nobodyIsLeft)
            {
                CloseBandWindows();
            }

            ShowInscription(Epitaph.Write(ending), offerAnotherBand: nobodyIsLeft);
        }
    }

    // Every window that shows something about whoever is selected or was, closed together so a
    // future one is not the one somebody forgets to add here - which is exactly how the detail
    // page got left open through an ending it was never told about.
    private void CloseBandWindows()
    {
        _selection.CloseForBandEnd();
        _workshopController.Close();
    }

    // A successor band arrives into this same world: a fresh crowd is spawned and the prologue
    // takes their place on screen. The old band's dead and graves stay where they are.
    private void OnAnotherBandRequested()
    {
        // Put away the old band's windows - the roster and selection are about dead people.
        CloseBandWindows();

        // The new band has not walked this land yet - fog clears around their new camp.
        _world.Exploration.Reset();

        var idRng = new Random(_world.Clock.CurrentTick.GetHashCode());
        var newCamp = MapLoader.SpawnNewBand(_world, idRng, _campCenter);
        _campCenter = newCamp;

        // So the new band's fate changes are announced independently of the old band's.
        _endingAnnouncements = new EndingAnnouncements();

        // Brief pre-roll so the new band is not standing still behind the prologue.
        _world.Advance(IdleTask.MaxPauseTicks + 1);

        // _Process is blocked while the inscription is up, so refresh the fog here rather than
        // waiting for it.
        _presenter.RefreshExploration(_cameraRig.RigGlobalPosition, _cameraRig.ViewRadius);
        _fogOfWar.Refresh();
        _groundClouds.Refresh();

        var arrival = BandArrival.Of(_world);
        _bandArrivalTick = arrival.ArrivalTick;
        ShowInscription(Prologue.Write(arrival), offerAnotherBand: false);

        var campX = (float)newCamp.X;
        var campZ = (float)newCamp.Y;
        var campHeight = _terrain.SampleHeight(campX, campZ);
        _cameraRig.FocusOn(new Vector3(campX, campHeight, campZ));
    }

    // Every inscription stops the clock until dismissed (see _Process); its title goes up on
    // the overlay and the whole of it into the chronicle, where it stays for the session.
    private void ShowInscription(Inscription inscription, bool offerAnotherBand)
    {
        RecordInscription(inscription);
        _inscriptionOverlay.Show(inscription, offerAnotherBand);
    }

    // Written down without stopping anything. For a moment the player is already living
    // through - they have just typed the name themselves - taking the whole screen to tell them
    // what they did would be ceremony in the way of play. The chronicle keeps it either way,
    // and that is what outlives the band.
    private void RecordInscription(Inscription inscription)
    {
        _chronicle.Add(inscription);
        _statusBar.ShowChronicleButton();
        GD.Print($"Inscription: {inscription.Title}");
    }

    // Debug only: the world's raw numbers and the levers that move them. What the player is meant
    // to read and press lives in SelectionPanel; this window keeps the dump, the spawner, the
    // extinguisher and the map reveal, none of which belong in the game proper (docs/todo/todo.md).
    //
    // Shut until the status bar's Inspector button is pressed. It used to open with the game and
    // sit over the corner the band's roster now claims, which put a debug tool in front of the
    // player before they had asked for one.
    private void SetUpInspectorWindow(CanvasLayer canvas)
    {
        const float width = 340f;

        var panel = new FloatingPanel("Inspector (debug)")
        {
            Position = new Vector2(16, 16),
            Visible = false,
            CustomMinimumSize = new Vector2(width, 0),
            // A Theme resource cascades its DefaultFontSize down to every descendant Control that
            // doesn't set its own override - unlike AddThemeFontSizeOverride, which only affects
            // the single Control it's called on.
            Theme = new Theme { DefaultFontSize = _presentation.InspectorFontSize },
        };
        canvas.AddChild(panel);
        _inspector = panel;
        _statusBar.InspectorRequested += () => _inspector.Visible = !_inspector.Visible;

        _infoLabel = new Label
        {
            Text = "No selection.",
            CustomMinimumSize = new Vector2(width, 0),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        panel.Body.AddChild(_infoLabel);

        var spawnButton = new Button { Text = "Spawn Person" };
        spawnButton.Pressed += OnSpawnButtonPressed;
        panel.Body.AddChild(spawnButton);

        // The quick way to the epitaph and its "Another band comes" offer (docs/todo/todo.md):
        // the epitaph of a band nobody is left in carries no closing words, so without this the
        // only way to that screen is playing the band out by hand.
        var extinguishButton = new Button { Text = "Extinguish Band" };
        extinguishButton.Pressed += OnExtinguishButtonPressed;
        panel.Body.AddChild(extinguishButton);

        // A development view, not a gameplay one (see RevealableExploration): the whole map as if
        // fog of war did not exist.
        var revealMapToggle = new CheckButton { Text = "Reveal Map" };
        revealMapToggle.Toggled += OnRevealMapToggled;
        panel.Body.AddChild(revealMapToggle);

        _buildingsLabel = new Label { Text = "Buildings: none" };
        panel.Body.AddChild(_buildingsLabel);

        _gravesLabel = new Label { Text = "Graves: none" };
        panel.Body.AddChild(_gravesLabel);
    }

    private void SetUpStatusBar(CanvasLayer canvas)
    {
        _statusBar = new StatusBar();
        _statusBar.AddThemeStyleboxOverride("panel", PanelChrome.Background());
        canvas.AddChild(_statusBar);
        _statusBar.SetTick(_world.Clock.CurrentTick, _world.CurrentSeason);
    }

    // Refreshes right away rather than waiting for the next tick: the tick interval is long
    // enough that a toggle which only took effect a moment later would read as broken.
    private void OnRevealMapToggled(bool toggledOn)
    {
        _exploration.RevealAll = toggledOn;
        _presenter.RefreshExploration(_cameraRig.RigGlobalPosition, _cameraRig.ViewRadius);
        _fogOfWar.Refresh();
        _groundClouds.Refresh();
    }

    private void OnSpawnButtonPressed()
    {
        var name = _world.GenerateUnrelatedName(Random.Shared);
        _world.Execute(new SpawnPersonCommand(name, FindFreeSpawnPosition(), Person.Unknown, Person.Unknown));
    }

    private void OnExtinguishButtonPressed()
    {
        _world.Execute(new ExtinguishBandCommand());
    }

    // What every caller of OrderCoordinator.Perform used to refresh by hand once it had executed
    // or queued the offer.
    private void OnOrderCoordinatorWorldChanged()
    {
        _selection.Refresh();
        RefreshBuildingsLabel();
        RefreshGravesLabel();
    }

    private Position FindFreeSpawnPosition()
    {
        const float minDistance = 1.2f;
        const float spread = 16f;

        return FreePositionSearch.Find(
            () => new Position(
                _campCenter.X + ((GD.Randf() - 0.5f) * spread),
                _campCenter.Y + ((GD.Randf() - 0.5f) * spread)),
            candidate => !_world.People.Any(p => WorldState.Distance(p.Position, candidate) < minDistance),
            maxAttempts: 20);
    }

    // The few people this one is closest to. Bonds never formed are absent (see Affections.For),
    // so a loner reads "none" rather than a column of zeroes.
    private string BondsText(Person person)
    {
        var namesById = _world.People.ToDictionary(p => p.Id, p => p.Name);
        var bonds = _world.Affections.For(person.Id)
            .Where(bond => namesById.ContainsKey(bond.Other))
            .Take(3)
            .Select(bond => $"{namesById[bond.Other]} {bond.Value:0}")
            .ToList();

        return bonds.Count > 0 ? string.Join(", ", bonds) : "none";
    }

    // Passed to WorldPresenter/TerrainSetup as click callbacks before WorldInputController can
    // exist (it is built inside SetUpUi, from SelectionController and OrderCoordinator - see
    // _Ready), so these forward instead of being the controller's own methods directly. Each
    // reads _worldInput lazily at call time, long after _Ready has finished building it.
    private void OnPersonClicked(Person person, MouseButton button) => _worldInput.OnPersonClicked(person, button);

    private void OnGraveSelected(Grave grave) => _worldInput.OnGraveSelected(grave);

    private void OnResourceNodeClicked(Entity node, MouseButton button) => _worldInput.OnResourceNodeClicked(node, button);

    private void OnItemPileClicked(Entity pile, MouseButton button) => _worldInput.OnItemPileClicked(pile, button);

    private void OnBuildingClicked(Entity building, MouseButton button) => _worldInput.OnBuildingClicked(building, button);

    private void OnMissedClick(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx) =>
        _worldInput.OnMissedClick(camera, @event, position, normal, shapeIdx);

    private void OnGroundInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx) =>
        _worldInput.OnGroundInputEvent(camera, @event, position, normal, shapeIdx);

    // The debug inspector's raw dump of whoever is selected - kept apart from the player-facing
    // panels, which are SelectionController's own to refresh.
    private void RefreshInfoLabel()
    {
        if (_selection.Grave is { } grave)
        {
            _infoLabel.Text = InspectorText.ForGrave(grave);
            return;
        }

        if (_selection.Person is not { } person)
        {
            _infoLabel.Text = "No selection.";
            return;
        }

        var status = person.IsAlive ? string.Empty : " [dead]";
        var skills = person.Skills.Levels.Count > 0
            ? string.Join(", ", person.Skills.Levels.Select(kv => $"{kv.Key}: {kv.Value}"))
            : "none";
        var techniques = person.KnownTechniques.Count > 0
            ? string.Join(", ", person.KnownTechniques)
            : "none";
        var inventory = person.Inventory.Counts.Count > 0
            ? string.Join(", ", person.Inventory.Counts.Select(kv => $"{kv.Key} x{kv.Value}"))
            : "empty";
        var carriedWeight = person.Inventory.TotalWeight(_world.Configuration.ItemCatalog);
        var maxCarryWeight = _world.MaxCarryWeightFor(person);
        _infoLabel.Text =
            $"{person.Id}  {person.Name}{status}\n" +
            $"Position: {person.Position}\n" +
            $"Age: {AgeText(person)} ({_world.LifeStageOf(person)}, {person.Sex})\n" +
            $"Task: {InspectorText.ForTask(person)}\n" +
            $"Hunger: {person.Needs.Hunger}  Fatigue: {person.Needs.Fatigue}\n" +
            $"Skills: {skills}\n" +
            $"Known techniques: {techniques}\n" +
            $"Closest to: {BondsText(person)}\n" +
            $"Carrying: {carriedWeight}/{maxCarryWeight}\n" +
            $"Inventory: {inventory}";
    }

    private void RefreshBuildingsLabel()
    {
        var buildings = _world.Entities.Where(e => e.Category == EntityCategory.Building).ToList();
        _buildingsLabel.Text = "Buildings: " + (buildings.Count > 0
            ? string.Join(", ", buildings.Select(BuildingSummary))
            : "none");
    }

    private string AgeText(Person person) =>
        DurationText.For(_world.Clock.CurrentTick - person.BirthTick, _world.Configuration.Rules.TicksPerYear, _world.Configuration.Rules.TicksPerSeason);

    private void RefreshGravesLabel()
    {
        _gravesLabel.Text = $"Graves: {_world.Graves.Count}";
    }

    private static string BuildingSummary(Entity building)
    {
        var inventory = building.Storage!.Counts.Count > 0
            ? string.Join(", ", building.Storage.Counts.Select(kv => $"{kv.Key} x{kv.Value}"))
            : "empty";
        return $"{building.Kind} #{building.Id} ({building.Condition!.Value:0}%) [{inventory}]";
    }
}
