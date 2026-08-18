using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class Main : Node3D
{
    private const double TickIntervalSeconds = 1.0;

    private readonly WorldState _world = new();
    private readonly Dictionary<PersonId, PersonView> _views = new();
    private readonly Dictionary<ResourceNodeId, ResourceNodeView> _resourceNodeViews = new();

    private Label _infoLabel = null!;
    private Label _tickLabel = null!;
    private PersonId? _selectedPersonId;
    private double _tickAccumulator;

    public override void _Ready()
    {
        GetViewport().PhysicsObjectPicking = true;

        SetUpCamera();
        SetUpLighting();
        SetUpGround();
        SetUpUi();

        const int columns = 5;
        for (var i = 0; i < 15; i++)
        {
            var x = ((i % columns) - (columns / 2f) + 0.5f) * 2f;
            var z = ((i / columns) - 1) * 2f;
            SpawnPerson($"Person {i + 1}", new Position(x, z));
        }

        SpawnResourceNode(ResourceKind.Apple, new Position(-6f, 5f), 200f);
        SpawnResourceNode(ResourceKind.Pear, new Position(0f, -5f), 200f);
        SpawnResourceNode(ResourceKind.Mushroom, new Position(6f, 5f), 200f);
        SpawnResourceNode(ResourceKind.Potato, new Position(-6f, -5f), 200f);
        SpawnResourceNode(ResourceKind.Apple, new Position(6f, -5f), 200f);

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
        _tickLabel.Text = $"Tick: {_world.Clock.CurrentTick}";
        RefreshInfoLabel();

        foreach (var person in _world.People)
        {
            if (_views.TryGetValue(person.Id, out var view))
            {
                view.SetAlive(person.IsAlive);
            }
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

    private void SetUpGround()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(20, 20) },
        });
    }

    private void SetUpUi()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        var panel = new PanelContainer
        {
            Position = new Vector2(16, 16),
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
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
        });
        canvas.AddChild(panel);

        var box = new VBoxContainer();
        panel.AddChild(box);

        box.AddChild(new Label
        {
            Text = "Left-click: select person. Right-click another person: teach them what the selected person knows. Click a resource node: gather (needs a selected person).",
            CustomMinimumSize = new Vector2(360, 0),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        _tickLabel = new Label { Text = "Tick: 0" };
        box.AddChild(_tickLabel);

        var spawnButton = new Button { Text = "Spawn Person" };
        spawnButton.Pressed += OnSpawnButtonPressed;
        box.AddChild(spawnButton);

        box.AddChild(new HSeparator());

        box.AddChild(new Label
        {
            Text = "Inspector",
            LabelSettings = new LabelSettings { FontSize = 18 },
        });

        _infoLabel = new Label { Text = "No selection." };
        box.AddChild(_infoLabel);
    }

    private void SpawnPerson(string name, Position position)
    {
        _world.Execute(new SpawnPersonCommand(name, position));
        var person = _world.People[^1];

        var view = new PersonView(person.Id, OnPersonClicked)
        {
            Name = person.Name,
            Position = new Vector3(position.X, PersonView.Height / 2f, position.Y),
        };
        AddChild(view);
        _views[person.Id] = view;
    }

    private void SpawnResourceNode(ResourceKind kind, Position position, float amount)
    {
        _world.Execute(new SpawnResourceNodeCommand(kind, position, amount));
        var node = _world.ResourceNodes[^1];

        var view = new ResourceNodeView(node.Id, node.Kind, OnResourceNodeSelected)
        {
            Position = new Vector3(position.X, ResourceNodeView.Size / 2f, position.Y),
        };
        AddChild(view);
        _resourceNodeViews[node.Id] = view;
    }

    private void OnSpawnButtonPressed()
    {
        SpawnPerson($"Person {_world.People.Count + 1}", FindFreeSpawnPosition());
    }

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
        if (node is { RemainingAmount: <= 0 } && _resourceNodeViews.TryGetValue(id, out var view))
        {
            view.QueueFree();
            _resourceNodeViews.Remove(id);
        }

        RefreshInfoLabel();
    }

    private void RefreshInfoLabel()
    {
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
        _infoLabel.Text =
            $"{person.Id}  {person.Name}{status}\n" +
            $"Position: {person.Position}\n" +
            $"Hunger: {person.Needs.Hunger}  Fatigue: {person.Needs.Fatigue}\n" +
            $"Skills: {skills}\n" +
            $"Known techniques: {techniques}";
    }
}
