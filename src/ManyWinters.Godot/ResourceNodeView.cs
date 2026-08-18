using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class ResourceNodeView : Area3D
{
    public const float Size = 0.6f;

    private readonly ResourceNodeId _nodeId;
    private readonly ResourceKindId _kind;
    private readonly Action<ResourceNodeId> _onSelected;

    public ResourceNodeView(ResourceNodeId nodeId, ResourceKindId kind, Action<ResourceNodeId> onSelected)
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

    // Placeholder until visuals move to per-resource .tres definitions under Content/resources/<id>/.
    private static Color ColorFor(ResourceKindId kind) => kind.Value switch
    {
        "apple" => new Color(0.8f, 0.1f, 0.1f),
        "pear" => new Color(0.7f, 0.85f, 0.2f),
        "mushroom" => new Color(0.55f, 0.35f, 0.2f),
        "potato" => new Color(0.8f, 0.65f, 0.35f),
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
