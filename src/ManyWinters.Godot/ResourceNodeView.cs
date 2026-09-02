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

    // Salts for EntityVisualVariation.RangeFor - distinct from each other so a tree's
    // trunk and canopy brighten/dim independently instead of moving in lockstep as one
    // uniform tint, which is what made every tree of a kind look like an identical
    // clone before the trunk/canopy split existed to vary them separately.
    private const int TrunkBrightnessSalt = 401;
    private const int CanopyBrightnessSalt = 402;
    private const float BrightnessJitterMin = 0.85f;
    private const float BrightnessJitterMax = 1.1f;

    // Width and height used to be one shared EntityVisualVariation.Scale draw - every
    // instance was a uniformly bigger/smaller copy of the same proportions. Separate salts
    // let a given tree end up tall-and-narrow or short-and-wide instead of just "the same
    // shape at a different zoom level".
    private const int WidthScaleSalt = 403;
    private const int HeightScaleSalt = 404;
    private const int MirrorSalt = 405;

    // Which of a kind's hand-authored trunk/canopy shape variants (art/generate_sprites.py -
    // e.g. apple_tree_trunk_v1.png alongside the original apple_tree_trunk.png) this
    // particular node drew - variant 0 is always the original, unsuffixed asset. Probing
    // stops at the first missing variant, so this is a sane upper bound, not a promise every
    // kind actually has this many.
    private const int TreeVariantSalt = 406;
    private const int MaxTreeVariantProbe = 8;

    // Branches are a separate shape-variant axis from the trunk/canopy pairing above (own
    // salt, own probe) - independently chosen so a tree's branch layer multiplies into new
    // combinations instead of always matching whichever trunk/canopy index was picked.
    private const int BranchVariantSalt = 407;
    private const int BranchBrightnessSalt = 408;

    // Fog of war's "remembered" tier (todo #13) - explored, but outside anyone's current sight
    // (see ExplorationState) - reads as a sepia-ish memory of the place rather than what's
    // actually there right now, same spirit as docs/ZemanConceptArt.png's own "Remembered"
    // panel. Multiplied into each layer's own base modulate (see _baseModulate/_Ready), not a
    // flat gray - a componentwise multiply that also darkens slightly keeps a warm, aged tone
    // rather than looking merely faded.
    private static readonly Color RememberedTint = new(0.78f, 0.68f, 0.52f);

    private readonly ResourceNodeId _nodeId;
    private readonly ResourceKindId _kind;
    private readonly bool _canFell;
    private readonly Action<ResourceNodeId> _onSelected;
    private readonly CollisionObject3D.InputEventEventHandler _onMissedClick;
    private readonly Color _baseColor;
    private int _variantIndex;
    private int _branchVariantIndex;
    private Sprite3D _sprite = null!;
    private string _spriteTexturePath = null!;
    private Color _baseModulate;
    private Color _normalModulate;
    private Sprite3D? _trunk;
    private string? _trunkTexturePath;
    private Color _trunkBaseModulate;
    private Color _trunkNormalModulate;
    private Sprite3D? _branches;
    private string? _branchesTexturePath;
    private Color _branchesBaseModulate;
    private Color _branchesNormalModulate;
    private Sprite3D? _fruitOverlay;
    private bool _isHovered;
    private bool _isRemembered;

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
        var widthScale = EntityVisualVariation.RangeFor(_nodeId.Value, WidthScaleSalt, MinScale, MaxScale);
        var heightScale = EntityVisualVariation.RangeFor(_nodeId.Value, HeightScaleSalt, MinScale, MaxScale);
        Scale = new Vector3(widthScale, heightScale, widthScale);

        // WorldPresenter positioned this node's own origin at groundHeight + Size/2,
        // assuming Scale stayed 1 - the sprite (centered, spanning local Y from -Size/2 to
        // +Size/2) then has its bottom edge land exactly on the ground. Scale.Y above
        // multiplies that -Size/2 by heightScale before it's added to Position, so a
        // shorter tree (heightScale < 1) floats with a visible gap under it and a taller
        // one (> 1) sinks in - the very "trees clearly in the air" a live check turned up.
        // Shifting this node's own Position by the same amount the scale just displaced the
        // ground-contact point cancels it back out, regardless of which way heightScale
        // went.
        Position += new Vector3(0f, (Size / 2f) * (heightScale - 1f), 0f);

        // A coin flip, not a continuous value - shared by every layer below (trunk, canopy,
        // fruit) so they stay aligned with each other; flipping trunk and canopy
        // independently would misalign a silhouette that was authored - and split - as one
        // asymmetric shape.
        var mirrored = EntityVisualVariation.RangeFor(_nodeId.Value, MirrorSalt, 0f, 1f) < 0.5f;

        var groundShadow = GroundShadow.Create(Size * ShadowDiameterRatio);
        groundShadow.Position += new Vector3(0, (-Size / 2f) + GroundShadow.GroundOffset, 0);
        AddChild(groundShadow);

        // A tree with a trunk/canopy split (art/generate_sprites.py's split_trunk_canopy)
        // renders as two separately-fadeable layers instead of one flattened sprite, so
        // occlusion fade can ghost just the canopy while the trunk (Camera.png's "trunks
        // stay solid" rule) never does - see Main.ComputeOccludingSprites. Falls back to
        // a single combined sprite for any kind without split art (non-tree resources,
        // or a future tree kind added before its split assets exist).
        if (HasTrunkCanopySplit(_kind))
        {
            var variantCount = TreeVariantCount(_kind);
            _variantIndex = variantCount > 1 ? EntityVisualVariation.IndexFor(_nodeId.Value, TreeVariantSalt, variantCount) : 0;

            _trunkTexturePath = TrunkTexturePathFor();
            _trunk = BillboardSprite.Create(_trunkTexturePath, Size, fallbackColor, excludeFromOcclusionFade: true);
            _trunk.Modulate *= LayerBrightnessVariation(TrunkBrightnessSalt);
            _trunk.FlipH = mirrored;
            _trunkBaseModulate = _trunk.Modulate;
            _trunkNormalModulate = _trunkBaseModulate;
            AddChild(_trunk);

            _spriteTexturePath = CanopyTexturePathFor();
            _sprite = BillboardSprite.Create(_spriteTexturePath, Size, fallbackColor);
            _sprite.Modulate *= LayerBrightnessVariation(CanopyBrightnessSalt);

            // Chosen independently of the trunk/canopy variant above (own salt) - see
            // BranchVariantSalt's own comment for why. Bare twig tips poking past the
            // canopy's edge near the trunk (see art/generate_sprites.py's
            // _fruit_tree_branch_layer) - wood, so it never fades either.
            if (HasBranchLayer(_kind))
            {
                var branchVariantCount = BranchVariantCount(_kind);
                _branchVariantIndex = branchVariantCount > 1 ? EntityVisualVariation.IndexFor(_nodeId.Value, BranchVariantSalt, branchVariantCount) : 0;

                _branchesTexturePath = BranchesTexturePathFor();
                _branches = BillboardSprite.Create(_branchesTexturePath, Size, fallbackColor, excludeFromOcclusionFade: true);
                _branches.Modulate *= LayerBrightnessVariation(BranchBrightnessSalt);
                _branches.FlipH = mirrored;
                _branchesBaseModulate = _branches.Modulate;
                _branchesNormalModulate = _branchesBaseModulate;
                AddChild(_branches);
            }
        }
        else
        {
            _spriteTexturePath = TexturePathFor();
            _sprite = BillboardSprite.Create(_spriteTexturePath, Size, fallbackColor);
        }

        _sprite.FlipH = mirrored;
        _baseModulate = _sprite.Modulate;
        _normalModulate = _baseModulate;
        AddChild(_sprite);

        // Composite sprite: the tree itself never changes, but whether it's currently
        // bearing fruit does (see GatherCommand/WorldState.Advance) - a separate overlay
        // layer means that doesn't need its own whole "bare tree" texture per kind. Driven by
        // which art files actually exist, not by CanFell - a fellable former-decoration tree
        // (conifer/deciduous/bush) has just the one plain sprite, no separate fruiting state.
        if (HasFruitOverlay(_kind))
        {
            _fruitOverlay = BillboardSprite.Create(
                FruitOverlayTexturePath(),
                Size,
                fallbackColor,
                SpriteBase3D.AlphaCutMode.Disabled,
                FruitOverlayRenderPriority);
            _fruitOverlay.FlipH = mirrored;
            AddChild(_fruitOverlay);
        }

        // Sized (and centered) to the sprite's actual drawn silhouette, not its full square
        // canvas - a canopy or a small icon doesn't fill the whole nominal Size, so a
        // collision box that size would hover/click-trigger well outside the visible shape.
        // A split tree has no single combined reference image any more (each shape variant
        // only exists as separate trunk/canopy files) - the true silhouette is the union of
        // both layers' own extents, which is exactly what the old single combined image's
        // extent already amounted to.
        var extent = SpriteVisibleExtent.Compute(_spriteTexturePath, Size);
        if (_trunk is not null)
        {
            extent = CombineExtents(extent, SpriteVisibleExtent.Compute(_trunkTexturePath!, Size));
        }

        if (_branches is not null)
        {
            extent = CombineExtents(extent, SpriteVisibleExtent.Compute(_branchesTexturePath!, Size));
        }
        // The extent is computed from the unflipped texture - a mirrored sprite's visible
        // content sits the same distance from center but on the opposite side.
        var centerXOffset = mirrored ? -extent.CenterXOffset : extent.CenterXOffset;
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(extent.Width, extent.Height, extent.Width) },
            Position = new Vector3(centerXOffset, extent.CenterYOffset, 0),
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

        // The trunk (and branches) highlight together with the canopy - hover is a single
        // "this whole tree is what you're pointing at" signal, unlike occlusion fade where
        // the layers deliberately behave differently.
        if (_trunk is not null)
        {
            _trunk.Modulate = hovered ? HoverHighlight.TintFor(_trunkNormalModulate) : _trunkNormalModulate;
            _trunk.Scale = Vector3.One * (hovered ? HoverHighlight.ScaleFactor : 1f);
        }

        if (_branches is not null)
        {
            _branches.Modulate = hovered ? HoverHighlight.TintFor(_branchesNormalModulate) : _branchesNormalModulate;
            _branches.Scale = Vector3.One * (hovered ? HoverHighlight.ScaleFactor : 1f);
        }
    }

    // Fog of war's "remembered" tier (WorldPresenter.RefreshExploration) - explored, but nobody
    // currently has this node in sight. Recomputes each layer's own _...NormalModulate from its
    // fixed _...BaseModulate (the brightness-jitter variation baked in at _Ready, never
    // touched again) so repeated calls - the exploration state can flip back and forth as the
    // group wanders - never compound the tint. Re-applies whatever's currently showing
    // (hovered or not) afterward, since SetHovered's own early-return would otherwise skip
    // refreshing the displayed color when the hover state itself hasn't changed.
    public void SetRemembered(bool remembered)
    {
        if (remembered == _isRemembered)
        {
            return;
        }

        _isRemembered = remembered;
        var tint = remembered ? RememberedTint : Colors.White;
        _normalModulate = _baseModulate * tint;
        _trunkNormalModulate = _trunkBaseModulate * tint;
        _branchesNormalModulate = _branchesBaseModulate * tint;

        _sprite.Modulate = _isHovered ? HoverHighlight.TintFor(_normalModulate) : _normalModulate;
        if (_trunk is not null)
        {
            _trunk.Modulate = _isHovered ? HoverHighlight.TintFor(_trunkNormalModulate) : _trunkNormalModulate;
        }

        if (_branches is not null)
        {
            _branches.Modulate = _isHovered ? HoverHighlight.TintFor(_branchesNormalModulate) : _branchesNormalModulate;
        }
    }

    private Color LayerBrightnessVariation(int salt)
    {
        var value = EntityVisualVariation.RangeFor(_nodeId.Value, salt, BrightnessJitterMin, BrightnessJitterMax);
        return new Color(value, value, value);
    }

    // The true silhouette of a split tree is the union of its trunk's and canopy's own
    // visible extents - equivalent to what a single combined image's extent already was,
    // since the two are an exact partition of it (see split_trunk_canopy).
    private static SpriteVisibleExtent.Extent CombineExtents(SpriteVisibleExtent.Extent a, SpriteVisibleExtent.Extent b)
    {
        var minX = Math.Min(a.CenterXOffset - (a.Width / 2f), b.CenterXOffset - (b.Width / 2f));
        var maxX = Math.Max(a.CenterXOffset + (a.Width / 2f), b.CenterXOffset + (b.Width / 2f));
        var minY = Math.Min(a.CenterYOffset - (a.Height / 2f), b.CenterYOffset - (b.Height / 2f));
        var maxY = Math.Max(a.CenterYOffset + (a.Height / 2f), b.CenterYOffset + (b.Height / 2f));
        return new SpriteVisibleExtent.Extent(maxX - minX, maxY - minY, (minX + maxX) / 2f, (minY + maxY) / 2f);
    }

    private void OnMouseExited() => SetHovered(false);

    // Lets HoverRescue ask "is this exact point actually opaque on you", for when some other
    // entity's broad-phase box won the pick instead - see its own doc comment for why that's
    // not just a hypothetical.
    public bool TryHoverAt(Camera3D camera, Vector3 worldPosition)
    {
        var opaque = IsOpaqueOnAnyLayer(camera, worldPosition);
        SetHovered(opaque);
        return opaque;
    }

    public bool TryClickAt(Camera3D camera, Vector3 worldPosition)
    {
        if (!IsOpaqueOnAnyLayer(camera, worldPosition))
        {
            return false;
        }

        _onSelected(_nodeId);
        return true;
    }

    // A split tree has no single combined texture any more (see _Ready) - a point counts as
    // opaque if it lands on any layer's own opaque pixels, since together they reconstruct
    // exactly the same silhouette a single combined texture used to represent (branches
    // included - a click on a bare twig tip should still select the tree).
    private bool IsOpaqueOnAnyLayer(Camera3D camera, Vector3 worldPosition) =>
        (_trunk is not null && SpritePixelHit.IsOpaqueAt(camera, worldPosition, _trunk, _trunkTexturePath!))
        || (_branches is not null && SpritePixelHit.IsOpaqueAt(camera, worldPosition, _branches, _branchesTexturePath!))
        || SpritePixelHit.IsOpaqueAt(camera, worldPosition, _sprite, _spriteTexturePath);

    // No-op for a non-tree node (_fruitOverlay stays null) - only fellable kinds have a
    // fruit layer to show or hide.
    public void SetHasFruit(bool hasFruit)
    {
        if (_fruitOverlay is not null)
        {
            _fruitOverlay.Visible = hasFruit;
        }
    }

    // A kind with a dedicated standing-tree sprite draws that instead of the fruit/veg icon
    // used for the rest of Content/resources/{kind} - {kind}_tree.png sits alongside
    // {kind}.png for those kinds (apple, pear...). Driven by which file actually exists, not
    // by CanFell - a fellable former-decoration tree (conifer/deciduous/bush) only ever had
    // the one plain {kind}.png to begin with.
    private string TexturePathFor() => BaseTexturePathFor(_kind);

    private static string BaseTexturePathFor(ResourceKindId kind) => HasTreeSprite(kind)
        ? $"res://Content/resources/{kind.Value}/{kind.Value}_tree.png"
        : $"res://Content/resources/{kind.Value}/{kind.Value}.png";

    // Fruit spots are authored per canopy variant (art/generate_sprites.py's
    // _APPLE_FRUIT_SPOT_VARIANTS/_PEAR_FRUIT_SPOT_VARIANTS) so they land inside whichever
    // canopy shape this node actually drew, not always the original's.
    private string FruitOverlayTexturePath() => VariantSuffixed($"res://Content/resources/{_kind.Value}/{_kind.Value}_tree_fruit.png", _variantIndex);

    // Split filenames sit alongside whichever image BaseTexturePathFor already uses as the
    // whole tree - {kind}_tree_trunk.png for a kind with its own dedicated standing-tree
    // sprite (apple, pear...), or {kind}_trunk.png for one that doesn't, since its kind id
    // already ends in "_tree" itself (conifer_tree, deciduous_tree) - {kind}_tree_trunk.png
    // there would double up the "_tree" and never match the actual asset on disk. A shape
    // variant beyond the first (_variantIndex > 0) adds one more suffix on top, e.g.
    // apple_tree_trunk_v1.png.
    private string TrunkTexturePathFor() => VariantSuffixed(InsertBeforeExtension(TexturePathFor(), "_trunk"), _variantIndex);

    private string CanopyTexturePathFor() => VariantSuffixed(InsertBeforeExtension(TexturePathFor(), "_canopy"), _variantIndex);

    // Shared, kind-independent asset (art/generate_sprites.py's _generic_tree_branch_layer)
    // - not derived from this kind's own texture path at all, unlike trunk/canopy. Its
    // anchor positions are pre-computed to clear the fruit-tree canopy family's shapes
    // specifically (see KindsWithGenericBranches), so every compatible kind quite
    // literally shares the exact same three files instead of each baking its own
    // near-duplicate copy.
    private const string SharedBranchesBasePath = "res://Content/branches/tree_branches.png";

    private string BranchesTexturePathFor() => VariantSuffixed(SharedBranchesBasePath, _branchVariantIndex);

    private static string InsertBeforeExtension(string path, string suffix)
    {
        var dot = path.LastIndexOf('.');
        return path[..dot] + suffix + path[dot..];
    }

    private static string VariantSuffixed(string path, int variant) => variant == 0 ? path : InsertBeforeExtension(path, $"_v{variant}");

    // How many hand-authored trunk/canopy shape variants this kind actually has on disk,
    // starting from 1 (the original, unsuffixed asset - always assumed present once
    // HasTrunkCanopySplit is true) and probing _v1, _v2, ... until one is missing. Cached
    // per kind, same reasoning as HasTreeSprite/HasFruitOverlay below.
    private static readonly Dictionary<ResourceKindId, int> TreeVariantCountCache = new();

    private static int TreeVariantCount(ResourceKindId kind)
    {
        if (TreeVariantCountCache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var basePath = BaseTexturePathFor(kind);
        var count = 1;
        for (var variant = 1; variant < MaxTreeVariantProbe; variant++)
        {
            var trunkPath = VariantSuffixed(InsertBeforeExtension(basePath, "_trunk"), variant);
            var canopyPath = VariantSuffixed(InsertBeforeExtension(basePath, "_canopy"), variant);
            if (!ResourceLoader.Exists(trunkPath) || !ResourceLoader.Exists(canopyPath))
            {
                break;
            }

            count++;
        }

        TreeVariantCountCache[kind] = count;
        return count;
    }

    // Geometrically valid only for kinds that share the fruit-tree canopy formula
    // (art/generate_sprites.py's _FRUIT_TREE_CANOPY_VARIANTS) - the shared branch layer's
    // anchor positions are pre-computed to clear specifically that family's canopy
    // shapes. conifer_tree's canopy is a different, independently-tiered shape (see
    // WorldState... no, generate_sprites.py's random_conifer_tiers) that was never
    // checked against, so it stays excluded rather than risking a branch drawn across
    // its leaves for real trees that happen to roll an unlucky tier layout.
    private static readonly HashSet<string> KindsWithGenericBranches = new() { "apple", "pear", "deciduous_tree" };

    private static readonly Dictionary<ResourceKindId, bool> HasBranchLayerCache = new();
    private static readonly Dictionary<ResourceKindId, int> BranchVariantCountCache = new();

    private static bool HasBranchLayer(ResourceKindId kind)
    {
        if (HasBranchLayerCache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var exists = KindsWithGenericBranches.Contains(kind.Value) && ResourceLoader.Exists(SharedBranchesBasePath);
        HasBranchLayerCache[kind] = exists;
        return exists;
    }

    // Same probing pattern as TreeVariantCount, but for the shared branches layer's own,
    // independent set of variants - identical for every kind that uses it, but still
    // cached per kind since callers key everything else off ResourceKindId too.
    private static int BranchVariantCount(ResourceKindId kind)
    {
        if (BranchVariantCountCache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var count = 1;
        for (var variant = 1; variant < MaxTreeVariantProbe; variant++)
        {
            if (!ResourceLoader.Exists(VariantSuffixed(SharedBranchesBasePath, variant)))
            {
                break;
            }

            count++;
        }

        BranchVariantCountCache[kind] = count;
        return count;
    }

    // Cached per kind (see VisualDefinitionCache above for why per-node ResourceLoader.Exists
    // calls at decoration scale are worth avoiding).
    private static readonly Dictionary<ResourceKindId, bool> HasTreeSpriteCache = new();
    private static readonly Dictionary<ResourceKindId, bool> HasFruitOverlayCache = new();
    private static readonly Dictionary<ResourceKindId, bool> HasTrunkCanopySplitCache = new();

    private static bool HasTreeSprite(ResourceKindId kind)
    {
        if (HasTreeSpriteCache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var exists = ResourceLoader.Exists($"res://Content/resources/{kind.Value}/{kind.Value}_tree.png");
        HasTreeSpriteCache[kind] = exists;
        return exists;
    }

    private static bool HasFruitOverlay(ResourceKindId kind)
    {
        if (HasFruitOverlayCache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var exists = ResourceLoader.Exists($"res://Content/resources/{kind.Value}/{kind.Value}_tree_fruit.png");
        HasFruitOverlayCache[kind] = exists;
        return exists;
    }

    private static bool HasTrunkCanopySplit(ResourceKindId kind)
    {
        if (HasTrunkCanopySplitCache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var basePath = BaseTexturePathFor(kind);
        var exists = ResourceLoader.Exists(InsertBeforeExtension(basePath, "_trunk"))
            && ResourceLoader.Exists(InsertBeforeExtension(basePath, "_canopy"));
        HasTrunkCanopySplitCache[kind] = exists;
        return exists;
    }

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
                if (!TryHoverAt(camera3D, position))
                {
                    HoverRescue.TryHoverElsewhere(this, camera3D, position);
                }

                break;
            // The broad-phase collision box (see the constructor's Size / SpriteVisibleExtent)
            // is bigger than the actual silhouette - Godot only delivers a click to the
            // nearest pickable collider along the ray, so a click landing inside the box but
            // off the opaque pixels (e.g. on this node's own ground shadow) would otherwise be
            // silently swallowed here instead of reaching the ground underneath. Try whatever
            // else is actually at this point first (HoverRescue's click counterpart), only
            // falling all the way back to a plain ground-click order if nothing there turns
            // out to be real either.
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
                if (!TryClickAt(camera3D, position)
                    && !HoverRescue.TryClickElsewhere(this, camera3D, position, MouseButton.Left))
                {
                    _onMissedClick(camera, @event, position, normal, shapeIdx);
                }

                break;
        }
    }
}
