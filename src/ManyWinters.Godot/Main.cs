using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
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
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot;

public partial class Main : Node3D
{
    private const string SelectionMarkerTexturePath = "res://Content/people/selection_marker.png";

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

    private Label _infoLabel = null!;
    private Label _buildingsLabel = null!;
    private Label _gravesLabel = null!;
    private VBoxContainer _contextualActions = null!;
    private StatusBar _statusBar = null!;
    private InscriptionOverlay _inscriptionOverlay = null!;
    private PausePanel _pausePanel = null!;
    private ChroniclePanel _chronicle = null!;
    private readonly EndingAnnouncements _endingAnnouncements = new();
    private TextureRect _selectionMarkerOverlay = null!;
    // The Person itself, not an id: commands and labels want the object and PersonView hands it
    // over on click, so nothing is looked up between "clicked" and "acted on".
    private Person? _selectedPerson;
    private Grave? _selectedGrave;
    private double _tickAccumulator;

    // Captured in _Ready, the one moment BandArrival.Of really means "just arrived"; TogglePause
    // calls BandArrival.Of again later only for its live population counts.
    private long _bandArrivalTick;

    // People walking to a resource node they were told to gather from; ResolvePendingGathers
    // fires the gather on arrival, so clicking a distant node means "go gather that" instead of
    // the silent no-op of an out-of-range GatherCommand.
    private readonly Dictionary<Person, ResourceNode> _pendingGathers = new();

