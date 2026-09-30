using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Maps;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Presentation;
using ManyWinters.Presentation.Logic;
using ManyWinters.Presentation.Views;
using ManyWinters.Presentation.Fog;
using ManyWinters.Presentation.Terrain;
using ManyWinters.Presentation.Interaction;
using ManyWinters.Presentation.Ui;

namespace ManyWinters.Godot;

public partial class Main : Node3D
{
    // Above the game's own UI canvas, which is built three quarters of the way through the load:
    // both sit on the default layer otherwise, and the status bar - added to the tree later -
    // drew over the bottom of the title page for the rest of the load.
    private const int LoadingCanvasLayer = 100;

    // Every tunable number this scene runs on lives in these two, not in constants here - what
    // is a rule of the world itself belongs to the simulation's own configuration instead.
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

    private SelectionController _selection = null!;
    private WorkshopController _workshopController = null!;
    private MainUi _mainUi = null!;
    private WorldInputController _worldInput = null!;
    private BandContinuityController _continuity = null!;
    private WorldFrameUpdater _worldFrameUpdater = null!;
    private SimulationLoop _simulationLoop = null!;
    private OcclusionFader _occlusionFader = null!;
    private OrderCoordinator _orderCoordinator = null!;

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

        // Everyone idles for up to the idle task's max pause before their first wander leg, and with
        // the prologue holding the clock a band that then stood still read as stuck. Running those
        // ticks before the views exist keeps the same deterministic world, watched from a few
        // ticks in; it costs the band that much hunger before the player can act.
        await Building(35, "Waking the band");
        _world.Advance(_world.Configuration.Rules.MaxPauseTicks + 1);

        await Building(45, "Hanging the sky");
        _exploration = new RevealableExploration(_world.Exploration);
        var campCenter = map.CampCenter;
        GetViewport().PhysicsObjectPicking = true;
        SetUpLighting();
        AddChild(SkySetup.Create());

        await Building(55, "Laying the ground");
        _terrain = TerrainRenderer.CreateDefault();
        AddChild(_terrain);

        await Building(70, "Placing the camera");
        var campX = (float)campCenter.X;
        var campZ = (float)campCenter.Y;
        var campPosition = new Vector3(campX, _terrain.SampleHeight(campX, campZ), campZ);
        _cameraRig = new FreeCameraRig(campPosition, _presentation.InitialZoomDistance, _presentation.MinZoom, _presentation.MaxZoom, _terrain.SampleHeight);
        AddChild(_cameraRig);

        await Building(75, "Drawing the pages");
        _presenter = new WorldPresenter(_world, _exploration, _cameraRig.RigGlobalPosition, _cameraRig.ViewRadius, _terrain.SampleHeight);
        AddChild(_presenter);
        _occlusionFader = new OcclusionFader(_cameraRig, _presenter, _presentation);
        _orderCoordinator = new OrderCoordinator(_world, _presenter, _presentation);
        SetUpUi();
        _orderCoordinator.WorldChanged += OnOrderCoordinatorWorldChanged;
        _orderCoordinator.OrderFailed += _mainUi.StatusBar.Notify;

        await Building(85, "Gathering the clouds");
        AddChild(CloudScatter.Scatter(_terrain.Half));
        _cloudFogMask = new CloudFogMask(_cameraRig.Camera);
        AddChild(_cloudFogMask);
        _worldFrameUpdater = new WorldFrameUpdater(_cameraRig, _occlusionFader, _selection, _presenter, _cloudFogMask);

