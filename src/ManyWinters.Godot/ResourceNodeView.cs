using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class ResourceNodeView : Area3D
{
    public const float Size = 0.6f;
    private const float MaxRotationDegrees = 25f;

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

        var color = EntityVisualVariation.Tint(ColorFor(_kind), _nodeId.Value);
        RotationDegrees = new Vector3(0f, EntityVisualVariation.RotationDegrees(_nodeId.Value, MaxRotationDegrees), 0f);

        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(Size, Size, Size) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
        });

        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(Size, Size, Size) },
        });

        InputEvent += OnInputEvent;
    }

    private static Color ColorFor(ResourceKindId kind)
    {
        var path = $"res://Content/resources/{kind.Value}/{kind.Value}.tres";
        var visual = ResourceLoader.Exists(path) ? ResourceLoader.Load<ResourceVisualDefinition>(path) : null;
        return visual?.Color ?? new Color(0.2f, 0.8f, 0.2f);
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            _onSelected(_nodeId);
        }
    }
}
