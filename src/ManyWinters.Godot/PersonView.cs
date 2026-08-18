using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class PersonView : Area3D
{
    public const float Radius = 0.3f;
    public const float Height = 1.8f;

    private static readonly Color AliveColor = new(0.9f, 0.7f, 0.5f);
    private static readonly Color DeadColor = new(0.3f, 0.3f, 0.3f);

    private readonly PersonId _personId;
    private readonly Action<PersonId, MouseButton> _onClicked;
    private StandardMaterial3D _material = null!;

    public PersonView(PersonId personId, Action<PersonId, MouseButton> onClicked)
    {
        _personId = personId;
        _onClicked = onClicked;
    }

    public override void _Ready()
    {
        InputRayPickable = true;

        _material = new StandardMaterial3D { AlbedoColor = AliveColor };
        AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = Radius, Height = Height },
            MaterialOverride = _material,
        });

        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = Radius, Height = Height },
        });

        InputEvent += OnInputEvent;
    }

    public void SetAlive(bool isAlive)
    {
        _material.AlbedoColor = isAlive ? AliveColor : DeadColor;
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true } mouseEvent)
        {
            _onClicked(_personId, mouseEvent.ButtonIndex);
        }
    }
}
