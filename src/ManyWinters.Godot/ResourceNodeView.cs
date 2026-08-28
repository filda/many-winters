using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class ResourceNodeView : Area3D
{
    // Ordinary resources (berries, mushrooms, tubers...) read fine as a small icon sitting on
    // the ground. A fellable one is meant to be an actual tree standing in the world - a
    // touch shorter than the purely decorative background conifers/deciduous trees
    // (TerrainRenderer.ScatterDecoration, ~7-8m; cultivated fruit trees being a bit smaller
    // than wild forest ones is plausible) but nowhere near as small as the original 2.4m,
    // which read as a bush/sapling next to a scattered forest at that scale.
    public const float DefaultSize = 0.6f;
    public const float TreeSize = 6f;
    private const float MinScale = 0.85f;
    private const float MaxScale = 1.15f;
    private const float ShadowDiameterRatio = 0.7f / DefaultSize;

    // Layered a fraction in front of the base tree (see BillboardSprite.Create's
    // renderPriority) so it composites cleanly on top instead of z-fighting with the
    // canopy pixels directly underneath it - two billboards at the same position and
    // depth otherwise have no defined draw order.
    private const int FruitOverlayRenderPriority = 1;

    private readonly ResourceNodeId _nodeId;
    private readonly ResourceKindId _kind;
    private readonly bool _canFell;
    private readonly Action<ResourceNodeId> _onSelected;
    private Sprite3D _sprite = null!;
    private Color _normalModulate;
    private Sprite3D? _fruitOverlay;

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

        _sprite = BillboardSprite.Create(TexturePathFor(), Size, fallbackColor);
        _normalModulate = _sprite.Modulate;
        AddChild(_sprite);

        // Composite sprite: the tree itself never changes, but whether it's currently
        // bearing fruit does (see GatherCommand/WorldState.Advance) - a separate overlay
        // layer means that doesn't need its own whole "bare tree" texture per kind.
        if (_canFell)
        {
            _fruitOverlay = BillboardSprite.Create(
                FruitOverlayTexturePath(),
                Size,
                fallbackColor,
                SpriteBase3D.AlphaCutMode.Disabled,
                FruitOverlayRenderPriority);
            AddChild(_fruitOverlay);
        }

        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(Size, Size, Size) },
        });

        InputEvent += OnInputEvent;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    private void OnMouseEntered()
    {
        _sprite.Modulate = HoverHighlight.TintFor(_normalModulate);
        _sprite.Scale = Vector3.One * HoverHighlight.ScaleFactor;
    }

    private void OnMouseExited()
    {
        _sprite.Modulate = _normalModulate;
        _sprite.Scale = Vector3.One;
    }

    // No-op for a non-tree node (_fruitOverlay stays null) - only fellable kinds have a
    // fruit layer to show or hide.
    public void SetHasFruit(bool hasFruit)
    {
        if (_fruitOverlay is not null)
        {
            _fruitOverlay.Visible = hasFruit;
        }
    }

    // A fellable node draws as a standing tree, not the fruit/veg icon used for the rest of
    // Content/resources/{kind} - {kind}_tree.png sits alongside {kind}.png for those kinds.
    private string TexturePathFor() => _canFell
        ? $"res://Content/resources/{_kind.Value}/{_kind.Value}_tree.png"
        : $"res://Content/resources/{_kind.Value}/{_kind.Value}.png";

    private string FruitOverlayTexturePath() => $"res://Content/resources/{_kind.Value}/{_kind.Value}_tree_fruit.png";

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
