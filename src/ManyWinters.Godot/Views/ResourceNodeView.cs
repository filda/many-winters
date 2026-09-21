using Godot;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// A resource - a berry bush, a mushroom, a standing tree - drawn out of up to four layers, so
// the occlusion fade can ghost a canopy while the trunk stays solid and a tree can bear fruit
// without a second whole texture. Being a sprite in the world (hover, clicks, fog fade, shadow,
// collision shape) is SpriteEntityView's; here is which layers this kind has and what its seed
// varies. The scattered forest is thousands of these, one node each, and stays that way: every
// tree and bush is a clickable entity (MapLoader.ScatterDecorations), so instancing them into a
// MultiMesh would erase exactly what makes them resources.
internal partial class ResourceNodeView : SpriteEntityView
{
    // Ordinary resources read as a small icon on the ground. A fellable one is a tree standing
    // in the world, a touch shorter than the wild conifer/deciduous kinds (7-8 m in their .tres);
    // much smaller and it reads as a sapling next to the forest.
    private const float DefaultSize = 0.6f;
    private const float TreeSize = 6f;
    private const float MinScale = 0.85f;
    private const float MaxScale = 1.15f;
    private const float ShadowDiameterRatio = 0.7f / DefaultSize;

    // A fraction in front of the canopy (BillboardSprite.Create's renderPriority): two
    // billboards at the same position and depth have no defined draw order and would z-fight.
    private const int FruitOverlayRenderPriority = 1;

    // Distinct salts for EntityVisualVariation.RangeFor, so trunk and canopy brighten and dim
    // independently instead of as one uniform tint.
    private const int TrunkBrightnessSalt = 401;
    private const int CanopyBrightnessSalt = 402;
    private const float BrightnessJitterMin = 0.85f;
    private const float BrightnessJitterMax = 1.1f;

    // Separate width and height draws let a tree be tall-and-narrow or short-and-wide rather
    // than the same shape at a different zoom.
    private const int WidthScaleSalt = 403;
    private const int HeightScaleSalt = 404;
    private const int MirrorSalt = 405;

    // Which hand-authored trunk/canopy shape variant (art/generate_sprites.py, e.g.
    // apple_tree_trunk_v1.png) this node drew; variant 0 is the unsuffixed original. Probing
    // stops at the first missing variant, so MaxTreeVariantProbe is an upper bound, not a promise.
    private const int TreeVariantSalt = 406;
    private const int MaxTreeVariantProbe = 8;

    // Branches are their own variant axis (own salt, own probe), so branch and trunk/canopy
    // choices multiply into new combinations instead of always matching.
    private const int BranchVariantSalt = 407;
    private const int BranchBrightnessSalt = 408;

    // For a kind with no .tres visual definition - a placeholder green, only seen if its art is
    // missing too.
    private static readonly Color DefaultColor = new(0.2f, 0.8f, 0.2f);

    private readonly Entity _node;
    private readonly EntityKindId _kind;

    // For WorldPresenter, which sends a view back to pending when its cell is un-revealed
    // (WorldPresenter.RefreshResourceNodeExploration).
    public Entity Node => _node;
    private readonly Action<Entity, MouseButton> _onClicked;
    private readonly Color _baseColor;
    private int _variantIndex;
    private int _branchVariantIndex;

    // The only layer kept after building: whether the tree bears fruit changes while it stands
    // (SetHasFruit). Everything else about the layers is SpriteEntityView's job.
    private SpriteLayer? _fruit;

    // Internal for the same reason as PersonView's constructor.
    internal ResourceNodeView(Entity node, bool canFell, HoverArbiter hover, Action<Entity, MouseButton> onClicked, InputEventEventHandler onMissedClick)
        : base(NominalHeightFor(node.Kind, canFell), hover, onMissedClick)
    {
        _node = node;
        _kind = node.Kind;
        _onClicked = onClicked;
        _baseColor = LoadVisualDefinition(node.Kind)?.Color ?? DefaultColor;
    }

    // What WorldPresenter places this node by (origin half a height above the ground) and the
    // height every layer is created at.
    public float Size => NominalHeight;

