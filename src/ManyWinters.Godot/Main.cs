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

namespace ManyWinters.Godot;

public partial class Main : Node3D
{
    private const double TickIntervalSeconds = 1.0;

    // Comfortably inside WorldState.MaxInteractionDistance (2f), but far enough out that a
    // person's own sprite doesn't overlap the resource node's.
    private const float ApproachDistance = 1.2f;

    private const string HeightmapPath = "res://Content/terrain/praha-liben/heightmap.json";
    private const string WaterwaysPath = "res://Content/terrain/praha-liben/waterways.json";
    private const string GroundTexturePath = "res://Content/terrain/ground.png";
    private const string ConiferTreePath = "res://Content/terrain/conifer_tree.png";
    private const string RockPilePath = "res://Content/terrain/rock_pile.png";
    private const int TreeCount = 90;
    private const int RockCount = 35;
    private const float TreeHeightMeters = 8f;
    private const float RockHeightMeters = 1.5f;
    private const float DecorationMinScale = 0.8f;
    private const float DecorationMaxScale = 1.3f;
    private const int DecorationScatterSeed = 1;

    // The starting band spans roughly 8x4 units and is centered exactly on campPosition
    // (see MapLoader.LoadDefault), so a close default zoom lets it fill most of the frame
    // right away rather than reading as a handful of specks in a huge empty field.
    private const float InitialZoomDistance = 10f;
    private const float MinZoom = 3f;
    private const float MaxZoom = 2000f;

    private static readonly Color TreeFallbackColor = new(0.20f, 0.32f, 0.18f);
    private static readonly Color RockFallbackColor = new(0.5f, 0.5f, 0.52f);

    private WorldState _world = null!;
    private WorldPresenter _presenter = null!;
    private TerrainRenderer _terrain = null!;
    private FreeCameraRig _cameraRig = null!;
    private Position _campCenter;

    private Label _infoLabel = null!;
    private Label _buildingsLabel = null!;
    private Label _gravesLabel = null!;
    private VBoxContainer _contextualActions = null!;
    private StatusBar _statusBar = null!;
    private PersonId? _selectedPersonId;
    private GraveId? _selectedGraveId;
    private double _tickAccumulator;

    // A person walking to a resource node they were told to gather from, rather than one
    // already in range when the order was given. Resolved once they arrive (see
    // ResolvePendingGathers), so clicking a distant node reads as "go gather that" instead
    // of silently doing nothing the way a bare out-of-range GatherCommand would.
    private readonly Dictionary<PersonId, ResourceNodeId> _pendingGathers = new();

    public override void _Ready()
    {
        var contentRoot = ProjectSettings.GlobalizePath("res://Content");
        var configuration = new WorldConfiguration(
            ResourceCatalog.LoadFromDirectory(Path.Combine(contentRoot, "resources")),
            SkillCatalog.LoadFromDirectory(Path.Combine(contentRoot, "skills")),
            RecipeCatalog.LoadFromDirectory(Path.Combine(contentRoot, "recipes")),
            BuildingCatalog.LoadFromDirectory(Path.Combine(contentRoot, "buildings")),
            ItemCatalog.LoadFromDirectory(Path.Combine(contentRoot, "items")),
            SeasonParameters.Default);
        var map = MapLoader.LoadDefault(configuration);
        _world = map.World;
        _campCenter = map.CampCenter;

        GetViewport().PhysicsObjectPicking = true;

        SetUpLighting();
        SetUpTerrain();
        SetUpCamera();
        SetUpUi();

        _presenter = new WorldPresenter(this, _world, OnPersonClicked, OnResourceNodeSelected, OnGraveSelected, _terrain.SampleHeight);

        GD.Print($"Main ready. World has {_world.People.Count} people and {_world.ResourceNodes.Count} resource nodes at tick {_world.Clock.CurrentTick}.");
    }