    public override void _Ready()
    {
        var configuration = WorldConfiguration.LoadFromJson(catalog => ContentFiles.ReadJsonTree($"res://Content/{catalog}"));
        var map = MapLoader.LoadDefault(configuration);
        _world = map.World;

        // Everyone idles for up to IdleTask.MaxPauseTicks before their first wander leg, and with
        // the prologue holding the clock a band that then stood still read as stuck. Running those
        // ticks before the views exist keeps the same deterministic world, watched from a few
        // ticks in; it costs the band that much hunger before the player can act.
        _world.Advance(IdleTask.MaxPauseTicks + 1);

        _exploration = new RevealableExploration(_world.Exploration);
        _campCenter = map.CampCenter;

        GetViewport().PhysicsObjectPicking = true;

        SetUpLighting();
        SetUpSky();
        SetUpTerrain();
        SetUpCamera();
        SetUpUi();

        CloudScatter.Scatter(this, _terrain.Half);
        _cloudFogMask = new CloudFogMask(this, _cameraRig.Camera);

        _presenter = new WorldPresenter(this, _world, _exploration, OnPersonClicked, OnResourceNodeSelected, OnGraveSelected, OnMissedClick, _terrain.SampleHeight);
        _fogOfWar = new FogOfWarRenderer(_exploration, _terrain.Half, _cameraRig.Camera, _cloudFogMask);
        _groundClouds = new GroundClouds(this, _fogOfWar, _terrain.Half, _terrain.SampleHeight);

        var arrival = BandArrival.Of(_world);
        _bandArrivalTick = arrival.ArrivalTick;
        ShowInscription(Prologue.Write(arrival), offerAnotherBand: false);

        GD.Print($"Main ready. World has {_world.People.Count} people and {_world.ResourceNodes.Count} resource nodes at tick {_world.Clock.CurrentTick}.");
        // Answers "am I running the build I think I am" (a stale process after hot-reload or a
        // forgotten relaunch) with one log line; derived from the assembly, not bumped by hand.
        GD.Print($"Build tag: {BuildTag.For(AssemblyBuildTimeUtc())}");
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
        _cameraRig.HandleInput((float)delta);
        // Every frame, not per tick: the camera and the selected person's interpolated position
        // move continuously between ticks, so what stands in the way changes continuously too.
        UpdateOcclusionFade();
        UpdateSelectionMarkerOverlay();
        // Also every frame: hover is taken on mouse movement but can be lost without any - a
        // person can walk out from under a resting cursor (see HoverArbiter).
        _presenter.RevalidateHover();
        // Also every frame: the mask camera tracks the main camera's continuous movement.
        _cloudFogMask.Update();

        // Time stands still while an inscription is up: what it says is true of this moment, and
        // the player decides when the world moves on (at the start, a chance to look around before
        // hunger counts). A pause the player asked for (TogglePause) holds the clock the same way.
        if (_inscriptionOverlay.Visible || _pausePanel.Visible)
        {
            return;
        }

        _tickAccumulator += delta;
        if (_tickAccumulator < _pacing.TickIntervalSeconds)
        {
            return;
        }

        _tickAccumulator -= _pacing.TickIntervalSeconds;
        if (_selectedPerson is { } selectedPerson)
        {
            _world.Execute(new GrantIdleGraceCommand(selectedPerson, _pacing.SelectedPersonIdleGraceTicks));
        }

        _world.Advance(1);
        _presenter.RefreshExploration();
        _fogOfWar.Refresh();
        _groundClouds.Refresh();
        ResolvePendingGathers();
        _statusBar.SetTick(_world.Clock.CurrentTick, _world.CurrentSeason);
        RefreshInfoLabel();
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

        foreach (var node in _world.ResourceNodes)
        {
            if (!node.IsAlive)
            {
                // Nodes that withered from climate stress (see WorldState.Advance); felling
                // removes its own view immediately.
                _presenter.RemoveResourceNodeView(node.Id);
                continue;
            }

            _presenter.SetResourceNodeHasFruit(node.Id, node.RemainingAmount > 0);
        }

        GD.Print($"Tick {_world.Clock.CurrentTick}: {_world.People.Count(p => p.IsAlive)} of {_world.People.Count} people alive.");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.T })
        {
            _cameraRig.ToggleProjection();
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
        {
            // Space is Godot's default ui_accept: unless eaten here it also activates whichever
            // Control last took focus (Chronicle, the "?" button) on top of toggling the pause.
            TogglePause();
            GetViewport().SetInputAsHandled();
        }

        // Ahead of Godot's physics picking (which runs later, from unhandled input) so it wins even
        // when the pick would land on something opaque in front of a person - see
        // PresentationSettings.PersonClickScreenRadius.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton
            && GetViewport().GuiGetHoveredControl() is null
            && FindNearestPersonOnScreen(mouseButton.Position) is { } person)
        {
            OnPersonClicked(person, MouseButton.Left);
            GetViewport().SetInputAsHandled();
        }
    }

    // The already-selected person is never a candidate: re-selecting is a no-op, and the radius
    // around them swallowed every "step over there" click on the ground at their feet. Excluded,
    // the click falls through to picking; their own opaque pixels still re-select them.
    private Person? FindNearestPersonOnScreen(Vector2 screenPosition)
    {
        var camera = _cameraRig.Camera;
        Person? nearest = null;
        var nearestDistance = float.MaxValue;

        foreach (var person in _world.People)
        {
            if (person == _selectedPerson
                || _presenter.GetPersonGlobalPosition(person.Id) is not { } personGlobalPosition
                || camera.IsPositionBehind(personGlobalPosition))
            {
                continue;
            }

            var distance = camera.UnprojectPosition(personGlobalPosition).DistanceTo(screenPosition);
            if (distance <= _presentation.PersonClickScreenRadius && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = person;
            }
        }

        return nearest;
    }

    // _UnhandledInput, not _Input: _Input fires before the UI gets the event, so wheel/drag over
    // a Control would also zoom/rotate the camera underneath. That alone is not enough - a
    // ScrollContainer with nothing left to scroll lets the wheel fall through - so the camera also
    // ignores everything while the cursor is over any Control at all.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (GetViewport().GuiGetHoveredControl() is not null)
        {
            return;
        }

        _cameraRig.HandleMouseInput(@event);
    }

    // A decoration sprite between the camera and the selected person would otherwise hide them
    // with no way to tell where they went.
    private void UpdateOcclusionFade()
    {
        var occluding = ComputeOccludingSprites();

        // Re-applied every frame, not only on entering the set: the hover highlight rewrites the
        // same sprite's Modulate on every hover-state change and would undo the fade whenever the
        // cursor sits on an occluding canopy. Cheap - the set is a handful of sprites.
        // The faded set lives in BillboardSprite because picking consults it too (see
        // BillboardSprite.OcclusionFadedSprites); this is still the only place deciding membership.
        foreach (var sprite in occluding)
        {
            BillboardSprite.SetOcclusionFaded(sprite, true);
            SetSpriteAlpha(sprite, _presentation.OcclusionFadedAlpha);
        }

        // Materialized first: un-fading mutates the very set being walked.
        var noLongerOccluding = BillboardSprite.OcclusionFadedSprites.Where(sprite => !occluding.Contains(sprite)).ToList();
        foreach (var sprite in noLongerOccluding)
        {
            BillboardSprite.SetOcclusionFaded(sprite, false);

            // The owning view can be freed between frames (a corpse mid-fade gets buried and its
            // PersonView queued free); touching a freed sprite throws ObjectDisposedException,
            // which breaks out of _Process every frame and silently stalls ticks.
            if (IsInstanceValid(sprite))
            {
                SetSpriteAlpha(sprite, 1f);
            }
        }
    }

    // Walks BillboardSprite.LiveSprites rather than the scene tree: a per-frame FindChildren over
    // every ResourceNode's Area3D subtree stuttered the whole frame, camera included. Ground
    // shadows are plain Sprite3Ds (GroundShadow), never billboards, so need no exclusion; only
    // the selection's own sprites do, since they sit at the target itself.
    private HashSet<Sprite3D> ComputeOccludingSprites()
    {
        var result = new HashSet<Sprite3D>();

        Vector3 targetPosition;
        Node? selectedPersonNode = null;
        if (_selectedPerson is { } person && _presenter.GetPersonGlobalPosition(person.Id) is { } personPosition)
        {
            targetPosition = personPosition;
            selectedPersonNode = _presenter.GetPersonNode(person.Id);
        }
        else
        {
            // Nobody selected: fall back to the camera's orbit/pan target, so nothing gets to block
            // the view indefinitely just because no one is selected.
            targetPosition = _cameraRig.RigGlobalPosition;
        }

        if (SightLine.From(_cameraRig.CameraGlobalPosition, targetPosition) is not { } sightLine)
        {
            return result;
        }

        foreach (var sprite in BillboardSprite.LiveSprites)
        {
            if (selectedPersonNode is not null && selectedPersonNode.IsAncestorOf(sprite))
            {
                continue;
            }

            // A tree's trunk layer (see ResourceNodeView): Camera.png's "trunks stay solid" rule,
            // even when the trunk geometrically sits in the way itself.
            if (BillboardSprite.IsExcludedFromOcclusionFade(sprite))
            {
                continue;
            }

            // Rendered width, scale included - the same answer pixel-accurate picking uses
            // (BillboardUv.RenderedSize), not the authored canvas size.
            var texture = sprite.Texture!;
            var scale = sprite.GlobalTransform.Basis.Scale;
            var renderedWidth = BillboardUv.RenderedSize(sprite.PixelSize, texture.GetWidth(), texture.GetHeight(), scale.X, scale.Y).X;

            if (sightLine.IsBlockedBy(
                    sprite.GlobalPosition,
                    renderedWidth / 2f,
                    _presentation.OcclusionMargin,
                    _presentation.OcclusionDistanceTolerance))
            {
                result.Add(sprite);
            }
        }

        return result;
    }

    private static void SetSpriteAlpha(Sprite3D sprite, float alpha)
    {
        var color = sprite.Modulate;
        color.A = alpha;
        sprite.Modulate = color;
    }

    // A 2D overlay, not a 3D billboard (see PresentationSettings.SelectionMarkerScreenSize).
    // Camera3D.UnprojectPosition/IsPositionBehind do the projection; this anchors a Control on it.
    private void UpdateSelectionMarkerOverlay()
    {
        if (_selectedPerson is not { } person
            || _presenter.GetPersonGlobalPosition(person.Id) is not { } personPosition
            || _presenter.GetPersonHeadHeightOffset(person.Id) is not { } headHeightOffset)
        {
            _selectionMarkerOverlay.Visible = false;
            return;
        }

        var camera = _cameraRig.Camera;
        var headPosition = personPosition + new Vector3(0, headHeightOffset, 0);
        if (camera.IsPositionBehind(headPosition))
        {
            _selectionMarkerOverlay.Visible = false;
            return;
        }

        // SelectionMarkerScreenGap is screen pixels, so it applies to the projected point, not to
        // headPosition before projecting.
        var screenPosition = camera.UnprojectPosition(headPosition);
        _selectionMarkerOverlay.Position = new Vector2(
            screenPosition.X - (_selectionMarkerOverlay.Size.X / 2f),
            screenPosition.Y - _presentation.SelectionMarkerScreenGap - _selectionMarkerOverlay.Size.Y);
        _selectionMarkerOverlay.Visible = true;
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

        SetUpInspectorWindow(canvas);
        SetUpStatusBar(canvas);
        SetUpSelectionMarker(canvas);
        SetUpChronicle(canvas);
        SetUpInscriptionOverlay(canvas);
        SetUpPausePanel(canvas);
    }

    // Opposite the inspector, so the two can be open at once without covering each other.
    private void SetUpChronicle(CanvasLayer canvas)
    {
        _chronicle = new ChroniclePanel
        {
            Position = new Vector2(GetViewport().GetVisibleRect().Size.X - 476f, 16f),
        };
        _chronicle.AddThemeStyleboxOverride("panel", PanelChrome.Background());
        canvas.AddChild(_chronicle);
        _statusBar.ChronicleRequested += _chronicle.Toggle;
    }

    // Added last so it draws over everything else on the canvas, the inspector included.
    private void SetUpInscriptionOverlay(CanvasLayer canvas)
    {
        _inscriptionOverlay = new InscriptionOverlay();
        // The clock stood still, so the next tick is due the moment the inscription comes down -
        // a full interval later read as the world taking a second to notice.
        _inscriptionOverlay.Dismissed += () => _tickAccumulator = _pacing.TickIntervalSeconds;
        canvas.AddChild(_inscriptionOverlay);
    }

    // After the inscription overlay: the two never show at once today (ticking, and with it every
    // death, is on hold while either is up), but this is the one that should draw on top.
    private void SetUpPausePanel(CanvasLayer canvas)
    {
        _pausePanel = new PausePanel();
        canvas.AddChild(_pausePanel);
    }

    // Space toggles the clock at the player's request - ignored while an inscription holds it,
    // which is not the player's to override. Unpausing primes the tick accumulator like an
    // inscription dismissal does (SetUpInscriptionOverlay), so the world resumes next frame.
    private void TogglePause()
    {
        if (_inscriptionOverlay.Visible)
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
        var sinceArrival = DurationText(_world.Clock.CurrentTick - _bandArrivalTick);
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
            ShowInscription(Epitaph.Write(ending), offerAnotherBand: ending.Fate == BandFate.Ended);
        }
    }

    // Every inscription stops the clock until dismissed (see _Process); its title goes up on
    // the overlay and the whole of it into the chronicle, where it stays for the session.
    private void ShowInscription(Inscription inscription, bool offerAnotherBand)
    {
        _chronicle.Add(inscription);
        _statusBar.ShowChronicleButton();
        _inscriptionOverlay.Show(inscription, offerAnotherBand);
        GD.Print($"Inscription: {inscription.Title}");
    }

    private void SetUpSelectionMarker(CanvasLayer canvas)
    {
        _selectionMarkerOverlay = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(SelectionMarkerTexturePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(_presentation.SelectionMarkerScreenSize, _presentation.SelectionMarkerScreenSize),
            Visible = false,
        };
        canvas.AddChild(_selectionMarkerOverlay);
    }

    // One collapsible window for both the inspector and the action buttons - the buttons are
    // contextual to the selected person, so they belong together.
    private void SetUpInspectorWindow(CanvasLayer canvas)
    {
        const float width = 340f;

        var panel = new FloatingPanel("Inspector")
        {
            Position = new Vector2(16, 16),
            CustomMinimumSize = new Vector2(width, 0),
            // A Theme's DefaultFontSize cascades to every descendant Control without its own
            // override (unlike AddThemeFontSizeOverride, which affects one Control), so this alone
            // shrinks the title, every label and every button inside.
            Theme = new Theme { DefaultFontSize = _presentation.InspectorFontSize },
        };
        panel.AddThemeStyleboxOverride("panel", PanelChrome.Background());
        canvas.AddChild(panel);

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

        // A development view, not a gameplay one (see RevealableExploration): the whole map as if
        // fog of war did not exist. Sits with "Spawn Person" because it ignores the selection.
        var revealMapToggle = new CheckButton { Text = "Reveal Map" };
        revealMapToggle.Toggled += OnRevealMapToggled;
        panel.Body.AddChild(revealMapToggle);

        _contextualActions = new VBoxContainer { Visible = false };
        panel.Body.AddChild(_contextualActions);

        var craftButton = new Button { Text = "Craft Axe (5 Wood)" };
        craftButton.Pressed += OnCraftButtonPressed;
        _contextualActions.AddChild(craftButton);

        var craftClothingButton = new Button { Text = "Craft Warm Clothing (10 Wood)" };
        craftClothingButton.Pressed += OnCraftClothingButtonPressed;
        _contextualActions.AddChild(craftClothingButton);

        var craftBasketButton = new Button { Text = "Craft Basket (8 Wood)" };
        craftBasketButton.Pressed += OnCraftBasketButtonPressed;
        _contextualActions.AddChild(craftBasketButton);

        var craftBagButton = new Button { Text = "Craft Bag (10 Grass)" };
        craftBagButton.Pressed += OnCraftBagButtonPressed;
        _contextualActions.AddChild(craftBagButton);

        var buildButton = new Button { Text = "Build Storage Hut (20 Wood)" };
        buildButton.Pressed += OnBuildButtonPressed;
        _contextualActions.AddChild(buildButton);

        var repairButton = new Button { Text = "Repair Nearest Building (5 Wood)" };
        repairButton.Pressed += OnRepairButtonPressed;
        _contextualActions.AddChild(repairButton);

        var depositButton = new Button { Text = "Deposit Wood -> Nearest Building" };
        depositButton.Pressed += OnDepositButtonPressed;
        _contextualActions.AddChild(depositButton);

        var withdrawButton = new Button { Text = "Withdraw Wood <- Nearest Building" };
        withdrawButton.Pressed += OnWithdrawButtonPressed;
        _contextualActions.AddChild(withdrawButton);

        var fellButton = new Button { Text = "Fell Nearest Tree" };
        fellButton.Pressed += OnFellButtonPressed;
        _contextualActions.AddChild(fellButton);

        var buryButton = new Button { Text = "Bury Nearest Dead Person" };
        buryButton.Pressed += OnBuryButtonPressed;
        _contextualActions.AddChild(buryButton);

        var lootButton = new Button { Text = "Loot Nearest Dead Person" };
        lootButton.Pressed += OnLootButtonPressed;
        _contextualActions.AddChild(lootButton);

        var eatButton = new Button { Text = "Eat" };
        eatButton.Pressed += OnEatButtonPressed;
        _contextualActions.AddChild(eatButton);

        var childButton = new Button { Text = "Have Child With Nearest Person" };
        childButton.Pressed += OnHaveChildButtonPressed;
        _contextualActions.AddChild(childButton);

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
        _presenter.RefreshExploration();
        _fogOfWar.Refresh();
        _groundClouds.Refresh();
    }

    private void OnSpawnButtonPressed()
    {
        var name = PersonNames.Pool[Random.Shared.Next(PersonNames.Pool.Length)];
        _world.Execute(new SpawnPersonCommand(name, FindFreeSpawnPosition(), Person.Unknown, Person.Unknown));
    }

    private void OnCraftButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(person, new ItemKindId("axe")));
        RefreshInfoLabel();
    }

    private void OnCraftClothingButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(person, new ItemKindId("warm_clothing")));
        RefreshInfoLabel();
    }

    private void OnCraftBasketButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(person, new ItemKindId("basket")));
        RefreshInfoLabel();
    }

    private void OnCraftBagButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(person, new ItemKindId("bag")));
        RefreshInfoLabel();
    }

    private void OnBuildButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then build.");
            return;
        }

        var buildPosition = FindFreeBuildingPosition(person.Position);
        _world.Execute(new ConstructCommand(person, new BuildingKindId("storage_hut"), buildPosition));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnRepairButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then repair.");
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to repair yet.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, nearestBuilding.Position))
        {
            _statusBar.Notify("The nearest building is too far away.");
            return;
        }

        _world.Execute(new RepairCommand(person, nearestBuilding));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnDepositButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then deposit.");
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to deposit into yet.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, nearestBuilding.Position))
        {
            _statusBar.Notify("The nearest building is too far away.");
            return;
        }

        var woodItem = new ItemKindId("wood");
        var amount = person.Inventory.Get(woodItem);
        if (amount <= 0)
        {
            _statusBar.Notify("No wood to deposit.");
            return;
        }

        _world.Execute(new DepositCommand(person, nearestBuilding, woodItem, amount));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnWithdrawButtonPressed()
    {
        const int withdrawAmount = 20;

        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then withdraw.");
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to withdraw from yet.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, nearestBuilding.Position))
        {
            _statusBar.Notify("The nearest building is too far away.");
            return;
        }

        var woodItem = new ItemKindId("wood");
        var amount = Math.Min(withdrawAmount, nearestBuilding.Inventory.Get(woodItem));
        if (amount <= 0)
        {
            _statusBar.Notify("No wood to withdraw.");
            return;
        }

        _world.Execute(new WithdrawCommand(person, nearestBuilding, woodItem, amount));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnFellButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then fell.");
            return;
        }

        var node = FindNearestFellableResourceNode(person.Position);
        if (node is null)
        {
            _statusBar.Notify("No trees nearby to fell.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, node.Position))
        {
            _statusBar.Notify("The nearest tree is too far away.");
            return;
        }

        TeachBaseTechniqueIfNeeded(person, _world.Configuration.ResourceCatalog.Get(node.Kind).Skill);
        _world.Execute(new FellCommand(person, node));
        _presenter.RemoveResourceNodeView(node.Id);
        RefreshInfoLabel();
    }

    private void OnBuryButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then bury.");
            return;
        }

        // A dead-and-unburied selected person would otherwise be their own nearest deceased:
        // BuryCommand no-ops (the burier must be alive) but the view below would still vanish
        // with no grave created.
        if (!person.IsAlive)
        {
            _statusBar.Notify("A dead person can't bury anyone.");
            return;
        }

        var deceased = FindNearestUnburiedDeceased(person.Position);
        if (deceased is null)
        {
            _statusBar.Notify("No one left to bury.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, deceased.Position))
        {
            _statusBar.Notify("The nearest deceased person is too far away.");
            return;
        }

        _world.Execute(new BuryCommand(person, deceased));
        _presenter.RemovePersonView(deceased.Id);
        RefreshInfoLabel();
        RefreshGravesLabel();
    }

    private void OnLootButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then loot.");
            return;
        }

        // Same reasoning as OnBuryButtonPressed: a dead selected person could otherwise loot
        // their own corpse.
        if (!person.IsAlive)
        {
            _statusBar.Notify("A dead person can't loot anyone.");
            return;
        }

        var deceased = FindNearestLootableDeceased(person.Position);
        if (deceased is null)
        {
            _statusBar.Notify("Nothing left to loot.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, deceased.Position))
        {
            _statusBar.Notify("The nearest belongings are too far away.");
            return;
        }

        _world.Execute(new LootCommand(person, deceased));
        RefreshInfoLabel();
    }

    // The deliberate meal - gathering feeds a picker only while they are hungry enough (see
    // GatherCommand). Tries every carried kind rather than asking the player to pick one;
    // EatCommand no-ops for anything that isn't food.
    private void OnEatButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then eat.");
            return;
        }

        TeachBaseTechniqueIfNeeded(person, EatCommand.Skill);
        foreach (var item in person.Inventory.Counts.Keys.ToList())
        {
            if (person.Needs.Hunger <= 0f)
            {
                break;
            }

            _world.Execute(new EatCommand(person, item));
        }

        RefreshInfoLabel();
    }

    private Building? FindNearestBuilding(Position position) =>
        _world.Buildings.OrderBy(b => WorldState.Distance(b.Position, position)).FirstOrDefault();

    private ResourceNode? FindNearestFellableResourceNode(Position position) =>
        _world.ResourceNodes
            .Where(n => n.IsAlive && _world.Configuration.ResourceCatalog.Get(n.Kind).CanFell)
            .OrderBy(n => WorldState.Distance(n.Position, position))
            .FirstOrDefault();

    private Person? FindNearestUnburiedDeceased(Position position) =>
        _world.People
            .Where(p => !p.IsAlive && !p.IsBuried)
            .OrderBy(p => WorldState.Distance(p.Position, position))
            .FirstOrDefault();

    private Person? FindNearestLootableDeceased(Position position) =>
        _world.People
            .Where(p => !p.IsAlive && p.Inventory.Counts.Count > 0)
            .OrderBy(p => WorldState.Distance(p.Position, position))
            .FirstOrDefault();

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

    private Position FindFreeBuildingPosition(Position near)
    {
        const float minDistance = 1.5f;
        // Kept within MaxInteractionDistance's worst-case diagonal (spread/2 * sqrt(2)) so the
        // picked spot is never too far to construct on - ConstructCommand requires proximity.
        var spread = _world.Configuration.Rules.MaxInteractionDistance;

        return FreePositionSearch.Find(
            () => new Position(
                near.X + ((GD.Randf() - 0.5f) * spread),
                near.Y + ((GD.Randf() - 0.5f) * spread)),
            candidate => !_world.Buildings.Any(b => WorldState.Distance(b.Position, candidate) < minDistance)
                && !_world.People.Any(p => WorldState.Distance(p.Position, candidate) < minDistance),
            maxAttempts: 20);
    }

    // The player asking for a child directly rather than waiting for fondness (WorldState's own
    // pass). Only the fondness is skipped: the possibility checks are BirthCommand's, repeated
    // here purely to say which one stopped it instead of doing nothing.
    private void OnHaveChildButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first.");
            return;
        }

        if (!person.IsAlive)
        {
            _statusBar.Notify("The dead have no children.");
            return;
        }

        if (!_world.IsOldEnoughForChildren(person))
        {
            _statusBar.Notify($"{person.Name} is still a child.");
            return;
        }

        var partner = FindNearestPartner(person);
        if (partner is null)
        {
            _statusBar.Notify($"Nobody {person.Name} could have a child with is standing close enough.");
            return;
        }

        // Which of the two is the mother is decided by them, not by whoever the player clicked
        // first: she is the one the newborn will follow and feed from (see WorldState nursing).
        var mother = person.Sex == Sex.Female ? person : partner;
        var father = ReferenceEquals(mother, person) ? partner : person;

        if (_world.NursingInfantOf(mother) is { } nursing)
        {
            _statusBar.Notify($"{mother.Name} is still nursing {nursing.Name}.");
            return;
        }

        var name = PersonNames.Pool[Random.Shared.Next(PersonNames.Pool.Length)];
        _world.Execute(new BirthCommand(name, mother, father));
        RefreshInfoLabel();
    }

    // The nearest person this one could have a child with: grown, of the other sex, not close kin,
    // within reach. All BirthCommand's rules, matched here so the button can name the obstacle
    // rather than silently no-op, like the "nearest building" buttons do.
    private Person? FindNearestPartner(Person person) =>
        _world.People
            .Where(p => p != person
                && p.IsAlive
                && p.Sex != person.Sex
                && !Kinship.AreCloseKin(person, p)
                && _world.IsOldEnoughForChildren(p)
                && _world.IsWithinReach(person.Position, p.Position))
            .OrderBy(p => WorldState.Distance(p.Position, person.Position))
            .FirstOrDefault();

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

    private void OnPersonClicked(Person person, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            TeachFromSelectedPersonTo(person);
            return;
        }

        _selectedPerson = person;
        _selectedGrave = null;
        _contextualActions.Visible = true;
        RefreshInfoLabel();
    }

    private void OnGraveSelected(Grave grave)
    {
        _selectedGrave = grave;
        _selectedPerson = null;
        _contextualActions.Visible = false;
        RefreshInfoLabel();
    }

    private void TeachFromSelectedPersonTo(Person student)
    {
        if (_selectedPerson is not { } teacher || teacher == student)
        {
            return;
        }

        // Directing a person to teach at all is the player showing them how to teach in the
        // first place - same as TeachBaseTechniqueIfNeeded for gather/fell/eat.
        TeachBaseTechniqueIfNeeded(teacher, TeachCommand.TeachingSkill);

        foreach (var technique in teacher.KnownTechniques)
        {
            _world.Execute(new TeachCommand(teacher, student, technique));
        }

        RefreshInfoLabel();
    }

    private void OnResourceNodeSelected(ResourceNode node)
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then click a resource node to gather.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, node.Position))
        {
            _pendingGathers[person] = node;
            // Fully qualified: inside a Node3D, a bare `Position` is the node's own Vector3.
            _world.Execute(new MoveCommand(person, Core.World.Position.Approach(person.Position, node.Position, _presentation.ApproachDistance)));
            RefreshInfoLabel();
            return;
        }

        _pendingGathers.Remove(person);
        GatherFrom(person, node);
        RefreshInfoLabel();
    }

    // Fires the gather once a person walking to a node (see OnResourceNodeSelected) arrives.
    private void ResolvePendingGathers()
    {
        if (_pendingGathers.Count == 0)
        {
            return;
        }

        foreach (var (person, node) in _pendingGathers.ToList())
        {
            if (!person.IsAlive)
            {
                _pendingGathers.Remove(person);
                continue;
            }

            if (!_world.IsWithinReach(person.Position, node.Position))
            {
                continue;
            }

            _pendingGathers.Remove(person);
            GatherFrom(person, node);
        }
    }

    // Depleting a node to zero keeps its view - the plant is still there, fruitless until
    // RegenPerTick refills it. Only IsAlive turning false (felled or withered) removes it.
    private void GatherFrom(Person person, ResourceNode node)
    {
        TeachBaseTechniqueIfNeeded(person, _world.Configuration.ResourceCatalog.Get(node.Kind).Skill);
        _world.Execute(new GatherCommand(person, node));
    }

    // Nobody starts knowing anything (see SkillDefinition.BaseTechnique): the player directing an
    // action is how the person is shown the way, so every player-driven action grants its base
    // technique first rather than silently no-oping.
    private void TeachBaseTechniqueIfNeeded(Person person, SkillTypeId skill)
    {
        var baseTechnique = _world.Configuration.SkillCatalog.Get(skill).BaseTechnique;
        if (!person.KnownTechniques.Contains(baseTechnique))
        {
            _world.Execute(new GrantTechniqueCommand(person, baseTechnique));
        }
    }

    // The view has already tried HoverRescue.TryClickElsewhere (a full re-cast of the ray past
    // everything ruled out) before forwarding here, so this genuinely is a ground click.
    //
    // The position handed over is the ray's hit on the view's collision box - up in the air on a
    // tree-sized box's front face, tens of meters off the ground under the cursor (see
    // GroundPick) - so only the screen position is reused and the ground re-derived. A click
    // that finds no ground (sky past the terrain's edge) is dropped rather than guessed.
    private void OnMissedClick(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (camera is not Camera3D camera3D
            || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton)
        {
            return;
        }

        if (GroundPick.FindGround(camera3D, mouseButton.Position) is { } groundPosition)
        {
            OrderWalkTo(groundPosition);
        }
    }

    private void OnGroundInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            OrderWalkTo(position);
        }
    }

    private void OrderWalkTo(Vector3 groundPosition)
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then click the ground to walk there.");
            return;
        }

        _world.Execute(new MoveCommand(person, WorldSpace.ToSimulation(groundPosition)));
        RefreshInfoLabel();
    }

    private void RefreshInfoLabel()
    {
        if (_selectedGrave is { } grave)
        {
            _infoLabel.Text = InspectorText.ForGrave(grave);
            return;
        }

        if (_selectedPerson is not { } person)
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
        _buildingsLabel.Text = "Buildings: " + (_world.Buildings.Count > 0
            ? string.Join(", ", _world.Buildings.Select(BuildingSummary))
            : "none");
    }

    private string AgeText(Person person) => DurationText(_world.Clock.CurrentTick - person.BirthTick);

    // Winters where there have been any, else seasons - the same rule a person's own age reads
    // by (see AgeText), applied to any span of ticks rather than only one measured from a birth.
    private string DurationText(long elapsedTicks)
    {
        var winters = elapsedTicks / _world.Configuration.Rules.TicksPerYear;
        if (winters >= 1)
        {
            return $"{winters} winter{(winters == 1 ? "" : "s")}";
        }

        var seasons = elapsedTicks / _world.Configuration.Rules.TicksPerSeason;
        return $"{seasons} season{(seasons == 1 ? "" : "s")}";
    }

    private void RefreshGravesLabel()
    {
        _gravesLabel.Text = $"Graves: {_world.Graves.Count}";
    }

    private static string BuildingSummary(Building building)
    {
        var inventory = building.Inventory.Counts.Count > 0
            ? string.Join(", ", building.Inventory.Counts.Select(kv => $"{kv.Key} x{kv.Value}"))
            : "empty";
        return $"{building.Kind} #{building.Id} ({building.Condition:0}%) [{inventory}]";
    }
}
