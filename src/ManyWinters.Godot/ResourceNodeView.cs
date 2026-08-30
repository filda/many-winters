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
    private const float DefaultSize = 0.6f;
    private const float TreeSize = 6f;
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
    private readonly CollisionObject3D.InputEventEventHandler _onMissedClick;
    private readonly Color _baseColor;
    private Sprite3D _sprite = null!;
    private Color _normalModulate;
    private Sprite3D? _fruitOverlay;
    private bool _isHovered;

    public ResourceNodeView(ResourceNodeId nodeId, ResourceKindId kind, bool canFell, Action<ResourceNodeId> onSelected, CollisionObject3D.InputEventEventHandler onMissedClick)
    {
        _nodeId = nodeId;
        _kind = kind;
        _canFell = canFell;
        _onSelected = onSelected;
        _onMissedClick = onMissedClick;

        var visual = LoadVisualDefinition(kind);
        _baseColor = visual?.Color ?? new Color(0.2f, 0.8f, 0.2f);
        Size = visual is { WorldHeight: > 0f } ? visual.WorldHeight : (canFell ? TreeSize : DefaultSize);
    }

    public float Size { get; }

    public override void _Ready()
    {
        InputRayPickable = true;

        var fallbackColor = EntityVisualVariation.Tint(_baseColor, _nodeId.Value);
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

        // Sized (and centered) to the sprite's actual drawn silhouette, not its full square
        // canvas - a canopy or a small icon doesn't fill the whole nominal Size, so a
        // collision box that size would hover/click-trigger well outside the visible shape.
        var extent = SpriteVisibleExtent.Compute(TexturePathFor(), Size);
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(extent.Width, extent.Height, extent.Width) },
            Position = new Vector3(extent.CenterXOffset, extent.CenterYOffset, 0),
        });

        InputEvent += OnInputEvent;
        // The broad-phase collision shape can only ever be a bounding box around the actual
        // silhouette (see SpriteVisibleExtent) - MouseExited still means "no longer even
        // close", but entering hover for real is decided pixel-by-pixel in OnInputEvent, not
        // here.
        MouseExited += OnMouseExited;
    }

    private void SetHovered(bool hovered)
    {
        if (hovered == _isHovered)
        {
            return;
        }

        _isHovered = hovered;
        _sprite.Modulate = hovered ? HoverHighlight.TintFor(_normalModulate) : _normalModulate;
        _sprite.Scale = Vector3.One * (hovered ? HoverHighlight.ScaleFactor : 1f);
    }

    private void OnMouseExited() => SetHovered(false);

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

    // Cached per kind, not reloaded per node - with decorations now spawning thousands of
    // ResourceNodes of a small handful of kinds (MapLoader.ScatterDecorations), calling
    // ResourceLoader.Load<T> once per node hammered the same handful of .tres paths thousands
    // of times in a single frame, which reliably crashed Godot's C# bridge (a GCHandle race
    // in ScriptManagerBridge.SwapGCHandleForType - "Handle is not initialized" - observed
    // consistently on startup once decoration counts got into the thousands).
    private static readonly Dictionary<ResourceKindId, ResourceVisualDefinition?> VisualDefinitionCache = new();

    private static ResourceVisualDefinition? LoadVisualDefinition(ResourceKindId kind)
    {
        if (VisualDefinitionCache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var path = $"res://Content/resources/{kind.Value}/{kind.Value}.tres";
        var definition = ResourceLoader.Exists(path) ? ResourceLoader.Load<ResourceVisualDefinition>(path) : null;
        VisualDefinitionCache[kind] = definition;
        return definition;
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (camera is not Camera3D camera3D)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseMotion:
                SetHovered(SpritePixelHit.IsOpaqueAt(camera3D, position, _sprite, TexturePathFor()));
                break;
            // The broad-phase collision box (see the constructor's Size / SpriteVisibleExtent)
            // is bigger than the actual silhouette - Godot only delivers a click to the
            // nearest pickable collider along the ray, so a click landing inside the box but
            // off the opaque pixels (e.g. on this node's own ground shadow) would otherwise be
            // silently swallowed here instead of reaching the ground underneath. Forward it to
            // whatever a plain ground click at this same spot would have done.
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
                if (SpritePixelHit.IsOpaqueAt(camera3D, position, _sprite, TexturePathFor()))
                {
                    _onSelected(_nodeId);
                }
                else
                {
                    _onMissedClick(camera, @event, position, normal, shapeIdx);
                }

                break;
        }
    }
}
