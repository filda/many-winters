using Godot;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;
using ManyWinters.Godot.Interaction;

namespace ManyWinters.Godot.Views;

public partial class ResourceNodeView : Area3D, IHoverable
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

    private readonly ResourceNode _node;
    private readonly ResourceKindId _kind;

    // For WorldPresenter, which needs the node back when a view has to return to pending
    // (see WorldPresenter.RefreshExploration).
    public ResourceNode Node => _node;
    private readonly bool _canFell;
    private readonly HoverArbiter _hover;
    private readonly Action<ResourceNode> _onSelected;
    private readonly InputEventEventHandler _onMissedClick;
    private readonly Color _baseColor;
    private int _variantIndex;
    private int _branchVariantIndex;
    private Sprite3D _sprite = null!;
    private string _spriteTexturePath = null!;
    private Color _baseModulate;
    private Sprite3D? _trunk;
    private string? _trunkTexturePath;
    private Color _trunkBaseModulate;
    private Sprite3D? _branches;
    private string? _branchesTexturePath;
    private Color _branchesBaseModulate;
    private Sprite3D? _fruitOverlay;
    private Color _fruitBaseModulate;
    private bool _isHovered;

    // How far the group's memory has taken over from actually seeing this node, and the fade
    // that carries it there - shared with every other view, so all of them dim alike.
    private readonly RememberedFade _remembered = new();

    // Internal for the same reason as PersonView's own constructor - see there.
    internal ResourceNodeView(ResourceNode node, bool canFell, HoverArbiter hover, Action<ResourceNode> onSelected, InputEventEventHandler onMissedClick)
    {
        _node = node;
        _kind = node.Kind;
        _canFell = canFell;
        _hover = hover;
        _onSelected = onSelected;
        _onMissedClick = onMissedClick;

        var visual = LoadVisualDefinition(_kind);
        _baseColor = visual?.Color ?? new Color(0.2f, 0.8f, 0.2f);
        Size = visual is { WorldHeight: > 0f } ? visual.WorldHeight : (canFell ? TreeSize : DefaultSize);
    }

    public float Size { get; }

    public override void _Ready()
    {
        InputRayPickable = true;

        var fallbackColor = EntityVisualVariation.Tint(_baseColor, _node.Id.Seed);
        var widthScale = EntityVisualVariation.RangeFor(_node.Id.Seed, WidthScaleSalt, MinScale, MaxScale);
        var heightScale = EntityVisualVariation.RangeFor(_node.Id.Seed, HeightScaleSalt, MinScale, MaxScale);
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
        var mirrored = EntityVisualVariation.RangeFor(_node.Id.Seed, MirrorSalt, 0f, 1f) < 0.5f;

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
            _variantIndex = variantCount > 1 ? EntityVisualVariation.IndexFor(_node.Id.Seed, TreeVariantSalt, variantCount) : 0;

            _trunkTexturePath = TrunkTexturePathFor();
            _trunk = BillboardSprite.Create(_trunkTexturePath, Size, fallbackColor, excludeFromOcclusionFade: true);
            _trunk.Modulate *= LayerBrightnessVariation(TrunkBrightnessSalt);
            _trunk.FlipH = mirrored;
            _trunkBaseModulate = _trunk.Modulate;
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
                _branchVariantIndex = branchVariantCount > 1 ? EntityVisualVariation.IndexFor(_node.Id.Seed, BranchVariantSalt, branchVariantCount) : 0;

                _branchesTexturePath = BranchesTexturePathFor();
                _branches = BillboardSprite.Create(_branchesTexturePath, Size, fallbackColor, excludeFromOcclusionFade: true);
                _branches.Modulate *= LayerBrightnessVariation(BranchBrightnessSalt);
                _branches.FlipH = mirrored;
                _branchesBaseModulate = _branches.Modulate;
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
            _fruitBaseModulate = _fruitOverlay.Modulate;
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
            extent = SpriteExtents.Combine(extent, SpriteVisibleExtent.Compute(_trunkTexturePath!, Size));
        }

        if (_branches is not null)
        {
            extent = SpriteExtents.Combine(extent, SpriteVisibleExtent.Compute(_branchesTexturePath!, Size));
        }
        // The extent is computed from the unflipped texture - a mirrored sprite's visible
        // content sits the same distance from center but on the opposite side.
        var centerXOffset = mirrored ? -extent.CenterXOffset : extent.CenterXOffset;
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(extent.Width, extent.Height, extent.Width) },
            Position = new Vector3(centerXOffset, extent.CenterYOffset, 0),
        });

        // No MouseExited here: Godot only ever sends that to the one collider its own picking
        // chose, which is exactly what used to leave sprites lit forever (see HoverArbiter).
        // Losing hover is settled once a frame instead, by IsStillUnderCursor below.
        InputEvent += OnInputEvent;

        // A node can be born already dimmed - WorldPresenter creates one the moment its cell
        // becomes explored, which for a world loaded from a save is somewhere the group
        // walked long ago (see SnapRemembered). Frames are processed only while a fade is
        // actually running: there are thousands of these once decorations are resource nodes.
        SetProcess(_remembered.IsFading);
        ApplyTints();
    }

    public override void _ExitTree() => _hover.Forget(this);

    public void ShowHovered(bool hovered)
    {
        if (hovered == _isHovered)
        {
            return;
        }

        _isHovered = hovered;
        var scale = Vector3.One * (hovered ? HoverHighlight.ScaleFactor : 1f);
        _sprite.Scale = scale;

        // The trunk (and branches) highlight together with the canopy - hover is a single
        // "this whole tree is what you're pointing at" signal, unlike occlusion fade where
        // the layers deliberately behave differently.
        if (_trunk is not null)
        {
            _trunk.Scale = scale;
        }

        if (_branches is not null)
        {
            _branches.Scale = scale;
        }

        // The fruit grows with the canopy it hangs on, which it did not before: a hovered
        // tree swelled by a tenth around fruit that stayed exactly where it was.
        if (_fruitOverlay is not null)
        {
            _fruitOverlay.Scale = scale;
        }

        ApplyTints();
    }

    // Fog of war's "remembered" tier (WorldPresenter.RefreshExploration) - explored, but
    // nobody currently has this node in sight. Only aims the fade: the tint itself moves a
    // frame at a time in _Process, so a place passing out of sight dims over about a second
    // instead of switching in one frame. Called once a tick for every live view, so the
    // no-change case has to cost nothing - which is what RememberedFade.Retarget answers.
    public void SetRemembered(bool remembered)
    {
        if (!_remembered.Retarget(remembered))
        {
            return;
        }

        SetProcess(true);
    }

    // Straight to the end state, no fade - for a view created for somewhere the group has
    // already left, where there was never anything on screen to fade out of. Touches no node
    // of its own, so WorldPresenter can call it before this view enters the scene tree.
    public void SnapRemembered(bool remembered) => _remembered.Snap(remembered);

    public override void _Process(double delta)
    {
        var stillFading = _remembered.Advance((float)delta);
        ApplyTints();
        if (!stillFading)
        {
            SetProcess(false);
        }
    }

    // Every layer's displayed colour, always re-derived from its own fixed base modulate (the
    // brightness jitter baked in at _Ready and never touched again) so repeated calls cannot
    // compound a tint. The single place any of this view's layers gets written, so hover, the
    // fog fade and the first paint in _Ready cannot disagree about what the other two did.
    private void ApplyTints()
    {
        SpriteLayerTint.Apply(_sprite, _baseModulate, _remembered, _isHovered);
        if (_trunk is not null)
        {
            SpriteLayerTint.Apply(_trunk, _trunkBaseModulate, _remembered, _isHovered);
        }

        if (_branches is not null)
        {
            SpriteLayerTint.Apply(_branches, _branchesBaseModulate, _remembered, _isHovered);
        }

        // The fruit fades with the tree, which it did not before: a remembered apple tree
        // kept a canopy full of bright fruit hanging over sepia branches.
        if (_fruitOverlay is not null)
        {
            SpriteLayerTint.Apply(_fruitOverlay, _fruitBaseModulate, _remembered, _isHovered);
        }
    }

    private Color LayerBrightnessVariation(int salt)
    {
        var value = EntityVisualVariation.RangeFor(_node.Id.Seed, salt, BrightnessJitterMin, BrightnessJitterMax);
        return new Color(value, value, value);
    }

    // The true silhouette of a split tree is the union of its trunk's and canopy's own
    // visible extents - equivalent to what a single combined image's extent already was,
    // since the two are an exact partition of it (see split_trunk_canopy).
    // Lets HoverRescue ask "is this exact point actually opaque on you", for when some other
    // entity's broad-phase box won the pick instead - see its own doc comment for why that's
    // not just a hypothetical.
    public bool TryHoverAt(Camera3D camera, Vector3 worldPosition)
    {
        var opaque = IsOpaqueOnAnyLayer(camera, worldPosition);
        _hover.Set(this, opaque);
        return opaque;
    }

    // Asked once a frame while this view holds the highlight (HoverArbiter.Revalidate) - the
    // same per-layer test as above, but from wherever the cursor is right now rather than from
    // a picking event, since the everyday ways a highlight got stuck consist of no picking
    // event arriving at all. A cursor over any UI panel counts as off: physics picking never
    // fires under a Control, so the sprite behind one would otherwise stay lit.
    public bool IsStillUnderCursor()
    {
        var viewport = GetViewport();
        return viewport.GuiGetHoveredControl() is null
            && viewport.GetCamera3D() is { } camera
            && IsOpaqueOnAnyLayerAtScreen(camera, viewport.GetMousePosition());
    }

    public bool TryClickAt(Camera3D camera, Vector3 worldPosition)
    {
        if (!IsOpaqueOnAnyLayer(camera, worldPosition))
        {
            return false;
        }

        _onSelected(_node);
        return true;
    }

    // A split tree has no single combined texture any more (see _Ready) - a point counts as
    // opaque if it lands on any layer's own opaque pixels, since together they reconstruct
    // exactly the same silhouette a single combined texture used to represent (branches
    // included - a click on a bare twig tip should still select the tree).
    private bool IsOpaqueOnAnyLayer(Camera3D camera, Vector3 worldPosition) =>
        IsOpaqueOnAnyLayerAtScreen(camera, camera.UnprojectPosition(worldPosition));

    private bool IsOpaqueOnAnyLayerAtScreen(Camera3D camera, Vector2 screenPosition) =>
        (_trunk is not null && SpritePixelHit.IsOpaqueAtScreen(camera, screenPosition, _trunk, _trunkTexturePath!))
        || (_branches is not null && SpritePixelHit.IsOpaqueAtScreen(camera, screenPosition, _branches, _branchesTexturePath!))
        || SpritePixelHit.IsOpaqueAtScreen(camera, screenPosition, _sprite, _spriteTexturePath);

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

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (camera is not Camera3D camera3D)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseMotion:
                // Nothing opaque here and nothing behind it either means the cursor is over
                // bare ground showing through, so whatever was lit has been left behind.
                if (!TryHoverAt(camera3D, position) && !HoverRescue.TryHoverElsewhere(this, camera3D, position))
                {
                    _hover.Clear();
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
