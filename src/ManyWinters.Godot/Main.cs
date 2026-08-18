using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class Main : Node3D
{
    private readonly WorldState _world = new();
    private readonly Dictionary<PersonId, PersonView> _views = new();

    private Label _infoLabel = null!;

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

        GD.Print($"Main ready. World has {_world.People.Count} people at tick {_world.Clock.CurrentTick}.");
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

    private void OnSpawnButtonPressed()
    {
        var x = (GD.Randf() - 0.5f) * 16f;
        var y = (GD.Randf() - 0.5f) * 16f;
        SpawnPerson($"Person {_world.People.Count + 1}", new Position(x, y));
    }

    private void OnPersonSelected(PersonId id)
    {
        var person = _world.People.FirstOrDefault(p => p.Id == id);
        _infoLabel.Text = person is null
            ? "No selection."
            : $"{person.Id}  {person.Name}\nPosition: {person.Position}\nHunger: {person.Needs.Hunger}  Fatigue: {person.Needs.Fatigue}";
    }
}