        await Building(90, "Setting out the band");
        _fogOfWar = new FogOfWarRenderer(
            _exploration,
            _terrain.Half,
            _cameraRig.Camera,
            _cloudFogMask,
            _world.Configuration.Rules.CellSizeMeters
            );
        _groundClouds = new GroundClouds(
            _fogOfWar,
            _terrain.Half,
            _terrain.SampleHeight,
            _world.Configuration.Rules.MinWorldSize,
            _world.Configuration.Rules.MaxWorldSize,
            _world.Configuration.Rules.MinCenterAboveGroundFraction,
            _world.Configuration.Rules.MaxCenterAboveGroundFraction,
            _world.Configuration.Rules.MeanSpacingMeters,
            _world.Configuration.Rules.GroundCloudSeed,
            _world.Configuration.Rules.MinGapFactor,
            _world.Configuration.Rules.AttemptsPerTargetSpot,
            _world.Configuration.Rules.ClumpScaleMeters,
            _world.Configuration.Rules.ClumpWeight
            );
        AddChild(_groundClouds.Root);
        _continuity = new BandContinuityController(_world, campCenter, _presenter, _fogOfWar, _groundClouds, _cameraRig, _terrain, _mainUi, _selection, _workshopController);
        _simulationLoop = new SimulationLoop(_world, _pacing, _presenter, _cameraRig, _fogOfWar, _groundClouds, _orderCoordinator, _mainUi, _selection, _continuity);

        // Letting a clock-holding page go primes the tick accumulator, so the world starts again
        // on the next frame rather than a full interval later. Input is disabled until _loading
        // clears below, so nothing can dismiss one of these pages before the loop exists to wire
        // straight to.
        _selection.Closed += _simulationLoop.TickAsSoonAsPossible;
        _workshopController.Closed += _simulationLoop.TickAsSoonAsPossible;
        _mainUi.ClockShouldResume += _simulationLoop.TickAsSoonAsPossible;

        // The E2E suite's calibration: printed the moment the prologue (or any later inscription)
        // goes down, which is always after the test harness has set its own log-reading offset -
        // the suite never has to guess a click target off a recorded frame again.
        _mainUi.InscriptionOverlay.Dismissed += PrintE2EAnchors;

        await Building(100, "The band arrives");

        _loadingCanvas.QueueFree();
        _loadingCanvas = null;
        _loadingScreen = null;
        _loading = false;

        _continuity.ShowInitialArrival();

