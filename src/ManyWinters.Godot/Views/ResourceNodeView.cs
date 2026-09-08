using Godot;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// A resource - a berry bush, a mushroom, a standing tree - drawn out of up to four
// layers so the occlusion fade can ghost a canopy while leaving its trunk solid, and so
// a tree can grow fruit without a second whole texture. Everything about being a sprite
// standing in the world (hover, clicks, the fog fade, the ground shadow, the collision
// shape cut to the silhouette) comes from SpriteEntityView; what is here is which layers
// this kind has, where their images live, and what its seed varies.
internal partial class ResourceNodeView : SpriteEntityView
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

    // For a kind with no .tres visual definition of its own - a placeholder green, only
    // ever seen if its art is missing too.
    private static readonly Color DefaultColor = new(0.2f, 0.8f, 0.2f);

    private readonly ResourceNode _node;
    private readonly ResourceKindId _kind;

    // For WorldPresenter, which needs the node back when a view has to return to pending
    // (see WorldPresenter.RefreshExploration).
    public ResourceNode Node => _node;
    private readonly Action<ResourceNode> _onSelected;
    private readonly Color _baseColor;
    private int _variantIndex;
    private int _branchVariantIndex;

    // The only layer this view keeps a handle on after building itself: whether the tree is
    // bearing fruit changes while it stands there (see SetHasFruit). Tinting, scaling,
    // measuring and picking against all of the layers is SpriteEntityView's job.
    private SpriteLayer? _fruit;

    // Internal for the same reason as PersonView's own constructor - see there.
    internal ResourceNodeView(ResourceNode node, bool canFell, HoverArbiter hover, Action<ResourceNode> onSelected, InputEventEventHandler onMissedClick)
        : base(NominalHeightFor(node.Kind, canFell), hover, onMissedClick)
    {
        _node = node;
        _kind = node.Kind;
        _onSelected = onSelected;
        _baseColor = LoadVisualDefinition(node.Kind)?.Color ?? DefaultColor;
    }

    // What WorldPresenter places this node by - it sits the origin half a height above the
    // ground - and the height every one of the layers below is created at.
    public float Size => NominalHeight;

    // A kind's own authored height where it has one, otherwise a standing tree's or a ground
    // icon's default. Static because the base class needs the answer before this view has any
    // fields of its own.
    private static float NominalHeightFor(ResourceKindId kind, bool canFell)
    {
        var visual = LoadVisualDefinition(kind);
        return visual is { WorldHeight: > 0f } ? visual.WorldHeight : (canFell ? TreeSize : DefaultSize);
    }

    protected override void Build()
    {
        var fallbackColor = EntityVisualVariation.Tint(_baseColor, _node.Id.Seed);
        ScaleAndKeepGroundContact(
            EntityVisualVariation.RangeFor(_node.Id.Seed, WidthScaleSalt, MinScale, MaxScale),
            EntityVisualVariation.RangeFor(_node.Id.Seed, HeightScaleSalt, MinScale, MaxScale));

        // A coin flip, not a continuous value - shared by every layer below (trunk, canopy,
        // fruit) so they stay aligned with each other; flipping trunk and canopy
        // independently would misalign a silhouette that was authored - and split - as one
        // asymmetric shape.
        var mirrored = EntityVisualVariation.RangeFor(_node.Id.Seed, MirrorSalt, 0f, 1f) < 0.5f;

        SetUpGroundShadow(Size * ShadowDiameterRatio);

        // A tree with a trunk/canopy split (art/generate_sprites.py's split_trunk_canopy)
        // renders as two separately-fadeable layers instead of one flattened sprite, so
        // occlusion fade can ghost just the canopy while the trunk (Camera.png's "trunks
        // stay solid" rule) never does - see Main.ComputeOccludingSprites. Falls back to
        // a single combined sprite for any kind without split art (non-tree resources,
        // or a future tree kind added before its split assets exist).
        //
        // Registered trunk first, then branches, then the canopy on top of both: all three
        // sit at the same render priority and roughly the same depth, so their order in the
        // tree is what settles which draws over which.
        string canopyTexturePath;
        Color? canopyBrightness = null;
        if (HasTrunkCanopySplit(_kind))
        {
            var variantCount = TreeVariantCount(_kind);
            _variantIndex = variantCount > 1 ? EntityVisualVariation.IndexFor(_node.Id.Seed, TreeVariantSalt, variantCount) : 0;

            var trunkTexturePath = TrunkTexturePathFor();
            var trunk = BillboardSprite.Create(trunkTexturePath, Size, fallbackColor, excludeFromOcclusionFade: true);
            trunk.Modulate *= LayerBrightnessVariation(TrunkBrightnessSalt);
            trunk.FlipH = mirrored;
            Register(trunk, trunkTexturePath);

            canopyTexturePath = CanopyTexturePathFor();
            canopyBrightness = LayerBrightnessVariation(CanopyBrightnessSalt);

            // Chosen independently of the trunk/canopy variant above (own salt) - see
            // BranchVariantSalt's own comment for why. Bare twig tips poking past the
            // canopy's edge near the trunk (see art/generate_sprites.py's
            // _fruit_tree_branch_layer) - wood, so it never fades either.
            if (HasBranchLayer(_kind))
            {
                var branchVariantCount = BranchVariantCount(_kind);
                _branchVariantIndex = branchVariantCount > 1 ? EntityVisualVariation.IndexFor(_node.Id.Seed, BranchVariantSalt, branchVariantCount) : 0;

                var branchesTexturePath = BranchesTexturePathFor();
                var branches = BillboardSprite.Create(branchesTexturePath, Size, fallbackColor, excludeFromOcclusionFade: true);
                branches.Modulate *= LayerBrightnessVariation(BranchBrightnessSalt);
                branches.FlipH = mirrored;
                Register(branches, branchesTexturePath);
            }
        }
        else
        {
            canopyTexturePath = TexturePathFor();
        }

        var canopy = BillboardSprite.Create(canopyTexturePath, Size, fallbackColor);
        if (canopyBrightness is { } brightness)
        {
            canopy.Modulate *= brightness;
        }

        canopy.FlipH = mirrored;
        Register(canopy, canopyTexturePath);

        // Composite sprite: the tree itself never changes, but whether it's currently
        // bearing fruit does (see GatherCommand/WorldState.Advance) - a separate overlay
        // layer means that doesn't need its own whole "bare tree" texture per kind. Driven by
        // which art files actually exist, not by CanFell - a fellable former-decoration tree
        // (conifer/deciduous/bush) has just the one plain sprite, no separate fruiting state.
        //
        // Not a picking layer: the fruit is drawn inside the canopy's own silhouette, so it
        // has no pixels of its own to be pointed at and nothing to add to the click rectangle.
        if (HasFruitOverlay(_kind))
        {
            var fruitTexturePath = FruitOverlayTexturePath();
            var fruit = BillboardSprite.Create(
                fruitTexturePath,
                Size,
                fallbackColor,
                SpriteBase3D.AlphaCutMode.Disabled,
                FruitOverlayRenderPriority);
            fruit.FlipH = mirrored;
            _fruit = Register(fruit, fruitTexturePath, picks: false);
        }
    }

    protected override bool OnClicked(MouseButton button)
    {
        _onSelected(_node);
        return true;
    }

    private Color LayerBrightnessVariation(int salt)
    {
        var value = EntityVisualVariation.RangeFor(_node.Id.Seed, salt, BrightnessJitterMin, BrightnessJitterMax);
        return new Color(value, value, value);
    }

    // No-op for a non-tree node (_fruit stays null) - only kinds with fruit art have a layer
    // to show or hide.
    public void SetHasFruit(bool hasFruit)
    {
        if (_fruit is not null)
        {
            _fruit.Sprite.Visible = hasFruit;
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
    private string FruitOverlayTexturePath() => TexturePaths.VariantSuffixed($"res://Content/resources/{_kind.Value}/{_kind.Value}_tree_fruit.png", _variantIndex);

    // Split filenames sit alongside whichever image BaseTexturePathFor already uses as the
    // whole tree - {kind}_tree_trunk.png for a kind with its own dedicated standing-tree
    // sprite (apple, pear...), or {kind}_trunk.png for one that doesn't, since its kind id
    // already ends in "_tree" itself (conifer_tree, deciduous_tree) - {kind}_tree_trunk.png
    // there would double up the "_tree" and never match the actual asset on disk. A shape
    // variant beyond the first (_variantIndex > 0) adds one more suffix on top, e.g.
    // apple_tree_trunk_v1.png.
    private string TrunkTexturePathFor() => TexturePaths.VariantSuffixed(TexturePaths.InsertBeforeExtension(TexturePathFor(), "_trunk"), _variantIndex);

    private string CanopyTexturePathFor() => TexturePaths.VariantSuffixed(TexturePaths.InsertBeforeExtension(TexturePathFor(), "_canopy"), _variantIndex);

    // Shared, kind-independent asset (art/generate_sprites.py's _generic_tree_branch_layer)
    // - not derived from this kind's own texture path at all, unlike trunk/canopy. Its
    // anchor positions are pre-computed to clear the fruit-tree canopy family's shapes
    // specifically (see KindsWithGenericBranches), so every compatible kind quite
    // literally shares the exact same three files instead of each baking its own
    // near-duplicate copy.
    private const string SharedBranchesBasePath = "res://Content/branches/tree_branches.png";

    private string BranchesTexturePathFor() => TexturePaths.VariantSuffixed(SharedBranchesBasePath, _branchVariantIndex);

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
            var trunkPath = TexturePaths.VariantSuffixed(TexturePaths.InsertBeforeExtension(basePath, "_trunk"), variant);
            var canopyPath = TexturePaths.VariantSuffixed(TexturePaths.InsertBeforeExtension(basePath, "_canopy"), variant);
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
            if (!ResourceLoader.Exists(TexturePaths.VariantSuffixed(SharedBranchesBasePath, variant)))
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
        var exists = ResourceLoader.Exists(TexturePaths.InsertBeforeExtension(basePath, "_trunk"))
            && ResourceLoader.Exists(TexturePaths.InsertBeforeExtension(basePath, "_canopy"));
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
}
