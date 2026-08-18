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

        for (var i = 0; i < 5; i++)
        {
            SpawnPerson($"Person {i + 1}", new Position((i - 2) * 1.5f, 0));
        }

        SpawnResourceNode(new Position(-4f, 3f), 100f);
        SpawnResourceNode(new Position(0f, -3f), 100f);
        SpawnResourceNode(new Position(4f, 3f), 100f);

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

        var box = new VBoxContainer
        {
            Position = new Vector2(16, 16),
        };
        canvas.AddChild(box);

        _tickLabel = new Label { Text = "Tick: 0" };
        box.AddChild(_tickLabel);

        _infoLabel = new Label { Text = "No selection." };
        box.AddChild(_infoLabel);

        var spawnButton = new Button { Text = "Spawn Person" };
        spawnButton.Pressed += OnSpawnButtonPressed;
        box.AddChild(spawnButton);
    }

    private void SpawnPerson(string name, Position position)
    {
        _world.Execute(new SpawnPersonCommand(name, position));
        var person = _world.People[^1];

        var view = new PersonView(person.Id, OnPersonSelected)
        {
            Name = person.Name,
            Position = new Vector3(position.X, PersonView.Height / 2f, position.Y),
        };
        AddChild(view);
        _views[person.Id] = view;
    }

    private void SpawnResourceNode(Position position, float amount)
    {
        _world.Execute(new SpawnResourceNodeCommand(ResourceKind.Food, position, amount));
        var node = _world.ResourceNodes[^1];

        var view = new ResourceNodeView(node.Id, OnResourceNodeSelected)
        {
            Position = new Vector3(position.X, ResourceNodeView.Size / 2f, position.Y),
        };
        AddChild(view);
        _resourceNodeViews[node.Id] = view;
    }

    private void OnSpawnButtonPressed()
    {
        var x = (GD.Randf() - 0.5f) * 16f;
        var y = (GD.Randf() - 0.5f) * 16f;
        SpawnPerson($"Person {_world.People.Count + 1}", new Position(x, y));
    }

    private void OnPersonSelected(PersonId id)
    {
        _selectedPersonId = id;
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
        _infoLabel.Text = $"{person.Id}  {person.Name}{status}\nPosition: {person.Position}\nHunger: {person.Needs.Hunger}  Fatigue: {person.Needs.Fatigue}";
    }
}
