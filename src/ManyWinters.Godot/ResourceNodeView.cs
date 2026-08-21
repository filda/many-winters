using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class ResourceNodeView : Area3D
{
    public const float Size = 0.6f;
    private const float MinScale = 0.85f;
    private const float MaxScale = 1.15f;
    private const float ShadowDiameter = 0.7f;

    private static readonly Color HoverOutlineColor = new(0.95f, 0.95f, 0.92f, 0.85f);

    private readonly ResourceNodeId _nodeId;
    private readonly ResourceKindId _kind;
    private readonly Action<ResourceNodeId> _onSelected;
    private Sprite3D _hoverOutline = null!;

    public ResourceNodeView(ResourceNodeId nodeId, ResourceKindId kind, Action<ResourceNodeId> onSelected)
    {
        _nodeId = nodeId;
        _kind = kind;
        _onSelected = onSelected;
    }

    public override void _Ready()
    {
        InputRayPickable = true;

        var fallbackColor = EntityVisualVariation.Tint(ColorFor(_kind), _nodeId.Value);
        Scale = Vector3.One * EntityVisualVariation.Scale(_nodeId.Value, MinScale, MaxScale);

        var groundShadow = GroundShadow.Create(ShadowDiameter);
        groundShadow.Position += new Vector3(0, (-Size / 2f) + GroundShadow.GroundOffset, 0);
        AddChild(groundShadow);

        _hoverOutline = SpriteOutline.Create(TexturePathFor(_kind), Size, HoverOutlineColor);
        _hoverOutline.Visible = false;
        AddChild(_hoverOutline);

        AddChild(BillboardSprite.Create(TexturePathFor(_kind), Size, fallbackColor));

        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(Size, Size, Size) },
        });

        InputEvent += OnInputEvent;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    private void OnMouseEntered() => _hoverOutline.Visible = true;

    private void OnMouseExited() => _hoverOutline.Visible = false;

    private static string TexturePathFor(ResourceKindId kind)
        => $"res://Content/resources/{kind.Value}/{kind.Value}.png";

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
