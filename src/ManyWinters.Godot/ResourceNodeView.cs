using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class ResourceNodeView : Area3D
{
    public const float Size = 0.6f;

    private readonly ResourceNodeId _nodeId;
    private readonly ResourceKind _kind;
    private readonly Action<ResourceNodeId> _onSelected;

    public ResourceNodeView(ResourceNodeId nodeId, ResourceKind kind, Action<ResourceNodeId> onSelected)
    {
        _nodeId = nodeId;
        _kind = kind;
        _onSelected = onSelected;
    }

    public override void _Ready()
    {
        InputRayPickable = true;

        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(Size, Size, Size) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = ColorFor(_kind) },
        });

        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(Size, Size, Size) },
        });

        InputEvent += OnInputEvent;
    }

    private static Color ColorFor(ResourceKind kind) => kind switch
    {
        ResourceKind.Apple => new Color(0.8f, 0.1f, 0.1f),
        ResourceKind.Pear => new Color(0.7f, 0.85f, 0.2f),
        ResourceKind.Mushroom => new Color(0.55f, 0.35f, 0.2f),
        ResourceKind.Potato => new Color(0.8f, 0.65f, 0.35f),
        _ => new Color(0.2f, 0.8f, 0.2f),
    };

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            _onSelected(_nodeId);
        }
    }
}