    public override void _Process(double delta)
    {
        _cameraRig.HandleInput((float)delta);

        _tickAccumulator += delta;
        if (_tickAccumulator < TickIntervalSeconds)
        {
            return;
        }

        _tickAccumulator -= TickIntervalSeconds;
        _world.Advance(1);
        ResolvePendingGathers();
        _statusBar.SetTick(_world.Clock.CurrentTick, _world.CurrentSeason);
        RefreshInfoLabel();
        RefreshBuildingsLabel();
        RefreshGravesLabel();

        foreach (var person in _world.People)
        {
            _presenter.SetPersonAlive(person.Id, person.IsAlive);
            _presenter.SetPersonPosition(person.Id, person.Position, (float)TickIntervalSeconds);
        }

        foreach (var node in _world.ResourceNodes)
        {
            if (!node.IsAlive)
            {
                // Catches nodes that withered from climate stress (see WorldState.Advance) -
                // felling already removes its own view immediately, this is just the passive
                // per-tick case.
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

        _cameraRig.HandleMouseInput(@event);
    }

    private void SetUpLighting()
    {
        AddChild(new DirectionalLight3D
        {
            Rotation = new Vector3(Mathf.DegToRad(-45), Mathf.DegToRad(-45), 0),
        });
    }

    private void SetUpTerrain()
    {
        _terrain = new TerrainRenderer(HeightmapPath, WaterwaysPath, GroundTexturePath);
        var groundBody = _terrain.BuildTerrainMesh(this);
        groundBody.InputEvent += OnGroundInputEvent;
        _terrain.BuildWaterways(this);

        var rng = new Random(DecorationScatterSeed);
        _terrain.ScatterDecoration(this, rng, TreeCount, ConiferTreePath, TreeHeightMeters, TreeFallbackColor, DecorationMinScale, DecorationMaxScale);
        _terrain.ScatterDecoration(this, rng, RockCount, RockPilePath, RockHeightMeters, RockFallbackColor, DecorationMinScale, DecorationMaxScale);
    }

    private void SetUpCamera()
    {
        var campX = (float)_campCenter.X;
        var campZ = (float)_campCenter.Y;
        var campPosition = new Vector3(campX, _terrain.SampleHeight(campX, campZ), campZ);
        _cameraRig = new FreeCameraRig(this, campPosition, InitialZoomDistance, MinZoom, MaxZoom);
    }

    private void SetUpUi()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        SetUpInspectorWindow(canvas);
        SetUpStatusBar(canvas);
    }

    private static StyleBoxFlat PanelBackground() => new()
    {
        BgColor = new Color(0f, 0f, 0f, 0.6f),
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 10,
        ContentMarginBottom = 10,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
    };

    // One floating, collapsible window for both the inspector and the action buttons -
    // the buttons are contextual to whichever person is selected, so they belong together
    // rather than in a separate always-open panel.
    private void SetUpInspectorWindow(CanvasLayer canvas)
    {
        const float width = 340f;

        var panel = new FloatingPanel("Inspector")
        {
            Position = new Vector2(16, 16),
            CustomMinimumSize = new Vector2(width, 0),
        };
        panel.AddThemeStyleboxOverride("panel", PanelBackground());
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

        _contextualActions = new VBoxContainer { Visible = false };
        panel.Body.AddChild(_contextualActions);

        var craftButton = new Button { Text = "Craft Axe (5 Wood)" };
        craftButton.Pressed += OnCraftButtonPressed;
        _contextualActions.AddChild(craftButton);

        var craftClothingButton = new Button { Text = "Craft Warm Clothing (10 Wood)" };
        craftClothingButton.Pressed += OnCraftClothingButtonPressed;
        _contextualActions.AddChild(craftClothingButton);

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

        _buildingsLabel = new Label { Text = "Buildings: none" };
        panel.Body.AddChild(_buildingsLabel);

        _gravesLabel = new Label { Text = "Graves: none" };
        panel.Body.AddChild(_gravesLabel);
    }

    private void SetUpStatusBar(CanvasLayer canvas)
    {
        _statusBar = new StatusBar();
        _statusBar.AddThemeStyleboxOverride("panel", PanelBackground());
        canvas.AddChild(_statusBar);
        _statusBar.SetTick(0, _world.CurrentSeason);
    }

    private void OnSpawnButtonPressed()
    {
        _world.Execute(new SpawnPersonCommand($"Person {_world.People.Count + 1}", FindFreeSpawnPosition()));
    }

    private void OnCraftButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(personId, new ItemKindId("axe")));
        RefreshInfoLabel();
    }

    private void OnCraftClothingButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(personId, new ItemKindId("warm_clothing")));
        RefreshInfoLabel();
    }

    private void OnBuildButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then build.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var buildPosition = FindFreeBuildingPosition(person.Position);
        _world.Execute(new ConstructCommand(personId, new BuildingKindId("storage_hut"), buildPosition));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnRepairButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then repair.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to repair yet.");
            return;
        }

        if (WorldState.Distance(person.Position, nearestBuilding.Position) > WorldState.MaxInteractionDistance)
        {
            _statusBar.Notify("The nearest building is too far away.");
            return;
        }

        _world.Execute(new RepairCommand(personId, nearestBuilding.Id));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnDepositButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then deposit.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to deposit into yet.");
            return;
        }

        if (WorldState.Distance(person.Position, nearestBuilding.Position) > WorldState.MaxInteractionDistance)
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

        _world.Execute(new DepositCommand(personId, nearestBuilding.Id, woodItem, amount));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnWithdrawButtonPressed()
    {
        const int withdrawAmount = 20;

        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then withdraw.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to withdraw from yet.");
            return;
        }

        if (WorldState.Distance(person.Position, nearestBuilding.Position) > WorldState.MaxInteractionDistance)
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

        _world.Execute(new WithdrawCommand(personId, nearestBuilding.Id, woodItem, amount));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnFellButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then fell.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var node = FindNearestFellableResourceNode(person.Position);
        if (node is null)
        {
            _statusBar.Notify("No trees nearby to fell.");
            return;
        }

        if (WorldState.Distance(person.Position, node.Position) > WorldState.MaxInteractionDistance)
        {
            _statusBar.Notify("The nearest tree is too far away.");
            return;
        }

        _world.Execute(new FellCommand(personId, node.Id));
        _presenter.RemoveResourceNodeView(node.Id);
        RefreshInfoLabel();
    }

    private void OnBuryButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then bury.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var deceased = FindNearestUnburiedDeceased(person.Position);
        if (deceased is null)
        {
            _statusBar.Notify("No one left to bury.");
            return;
        }

        if (WorldState.Distance(person.Position, deceased.Position) > WorldState.MaxInteractionDistance)
        {
            _statusBar.Notify("The nearest deceased person is too far away.");
            return;
        }

        _world.Execute(new BuryCommand(personId, deceased.Id));
        _presenter.RemovePersonView(deceased.Id);
        RefreshInfoLabel();
        RefreshGravesLabel();
    }

    private void OnLootButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then loot.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var deceased = FindNearestLootableDeceased(person.Position);
        if (deceased is null)
        {
            _statusBar.Notify("Nothing left to loot.");
            return;
        }

        if (WorldState.Distance(person.Position, deceased.Position) > WorldState.MaxInteractionDistance)
        {
            _statusBar.Notify("The nearest belongings are too far away.");
            return;
        }

        _world.Execute(new LootCommand(personId, deceased.Id));
        RefreshInfoLabel();
    }

    private Building? FindNearestBuilding(Position position) =>
        _world.Buildings.OrderBy(b => WorldState.Distance(b.Position, position)).FirstOrDefault();

    private ResourceNode? FindNearestFellableResourceNode(Position position) =>
        _world.ResourceNodes
            .Where(n => n.IsAlive && _world.ResourceCatalog.Get(n.Kind).CanFell)
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
        const int maxAttempts = 20;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = new Position(
                _campCenter.X + ((GD.Randf() - 0.5f) * spread),
                _campCenter.Y + ((GD.Randf() - 0.5f) * spread));
            var tooClose = _world.People.Any(p => WorldState.Distance(p.Position, candidate) < minDistance);
            if (!tooClose)
            {
                return candidate;
            }
        }

        return new Position(
            _campCenter.X + ((GD.Randf() - 0.5f) * spread),
            _campCenter.Y + ((GD.Randf() - 0.5f) * spread));
    }

    private Position FindFreeBuildingPosition(Position near)
    {
        const float minDistance = 1.5f;
        // Kept within MaxInteractionDistance's worst-case diagonal (spread/2 * sqrt(2)) so a freshly
        // picked spot is never too far away to actually construct on, since ConstructCommand itself
        // now requires proximity.
        const float spread = WorldState.MaxInteractionDistance;
        const int maxAttempts = 20;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = new Position(
                near.X + ((GD.Randf() - 0.5f) * spread),
                near.Y + ((GD.Randf() - 0.5f) * spread));
            var blocked = _world.Buildings.Any(b => WorldState.Distance(b.Position, candidate) < minDistance)
                || _world.People.Any(p => WorldState.Distance(p.Position, candidate) < minDistance);
            if (!blocked)
            {
                return candidate;
            }
        }

        return new Position(near.X + ((GD.Randf() - 0.5f) * spread), near.Y + ((GD.Randf() - 0.5f) * spread));
    }

    private void OnPersonClicked(PersonId id, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            TeachFromSelectedPersonTo(id);
            return;
        }

        _selectedPersonId = id;
        _selectedGraveId = null;
        _presenter.SetSelectedPerson(id);
        _contextualActions.Visible = true;
        RefreshInfoLabel();
    }

    private void OnGraveSelected(GraveId id)
    {
        _selectedGraveId = id;
        _selectedPersonId = null;
        _presenter.SetSelectedPerson(null);
        _contextualActions.Visible = false;
        RefreshInfoLabel();
    }

    private void TeachFromSelectedPersonTo(PersonId studentId)
    {
        if (_selectedPersonId is not { } teacherId || teacherId == studentId)
        {
            return;
        }

        var teacher = _world.People.FirstOrDefault(p => p.Id == teacherId);
        if (teacher is null)
        {
            return;
        }

        foreach (var technique in teacher.KnownTechniques)
        {
            _world.Execute(new TeachCommand(teacherId, studentId, technique));
        }

        RefreshInfoLabel();
    }

    private void OnResourceNodeSelected(ResourceNodeId id)
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then click a resource node to gather.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        var node = _world.ResourceNodes.FirstOrDefault(n => n.Id == id);
        if (person is null || node is null)
        {
            return;
        }

        if (WorldState.Distance(person.Position, node.Position) > WorldState.MaxInteractionDistance)
        {
            _pendingGathers[personId] = id;
            _world.Execute(new MoveCommand(personId, ApproachPosition(person.Position, node.Position, ApproachDistance)));
            RefreshInfoLabel();
            return;
        }

        _pendingGathers.Remove(personId);
        GatherFrom(personId, node);
        RefreshInfoLabel();
    }

    // Runs once a person who was walking to a resource node (see OnResourceNodeSelected)
    // arrives, so clicking a distant node reads as "go gather that" rather than the person
    // just standing there once they arrive.
    private void ResolvePendingGathers()
    {
        if (_pendingGathers.Count == 0)
        {
            return;
        }

        foreach (var (personId, nodeId) in _pendingGathers.ToList())
        {
            var person = _world.People.FirstOrDefault(p => p.Id == personId && p.IsAlive);
            var node = _world.ResourceNodes.FirstOrDefault(n => n.Id == nodeId);
            if (person is null || node is null)
            {
                _pendingGathers.Remove(personId);
                continue;
            }

            if (WorldState.Distance(person.Position, node.Position) > WorldState.MaxInteractionDistance)
            {
                continue;
            }

            _pendingGathers.Remove(personId);
            GatherFrom(personId, node);
        }
    }

    // Depleting a node down to zero doesn't remove its view - the plant/tree is still there,
    // just fruitless until RegenPerTick brings it back. Only IsAlive turning false (felled or
    // withered - see FellCommand, WorldState.Advance) means the thing itself is actually gone.
    private void GatherFrom(PersonId personId, ResourceNode node) => _world.Execute(new GatherCommand(personId, node.Id));

    // A destination short of the node's own position, approaching from wherever the
    // person currently is - so they end up standing next to the resource rather than
    // walking on top of and visually covering it.
    private static Position ApproachPosition(Position from, Position to, float standoffDistance)
    {
        var dx = from.X - to.X;
        var dy = from.Y - to.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance <= standoffDistance)
        {
            return from;
        }

        var ratio = standoffDistance / distance;
        return new Position(to.X + (dx * ratio), to.Y + (dy * ratio));
    }

    private void OnGroundInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            return;
        }

        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then click the ground to walk there.");
            return;
        }

        _world.Execute(new MoveCommand(personId, new Position(position.X, position.Z)));
        RefreshInfoLabel();
    }

    private void RefreshInfoLabel()
    {
        if (_selectedGraveId is { } graveId)
        {
            var grave = _world.Graves.FirstOrDefault(g => g.Id == graveId);
            _infoLabel.Text = grave is null ? "No selection." : GraveText(grave);
            return;
        }

        var person = _selectedPersonId is { } id ? _world.People.FirstOrDefault(p => p.Id == id) : null;
        if (person is null)
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
        _infoLabel.Text =
            $"{person.Id}  {person.Name}{status}\n" +
            $"Position: {person.Position}\n" +
            $"Age: {AgeText(person)}\n" +
            $"Task: {TaskText(person)}\n" +
            $"Hunger: {person.Needs.Hunger}  Fatigue: {person.Needs.Fatigue}\n" +
            $"Skills: {skills}\n" +
            $"Known techniques: {techniques}\n" +
            $"Inventory: {inventory}";
    }

    private static string TaskText(Person person) => person.Tasks.Current switch
    {
        MoveTask move => $"Walking to {move.Destination}",
        _ => "Idle",
    };

    private static string GraveText(Grave grave)
    {
        if (!grave.IsMarked)
        {
            return $"{grave.Id}\nPosition: {grave.Position}\nUnmarked grave - no record survives.";
        }

        var techniques = grave.KnownTechniques.Count > 0
            ? string.Join(", ", grave.KnownTechniques)
            : "none";
        var causeText = grave.CauseOfDeath switch
        {
            DeathCause.Hunger => " of hunger",
            DeathCause.OldAge => " of old age",
            _ => string.Empty,
        };
        return
            $"{grave.Id}\n" +
            $"Position: {grave.Position}\n" +
            $"{grave.Name}, died at age {grave.AgeAtDeath} winter{(grave.AgeAtDeath == 1 ? "" : "s")}{causeText}\n" +
            $"{ParentsText(grave.MotherName, grave.FatherName)}" +
            $"Known techniques: {techniques}";
    }

    private static string ParentsText(string? motherName, string? fatherName)
    {
        if (motherName is null && fatherName is null)
        {
            return string.Empty;
        }

        var parents = string.Join(" and ", new[] { motherName, fatherName }.Where(name => name is not null));
        return $"Child of {parents}\n";
    }

    private void RefreshBuildingsLabel()
    {
        _buildingsLabel.Text = "Buildings: " + (_world.Buildings.Count > 0
            ? string.Join(", ", _world.Buildings.Select(BuildingSummary))
            : "none");
    }

    private string AgeText(Person person)
    {
        var winters = _world.AgeInYears(person);
        if (winters >= 1)
        {
            return $"{winters} winter{(winters == 1 ? "" : "s")}";
        }

        var seasons = _world.AgeInSeasons(person);
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