    // A kind's authored height where it has one, else a standing tree's or a ground icon's
    // default. Static because the base class needs it before this view has fields.
    private static float NominalHeightFor(EntityKindId kind, bool canFell)
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

        // A coin flip shared by every layer (trunk, branches, canopy, fruit): flipping them
        // independently would misalign a silhouette authored and split as one asymmetric shape.
        var mirrored = EntityVisualVariation.RangeFor(_node.Id.Seed, MirrorSalt, 0f, 1f) < 0.5f;

        SetUpGroundShadow(Size * ShadowDiameterRatio);

        // A kind with split art (art/generate_sprites.py's split_trunk_canopy) renders trunk and
        // canopy as separate layers, so the occlusion fade can ghost the canopy while the trunk
        // never fades. A kind without gets one combined sprite.
        //
        // Registered trunk first, then branches, then canopy: all three share a render priority
        // and roughly a depth, so tree order settles which draws over which.
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

            // Bare twig tips poking past the canopy near the trunk (art/generate_sprites.py's
            // _generic_tree_branch_layer) - wood, so it never fades either.
            if (HasBranchLayer(_kind))
            {
                var branchVariantCount = BranchVariantCount(_kind);
                _branchVariantIndex = branchVariantCount > 1 ? EntityVisualVariation.IndexFor(_node.Id.Seed, BranchVariantSalt, branchVariantCount) : 0;

                var branchesTexturePath = BranchesTexturePathFor();
                var branches = BillboardSprite.Create(branchesTexturePath, Size, fallbackColor, excludeFromOcclusionFade: true);
                branches.Modulate *= LayerBrightnessVariation(BranchBrightnessSalt);
                branches.FlipH = mirrored;
                // Pickable but not traced: a click on a bare twig selects the tree, while a rim
                // around twigs this fine reads as a squiggle beside the canopy.
                Register(branches, branchesTexturePath, outlines: false);
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

        // The tree never changes, but whether it bears fruit does (RemainingAmount, pushed from
        // Main each tick); an overlay avoids a whole "bare tree" texture per kind. Driven by
        // which art exists, not by CanFell: a fellable conifer/deciduous/bush has one plain
        // sprite. Not a picking layer - the fruit lies inside the canopy's silhouette.
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
            _fruit = Register(fruit, fruitTexturePath, picks: false, outlines: false);
        }
    }

    // Both buttons, as a person answers to both: left gathers from it, right asks what else may
    // be done with it (see Main, ContextMenu).
    protected override bool WantsClick(MouseButton button) => true;

    protected override bool OnClicked(MouseButton button)
    {
        _onClicked(_node, button);
        return true;
    }

    private Color LayerBrightnessVariation(int salt)
    {
        var value = EntityVisualVariation.RangeFor(_node.Id.Seed, salt, BrightnessJitterMin, BrightnessJitterMax);
        return new Color(value, value, value);
    }

    // No-op for a kind without fruit art (_fruit stays null).
    public void SetHasFruit(bool hasFruit)
    {
        if (_fruit is not null)
        {
            _fruit.Sprite.Visible = hasFruit;
        }
    }

    // A kind with a dedicated standing-tree sprite ({kind}_tree.png alongside {kind}.png: apple,
    // pear...) draws that instead of the icon. Driven by which file exists, not by CanFell - a
    // fellable conifer/deciduous/bush only has the plain {kind}.png.
    private string TexturePathFor() => BaseTexturePathFor(_kind);

    private static string BaseTexturePathFor(EntityKindId kind) => HasTreeSprite(kind)
        ? $"res://Content/resources/{kind.Value}/{kind.Value}_tree.png"
        : $"res://Content/resources/{kind.Value}/{kind.Value}.png";

    // Fruit spots are authored per canopy variant (art/generate_sprites.py's
    // _APPLE_FRUIT_SPOT_VARIANTS/_PEAR_FRUIT_SPOT_VARIANTS), so they land inside the canopy
    // shape this node actually drew.
    private string FruitOverlayTexturePath() => TexturePaths.VariantSuffixed($"res://Content/resources/{_kind.Value}/{_kind.Value}_tree_fruit.png", _variantIndex);

    // Split filenames sit alongside whatever BaseTexturePathFor uses as the whole tree:
    // {kind}_tree_trunk.png for apple/pear, {kind}_trunk.png for conifer_tree/deciduous_tree
    // (whose id already ends in "_tree"). A variant beyond the first adds a _vN suffix on top.
    private string TrunkTexturePathFor() => TexturePaths.VariantSuffixed(TexturePaths.InsertBeforeExtension(TexturePathFor(), "_trunk"), _variantIndex);

    private string CanopyTexturePathFor() => TexturePaths.VariantSuffixed(TexturePaths.InsertBeforeExtension(TexturePathFor(), "_canopy"), _variantIndex);

    // Shared, kind-independent asset (art/generate_sprites.py's _generic_tree_branch_layer),
    // not derived from this kind's texture path. Its anchor positions are pre-computed to clear
    // the fruit-tree canopy family's shapes (see KindsWithGenericBranches).
    private const string SharedBranchesBasePath = "res://Content/branches/tree_branches.png";

    private string BranchesTexturePathFor() => TexturePaths.VariantSuffixed(SharedBranchesBasePath, _branchVariantIndex);

    // How many trunk/canopy shape variants this kind has on disk: 1 for the unsuffixed
    // original, then probing _v1, _v2, ... until one is missing. Cached per kind like the
    // other lookups.
    private static readonly Dictionary<EntityKindId, int> TreeVariantCountCache = new();

    private static int TreeVariantCount(EntityKindId kind)
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

    // Only kinds sharing the fruit-tree canopy formula (art/generate_sprites.py's
    // _FRUIT_TREE_CANOPY_VARIANTS), whose shapes the shared branch anchors were computed to
    // clear. conifer_tree's tiered canopy (random_conifer_tiers) was never checked, so it stays
    // out rather than risk a branch drawn across its leaves.
    private static readonly HashSet<string> KindsWithGenericBranches = new() { "apple", "pear", "deciduous_tree" };

    private static readonly Dictionary<EntityKindId, bool> HasBranchLayerCache = new();
    private static readonly Dictionary<EntityKindId, int> BranchVariantCountCache = new();

    private static bool HasBranchLayer(EntityKindId kind)
    {
        if (HasBranchLayerCache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var exists = KindsWithGenericBranches.Contains(kind.Value) && ResourceLoader.Exists(SharedBranchesBasePath);
        HasBranchLayerCache[kind] = exists;
        return exists;
    }

    // Same probing as TreeVariantCount for the shared branch layer's own variants - identical
    // for every kind, but cached per kind since callers key everything off EntityKindId.
    private static int BranchVariantCount(EntityKindId kind)
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

    // Cached per kind (see VisualDefinitionCache below for why per-node ResourceLoader calls at
    // decoration scale are avoided).
    private static readonly Dictionary<EntityKindId, bool> HasTreeSpriteCache = new();
    private static readonly Dictionary<EntityKindId, bool> HasFruitOverlayCache = new();
    private static readonly Dictionary<EntityKindId, bool> HasTrunkCanopySplitCache = new();

    private static bool HasTreeSprite(EntityKindId kind)
    {
        if (HasTreeSpriteCache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var exists = ResourceLoader.Exists($"res://Content/resources/{kind.Value}/{kind.Value}_tree.png");
        HasTreeSpriteCache[kind] = exists;
        return exists;
    }

    private static bool HasFruitOverlay(EntityKindId kind)
    {
        if (HasFruitOverlayCache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var exists = ResourceLoader.Exists($"res://Content/resources/{kind.Value}/{kind.Value}_tree_fruit.png");
        HasFruitOverlayCache[kind] = exists;
        return exists;
    }

    private static bool HasTrunkCanopySplit(EntityKindId kind)
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

    // Cached per kind, not loaded per node: thousands of nodes of a handful of kinds
    // (MapLoader.ScatterDecorations) calling ResourceLoader.Load on the same .tres in one frame
    // reliably crashed Godot's C# bridge (a GCHandle race in
    // ScriptManagerBridge.SwapGCHandleForType, "Handle is not initialized").
    private static readonly Dictionary<EntityKindId, ResourceVisualDefinition?> VisualDefinitionCache = new();

    private static ResourceVisualDefinition? LoadVisualDefinition(EntityKindId kind)
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
