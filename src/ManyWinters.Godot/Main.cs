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

    private WorldState _world = null!;
    private WorldPresenter _presenter = null!;

    private Label _infoLabel = null!;
    private Label _tickLabel = null!;
    private Label _buildingsLabel = null!;
    private Label _gravesLabel = null!;
    private PersonId? _selectedPersonId;
    private GraveId? _selectedGraveId;
    private double _tickAccumulator;

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

        GetViewport().PhysicsObjectPicking = true;

        SetUpCamera();
        SetUpLighting();
        SetUpGround(map.TerrainWidth, map.TerrainDepth);
        SetUpUi();

        _presenter = new WorldPresenter(this, _world, OnPersonClicked, OnResourceNodeSelected, OnGraveSelected);

        GD.Print($"Main ready. World has {_world.People.Count} people and {_world.ResourceNodes.Count} resource nodes at tick {_world.Clock.CurrentTick}.");
    }

    public override void _Process(double delta)
    {
        _tickAccumulator += delta;
        if (_tickAccumulator < TickIntervalSeconds)
        {
            return;
        }

        _tickAccumulator -= TickIntervalSeconds;
        _world.Advance(1);
        _tickLabel.Text = $"Tick: {_world.Clock.CurrentTick}  Season: {_world.CurrentSeason}";
        RefreshInfoLabel();
        RefreshBuildingsLabel();
        RefreshGravesLabel();

        foreach (var person in _world.People)
        {
            _presenter.SetPersonAlive(person.Id, person.IsAlive);
            _presenter.SetPersonPosition(person.Id, person.Position, (float)TickIntervalSeconds);
        }

        GD.Print($"Tick {_world.Clock.CurrentTick}: {_world.People.Count(p => p.IsAlive)} of {_world.People.Count} people alive.");
    }

    private void SetUpCamera()
    {
        var camera = new Camera3D
        {
            Position = new Vector3(0, 8, 8),
        };
        AddChild(camera);
        camera.LookAt(Vector3.Zero, Vector3.Up);
    }

    private void SetUpLighting()
    {
        AddChild(new DirectionalLight3D
        {
            Rotation = new Vector3(Mathf.DegToRad(-45), Mathf.DegToRad(-45), 0),
        });
    }

    private void SetUpGround(float width, float depth)
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(width, depth) },
        });

        var groundBody = new StaticBody3D { InputRayPickable = true };
        groundBody.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(width, 0.1f, depth) },
            Position = new Vector3(0, -0.05f, 0),
        });
        groundBody.InputEvent += OnGroundInputEvent;
        AddChild(groundBody);
    }

    private void SetUpUi()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        SetUpControlsPanel(canvas);
        SetUpInspectorPanel(canvas);
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

    private void SetUpControlsPanel(CanvasLayer canvas)
    {
        var panel = new PanelContainer
        {
            Position = new Vector2(16, 16),
        };
        panel.AddThemeStyleboxOverride("panel", PanelBackground());
        canvas.AddChild(panel);

        var box = new VBoxContainer();
        panel.AddChild(box);

        box.AddChild(new Label
        {
            Text = "Left-click: select person. Right-click another person: teach them what the selected person knows. Click a resource node: gather (needs a selected person). Click a grave: view its record. Click empty ground: walk there (needs a selected person).",
            CustomMinimumSize = new Vector2(360, 0),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        _tickLabel = new Label { Text = $"Tick: 0  Season: {_world.CurrentSeason}" };
        box.AddChild(_tickLabel);

        var spawnButton = new Button { Text = "Spawn Person" };
        spawnButton.Pressed += OnSpawnButtonPressed;
        box.AddChild(spawnButton);

        var craftButton = new Button { Text = "Craft Axe (selected person, 5 Wood)" };
        craftButton.Pressed += OnCraftButtonPressed;
        box.AddChild(craftButton);

        var craftClothingButton = new Button { Text = "Craft Warm Clothing (selected person, 10 Wood)" };
        craftClothingButton.Pressed += OnCraftClothingButtonPressed;
        box.AddChild(craftClothingButton);

        var buildButton = new Button { Text = "Build Storage Hut (selected person, 20 Wood)" };
        buildButton.Pressed += OnBuildButtonPressed;
        box.AddChild(buildButton);

        var repairButton = new Button { Text = "Repair Nearest Building (selected person, 5 Wood)" };
        repairButton.Pressed += OnRepairButtonPressed;
        box.AddChild(repairButton);

        var depositButton = new Button { Text = "Deposit Wood (selected person -> nearest building)" };
        depositButton.Pressed += OnDepositButtonPressed;
        box.AddChild(depositButton);

        var withdrawButton = new Button { Text = "Withdraw Wood (nearest building -> selected person)" };
        withdrawButton.Pressed += OnWithdrawButtonPressed;
        box.AddChild(withdrawButton);

        var buryButton = new Button { Text = "Bury Nearest Dead Person (selected person)" };
        buryButton.Pressed += OnBuryButtonPressed;
        box.AddChild(buryButton);

        _buildingsLabel = new Label { Text = "Buildings: none" };
        box.AddChild(_buildingsLabel);

        _gravesLabel = new Label { Text = "Graves: none" };
        box.AddChild(_gravesLabel);
    }

    private void SetUpInspectorPanel(CanvasLayer canvas)
    {
        const float width = 340f;
        const float margin = 16f;
        var viewportWidth = GetViewport().GetVisibleRect().Size.X;

        var panel = new PanelContainer
        {
            Position = new Vector2(viewportWidth - width - margin, margin),
            CustomMinimumSize = new Vector2(width, 0),
        };
        panel.AddThemeStyleboxOverride("panel", PanelBackground());
        canvas.AddChild(panel);

        var box = new VBoxContainer();
        panel.AddChild(box);

        box.AddChild(new Label
        {
            Text = "Inspector",
            LabelSettings = new LabelSettings { FontSize = 18 },
        });

        _infoLabel = new Label
        {
            Text = "No selection.",
            CustomMinimumSize = new Vector2(width, 0),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        box.AddChild(_infoLabel);
    }

    private void OnSpawnButtonPressed()
    {
        _world.Execute(new SpawnPersonCommand($"Person {_world.People.Count + 1}", FindFreeSpawnPosition()));
    }

    private void OnCraftButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _infoLabel.Text = "Select a person first, then craft.";
            return;
        }

        _world.Execute(new CraftCommand(personId, new ItemKindId("axe")));
        RefreshInfoLabel();
    }

    private void OnCraftClothingButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _infoLabel.Text = "Select a person first, then craft.";
            return;
        }

        _world.Execute(new CraftCommand(personId, new ItemKindId("warm_clothing")));
        RefreshInfoLabel();
    }

    private void OnBuildButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _infoLabel.Text = "Select a person first, then build.";
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
            _infoLabel.Text = "Select a person first, then repair.";
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
            _infoLabel.Text = "No buildings to repair yet.";
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
            _infoLabel.Text = "Select a person first, then deposit.";
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
            _infoLabel.Text = "No buildings to deposit into yet.";
            return;
        }

        var woodItem = new ItemKindId("wood");
        var amount = person.Inventory.Get(woodItem);
        if (amount <= 0)
        {
            _infoLabel.Text = "No wood to deposit.";
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
            _infoLabel.Text = "Select a person first, then withdraw.";
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
            _infoLabel.Text = "No buildings to withdraw from yet.";
            return;
        }

        var woodItem = new ItemKindId("wood");
        var amount = Math.Min(withdrawAmount, nearestBuilding.Inventory.Get(woodItem));
        if (amount <= 0)
        {
            _infoLabel.Text = "No wood to withdraw.";
            return;
        }

        _world.Execute(new WithdrawCommand(personId, nearestBuilding.Id, woodItem, amount));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnBuryButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _infoLabel.Text = "Select a person first, then bury.";
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
            _infoLabel.Text = "No one left to bury.";
            return;
        }

        _world.Execute(new BuryCommand(personId, deceased.Id));
        _presenter.RemovePersonView(deceased.Id);
        RefreshInfoLabel();
        RefreshGravesLabel();
    }

    private Building? FindNearestBuilding(Position position) =>
        _world.Buildings.OrderBy(b => Distance(b.Position, position)).FirstOrDefault();

    private Person? FindNearestUnburiedDeceased(Position position) =>
        _world.People
            .Where(p => !p.IsAlive && !p.IsBuried)
            .OrderBy(p => Distance(p.Position, position))
            .FirstOrDefault();

    private Position FindFreeSpawnPosition()
    {
        const float minDistance = 1.2f;
        const int maxAttempts = 20;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = new Position((GD.Randf() - 0.5f) * 16f, (GD.Randf() - 0.5f) * 16f);
            var tooClose = _world.People.Any(p => Distance(p.Position, candidate) < minDistance);
            if (!tooClose)
            {
                return candidate;
            }
        }

        return new Position((GD.Randf() - 0.5f) * 16f, (GD.Randf() - 0.5f) * 16f);
    }

    private Position FindFreeBuildingPosition(Position near)
    {
        const float minDistance = 1.5f;
        const float spread = 4f;
        const int maxAttempts = 20;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = new Position(
                near.X + ((GD.Randf() - 0.5f) * spread),
                near.Y + ((GD.Randf() - 0.5f) * spread));
            var blocked = _world.Buildings.Any(b => Distance(b.Position, candidate) < minDistance)
                || _world.People.Any(p => Distance(p.Position, candidate) < minDistance);
            if (!blocked)
            {
                return candidate;
            }
        }

        return new Position(near.X + ((GD.Randf() - 0.5f) * spread), near.Y + ((GD.Randf() - 0.5f) * spread));
    }

    private static float Distance(Position a, Position b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
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
        RefreshInfoLabel();
    }

    private void OnGraveSelected(GraveId id)
    {
        _selectedGraveId = id;
        _selectedPersonId = null;
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
            _infoLabel.Text = "Select a person first, then click a resource node to gather.";
            return;
        }

        _world.Execute(new GatherCommand(personId, id));

        var node = _world.ResourceNodes.FirstOrDefault(n => n.Id == id);
        if (node is { RemainingAmount: <= 0 })
        {
            _presenter.RemoveResourceNodeView(id);
        }

        RefreshInfoLabel();
    }

    private void OnGroundInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            return;
        }

        if (_selectedPersonId is not { } personId)
        {
            _infoLabel.Text = "Select a person first, then click the ground to walk there.";
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
        return
            $"{grave.Id}\n" +
            $"Position: {grave.Position}\n" +
            $"{grave.Name}, died at age {grave.AgeAtDeath} winter{(grave.AgeAtDeath == 1 ? "" : "s")}\n" +
            $"Known techniques: {techniques}";
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
