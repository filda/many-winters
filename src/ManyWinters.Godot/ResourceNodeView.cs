using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class ResourceNodeView : Area3D
{
    public const float Size = 0.6f;

    private readonly ResourceNodeId _nodeId;
    private readonly Action<ResourceNodeId> _onSelected;

    public ResourceNodeView(ResourceNodeId nodeId, Action<ResourceNodeId> onSelected)
    {
        _nodeId = nodeId;
        _onSelected = onSelected;
    }

    public override void _Ready()
    {
        InputRayPickable = true;

        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(Size, Size, Size) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.8f, 0.2f) },
        });

        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(Size, Size, Size) },
        });

        InputEvent += OnInputEvent;
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            _onSelected(_nodeId);
        }
    }
}
