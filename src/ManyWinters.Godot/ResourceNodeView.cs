using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class ResourceNodeView : Area3D
{
    // Ordinary resources (berries, mushrooms, tubers...) read fine as a small icon sitting on
    // the ground. A fellable one is meant to be an actual tree standing in the world - kept
    // shorter than the purely decorative background conifers (TerrainRenderer.ScatterDecoration,
    // ~8m) so it doesn't read as generic forest, but tall enough to be visibly a tree rather
    // than a ground-level pickup, and findable without confusing it for the (non-interactive)
    // decoration.
    public const float DefaultSize = 0.6f;
    public const float TreeSize = 2.4f;
    private const float MinScale = 0.85f;
    private const float MaxScale = 1.15f;
    private const float ShadowDiameterRatio = 0.7f / DefaultSize;

    private static readonly Color HoverOutlineColor = new(0.95f, 0.95f, 0.92f, 0.85f);

    private readonly ResourceNodeId _nodeId;
    private readonly ResourceKindId _kind;
    private readonly bool _canFell;
    private readonly Action<ResourceNodeId> _onSelected;
    private Sprite3D _hoverOutline = null!;

    public ResourceNodeView(ResourceNodeId nodeId, ResourceKindId kind, bool canFell, Action<ResourceNodeId> onSelected)
    {
        _nodeId = nodeId;
        _kind = kind;
        _canFell = canFell;
        _onSelected = onSelected;
        Size = canFell ? TreeSize : DefaultSize;
    }

    public float Size { get; }

    public override void _Ready()
    {
        InputRayPickable = true;

        var fallbackColor = EntityVisualVariation.Tint(ColorFor(_kind), _nodeId.Value);
        Scale = Vector3.One * EntityVisualVariation.Scale(_nodeId.Value, MinScale, MaxScale);

        var groundShadow = GroundShadow.Create(Size * ShadowDiameterRatio);
        groundShadow.Position += new Vector3(0, (-Size / 2f) + GroundShadow.GroundOffset, 0);
        AddChild(groundShadow);

        _hoverOutline = SpriteOutline.Create(TexturePathFor(), Size, HoverOutlineColor);
        _hoverOutline.Visible = false;
        AddChild(_hoverOutline);

        AddChild(BillboardSprite.Create(TexturePathFor(), Size, fallbackColor));

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

    // A fellable node draws as a standing tree, not the fruit/veg icon used for the rest of
    // Content/resources/{kind} - {kind}_tree.png sits alongside {kind}.png for those kinds.
    private string TexturePathFor() => _canFell
        ? $"res://Content/resources/{_kind.Value}/{_kind.Value}_tree.png"
        : $"res://Content/resources/{_kind.Value}/{_kind.Value}.png";

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