        GD.Print($"Main ready. World has {_world.People.Count} people and {_world.Entities.Count(e => e.Category == EntityCategory.Growable)} resource nodes at tick {_world.Clock.CurrentTick}.");
        // Answers "am I running the build I think I am" (a stale process after hot-reload or a
        // forgotten relaunch) with one log line; derived from the assembly, not bumped by hand.
        GD.Print($"Build tag: {BuildTag.For(AssemblyBuildTimeUtc())}");
    }

    public override void _Process(double delta)
    {
        // Half the world does not exist yet; every line below reaches into it.
        if (_loading)
        {
            return;
        }

        _worldFrameUpdater.Update((float)delta);
        _simulationLoop.Update(delta);
    }

    public override void _Input(InputEvent @event)
    {
        // Nothing to toggle, pause or dismiss until _Ready has finished building it.
        if (_loading)
        {
            return;
        }

        // A name for a new thing is being typed: every letter belongs to the field, so the keys
        // the game answers to on its own are left alone. Escape and F11 are not letters and still
        // work - one puts the workbench away, the other is the window's.
        var typing = TextEntry.HasTheKeyboard(GetViewport());

        if (!typing && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.T })
        {
            _cameraRig.ToggleProjection();
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            MainUi.ToggleFullscreen();
        }

        if (!typing && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
        {
            // Space is Godot's default ui_accept: unless eaten here it also activates whichever
            // Control last took focus (Chronicle, the "?" button) on top of toggling the pause.
            TogglePause();
            GetViewport().SetInputAsHandled();
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F12 })
        {
            // The "advance one tick" key for a session launched with the clock held: the world
            // stands still and this steps it exactly one tick - the same single step the
            // accumulator owes after a held clock is let go - so whatever was ordered while the
            // clock stood is resolved once and the frame settles at the next fixed tick. Normal
            // play (the clock running) ignores it.
            if (LaunchOptions.ClockHeld)
            {
                _simulationLoop.TickOnce();
            }
        }

        // Nothing else answers to Escape, and a menu or a page that can only be dismissed by
        // clicking one particular thing is one the player fights. Both, not one or the other:
        // the controls page swallows the clicks that would open a menu, so only one can be up.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            _worldInput.CloseContextMenu();
            _workshopController.HandleEscape();
            _mainUi.HandleEscape();
            _selection.CloseDetail();
        }

        _worldInput.Handle(@event, GetViewport());
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

    // The sprite's body centre, not its feet: WorldPresenter seats every creature/node view's
    // origin at groundHeight + nominalHeight/2, which already is the vertical middle of the drawn
    // silhouette for an ordinarily-centred sprite, so the unprojected origin itself is a click
    // that lands on opaque pixels rather than off the top or bottom of one. None when there is no
    // such thing, it fell out of camera view (a pending resource node), or it projects behind the
    // camera or off the edge of the viewport - a test reading "none" fails with a clear message
    // instead of clicking a stale or wrong pixel.
    private static void PrintE2EAnchor(string kind, Vector3? worldPosition, Camera3D camera)
    {
        if (worldPosition is not { } position || camera.IsPositionBehind(position))
        {
            GD.Print($"E2E anchor {kind} none");
            return;
        }

        var screen = camera.UnprojectPosition(position);
        var viewportSize = camera.GetViewport().GetVisibleRect().Size;
        if (screen.X < 0 || screen.Y < 0 || screen.X >= viewportSize.X || screen.Y >= viewportSize.Y)
        {
            GD.Print($"E2E anchor {kind} none");
            return;
        }

        GD.Print($"E2E anchor {kind} {(int)screen.X} {(int)screen.Y}");
    }

    // Says what is about to be built, then lets the frame draw before building it - the other way
    // round and every line the player reads names the step that has just finished.
    private async Task Building(float progress, string step)
    {
        _loadingScreen!.Show(progress, step);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void SetUpLighting()
    {
        AddChild(new DirectionalLight3D
        {
            Rotation = new Vector3(Mathf.DegToRad(-45), Mathf.DegToRad(-45), 0),
        });
    }

    // Every control SelectionController, WorkshopController and WorldInputController operate is
    // already built and attached by the UI's own constructor; here they are only handed the
    // bundle they need and wired to each other and to Main.
    private void SetUpUi()
    {
        _mainUi = new MainUi(_world, _presentation);
        AddChild(_mainUi);

        _selection = new SelectionController(_mainUi.Selection, _world, _presenter, _cameraRig, _presentation);
        _selection.Refreshed += RefreshInfoLabel;
        _mainUi.StatusBar.BandRequested += _selection.ToggleBandPanel;

        // The workbench, opened from the pack line on the selected person's card. Like the pause
        // page it holds the clock while it is up: working a thing over is meant to be unhurried.
        _workshopController = new WorkshopController(_mainUi.Workshop, _world, _orderCoordinator);
        _selection.WorkshopRequested += _workshopController.Toggle;

        _worldInput = new WorldInputController(_mainUi.ContextMenu, _world, _cameraRig, _presenter, _terrain, _selection, _orderCoordinator, _mainUi.StatusBar, _presentation);
        _selection.ActionInvoked += _worldInput.PerformAction;

        _mainUi.Inspector.SpawnRequested += OnSpawnButtonPressed;
        _mainUi.Inspector.ExtinguishRequested += OnExtinguishButtonPressed;
        _mainUi.Inspector.RevealMapToggled += OnRevealMapToggled;
    }

    // Space toggles the clock at the player's request - ignored while an inscription holds it,
    // which is not the player's to override.
    private void TogglePause()
    {
        var band = BandArrival.Of(_world);
        var sinceArrival = DurationText.For(_world.Clock.CurrentTick - _continuity.ArrivalTick, _world.Configuration.Rules.TicksPerYear, _world.Configuration.Rules.TicksPerSeason);
        _mainUi.TogglePause(band.BandName, sinceArrival, PopulationSummary.Of(band.People, band.Men, band.Women, band.Children));
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
        var name = _world.Naming.GenerateUnrelatedName(Random.Shared);
        var position = FindFreeSpawnPosition();

        // Joins whichever band is closest; with nobody alive, founds a camp of their own where
        // they stand.
        var nearest = _world.People.Where(person => person.IsAlive).MinBy(person => WorldState.Distance(position, person.Position));
        var home = nearest?.Home ?? FoundCamp(position);
        _world.Execute(new SpawnPersonCommand(name, position, Person.Unknown, Person.Unknown, home));
    }

    private HomeRange FoundCamp(Position anchor)
    {
        var camp = new HomeRange(anchor)
        {
            Radius = _world.Configuration.Rules.CampHomeRadius,
            DriftMetresPerSeason = 0f,
        };
        _world.AddHomeRange(camp);
        return camp;
    }

    private void OnExtinguishButtonPressed()
    {
        _world.Execute(new ExtinguishBandCommand());
    }

    // The E2E suite's own calibration: one parsable line per anchor, in the viewport pixel space
    // the test harness already posts clicks to - it sends WM_LBUTTONDOWN/UP straight to the
    // window's client area, and the game is launched at exactly that client size, so a viewport
    // pixel from Camera3D.UnprojectPosition needs no further conversion on either side. Which
    // person/node/animal is picked is unit-testable on its own; only turning that pick into a
    // screen point needs the engine. Gated like every other input-echoing log line: the harness
    // always launches this way, so the suite always sees these, and an ordinary session never
    // does.
    private void PrintE2EAnchors()
    {
        if (!LaunchOptions.Verbose)
        {
            return;
        }

        var camera = _cameraRig.Camera;
        var campCenter = _continuity.CampCenter;

        PrintE2EAnchor(
            "person",
            E2EAnchors.FirstLivingPerson(_world.People) is { } person ? _presenter.GetCreatureGlobalPosition(person.Id) : null,
            camera);

        PrintE2EAnchor(
            "wood",
            E2EAnchors.NearestWoodResourceNode(_world.Entities, _world.Configuration.ResourceCatalog, campCenter) is { } wood
                ? _presenter.GetResourceNodeGlobalPosition(wood.Id)
                : null,
            camera);

        PrintE2EAnchor(
            "deer",
            E2EAnchors.NearestLivingAnimal(_world.Animals, campCenter) is { } animal ? _presenter.GetCreatureGlobalPosition(animal.Id) : null,
            camera);
    }

    // Everything that must follow any order taking effect, in one place rather than at every
    // call site.
    private void OnOrderCoordinatorWorldChanged()
    {
        _selection.Refresh();
        _simulationLoop.RefreshBuildingsLabel();
        _simulationLoop.RefreshGravesLabel();
    }

    private Position FindFreeSpawnPosition()
    {
        const float minDistance = 1.2f;
        const float spread = 16f;

        return FreePositionSearch.Find(
            () => new Position(
                _continuity.CampCenter.X + ((GD.Randf() - 0.5f) * spread),
                _continuity.CampCenter.Y + ((GD.Randf() - 0.5f) * spread)),
            candidate => !_world.People.Any(p => WorldState.Distance(p.Position, candidate) < minDistance),
            maxAttempts: 20);
    }

    // The few people this one is closest to. Bonds never formed are absent, so a loner reads
    // "none" rather than a column of zeroes.
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

    // The debug inspector's raw dump of whoever is selected - kept apart from the player-facing
    // panels, which have their own refresh path.
    private void RefreshInfoLabel()
    {
        if (_selection.Grave is { } grave)
        {
            _mainUi.Inspector.ShowInfo(InspectorText.ForGrave(grave));
            return;
        }

        if (_selection.Person is not { } person)
        {
            _mainUi.Inspector.ShowInfo("No selection.");
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
        _mainUi.Inspector.ShowInfo(
            $"{person.Id}  {person.Name}{status}\n" +
            $"Position: {person.Position}\n" +
            $"Age: {AgeText(person)} ({_world.LifeStageOf(person)}, {person.Sex})\n" +
            $"Task: {InspectorText.ForTask(person)}\n" +
            $"Hunger: {person.Needs.Hunger}  Fatigue: {person.Needs.Fatigue}\n" +
            $"Skills: {skills}\n" +
            $"Known techniques: {techniques}\n" +
            $"Closest to: {BondsText(person)}\n" +
            $"Carrying: {carriedWeight}/{maxCarryWeight}\n" +
            $"Inventory: {inventory}");
    }

    private string AgeText(Person person) =>
        DurationText.For(_world.Clock.CurrentTick - person.BirthTick, _world.Configuration.Rules.TicksPerYear, _world.Configuration.Rules.TicksPerSeason);
}
