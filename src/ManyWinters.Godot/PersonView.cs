using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class PersonView : Area3D
{
    public const float Radius = 0.3f;
    public const float Height = 1.8f;

    private readonly PersonId _personId;
    private readonly Action<PersonId> _onSelected;

    public PersonView(PersonId personId, Action<PersonId> onSelected)
    {
        _personId = personId;
        _onSelected = onSelected;
    }

    public override void _Ready()
    {
        InputRayPickable = true;

        AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = Radius, Height = Height },
        });

        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = Radius, Height = Height },
        });

        InputEvent += OnInputEvent;
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            _onSelected(_personId);
        }
    }
}
