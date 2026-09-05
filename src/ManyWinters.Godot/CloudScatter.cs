using Godot;

namespace ManyWinters.Godot;

// Purely decorative sky clouds, scattered once across the whole terrain footprint at
// startup - unlike every other piece of scenery (trees, resources, decorations), these
// are never gated by ExplorationState/fog-of-war: a cloud drifting over unexplored
// ground is still just sky, nothing worth hiding the way a resource node would be, and
// the user explicitly wants them visible at every distance from camp, not just nearby.
public static class CloudScatter
{
    // Sparse, not the dense tiled ceiling the old mesh-based fog-of-war approach used
    // (see FogOfWarRenderer.cs's own history) - these are individual, independent puffs
    // now, not a continuous cover.
    private const int CloudCount = 40;

    private const float MinWorldSize = 25f;
    private const float MaxWorldSize = 70f;

    // Well above every tree (ResourceNodeView's tallest kinds top out well under 20m) and
    // comfortably below the camera's own far clip (FreeCameraRig.cs, 5000) - high enough
    // to always read as sky, close enough to still catch the light/detail at a normal
    // play zoom instead of vanishing into a haze.
    private const float MinHeight = 60f;
    private const float MaxHeight = 140f;

    // Fixed, not time-based, for the same reproducibility reason every other scatter in
    // this codebase (MapLoader's decorations, TerrainRenderer's own preview scatter) uses
    // a seeded RNG rather than System.Random with no seed.
    private const int Seed = 9;

    private static readonly string[] TexturePaths =
    [
        "res://Content/effects/cloud_1.png",
        "res://Content/effects/cloud_2.png",
        "res://Content/effects/cloud_3.png",
    ];

    private static readonly Color FallbackColor = new(0.85f, 0.87f, 0.90f);

    // Deliberately outside the normal [0, 1] color range - Godot's Color doesn't clamp at
    // construction, and multiplying a cloud texture's own (fairly light, never fully
    // black) pixels by this pushes every one of them, dark hatch ink included, past the
    // fog shader's own color-match thresholds (fog_of_war_screen.gdshader's cloud_pixel
    // test) uniformly - not just the texture's already-brightest pixels. A custom
    // shader forcing a flat ALBEDO output (tried first) needed its own hand-rolled
    // billboard math to replace what Sprite3D's *own* auto-generated material otherwise
    // provides for free (see BillboardSprite.Create's own doc comment on Billboard=
    // FixedY) - two different reference conventions for "face the camera" were tried and
    // both left a visible mismatch (a green fringe of real, unfogged ground) between the
    // proxy's own silhouette and the real sprite's. Reusing the exact same Sprite3D
    // pipeline for both - differing only in Modulate and which camera's cull mask
    // includes them - makes that mismatch structurally impossible: there's no second
    // implementation of "face the camera" left to disagree with the first.
    private static readonly Color MaskFlagModulate = new(12f, 0f, 12f, 1f);

    public static void Scatter(Node3D parent, float halfExtentMeters)
    {
        var rng = new RandomNumberGenerator { Seed = Seed };
        for (var i = 0; i < CloudCount; i++)
        {
            var x = rng.RandfRange(-halfExtentMeters, halfExtentMeters);
            var z = rng.RandfRange(-halfExtentMeters, halfExtentMeters);
            var y = rng.RandfRange(MinHeight, MaxHeight);
            var size = rng.RandfRange(MinWorldSize, MaxWorldSize);
            var texturePath = TexturePaths[rng.RandiRange(0, TexturePaths.Length - 1)];
            var position = new Vector3(x, y, z);

            // NOT excluded from occlusion fade (Main.ComputeOccludingSprites) - a cloud
            // low/near enough to sit between the camera and the current view target
            // should dim like anything else in the way, the same as a nearby tree
            // canopy. Mipmaps off, unlike every other billboard - see BillboardSprite.
            // Create's own doc comment on useMipmaps: with only 40 of these, sparse and
            // never filling the screen with a repeated silhouette, there's no real
            // aliasing risk to trade the woodcut hatching/outline away for.
            // VisibleCloudLayerBit, not the default layer - see CloudFogMask's own doc
            // comment on why the real sprite and its mask-only proxy below can never
            // both be visible to the mask camera at once.
            var sprite = BillboardSprite.Create(texturePath, size, FallbackColor, useMipmaps: false);
            sprite.Position = position;
            sprite.Layers = CloudFogMask.VisibleCloudLayerBit;
            parent.AddChild(sprite);

            // A second, mask-only stand-in, identical in every way that affects its own
            // shape/silhouette (same texture, same size, same position, same default
            // material and billboard mode) - never seen by the main camera (see
            // FreeCameraRig's own CullMask, which excludes CloudLayerBit), only by
            // CloudFogMask's mask camera. AlphaCutMode.OpaquePrepass's own alpha-scissor
            // depth-write still keeps it a real occluder there too (see CloudFogMask's
            // own doc comment on why a hill in front of a cloud needs to occlude this
            // proxy the same way it occludes the real sprite) - correctly proportional
            // Alpha too, so a cloud faded down by occlusion fade (its Modulate.A, not
            // touched here) stops registering as "a cloud" here in step with it, instead
            // of fog-of-war staying skipped over its whole silhouette regardless.
            var proxy = BillboardSprite.Create(texturePath, size, FallbackColor, useMipmaps: false);
            proxy.Position = position;
            proxy.Layers = CloudFogMask.CloudLayerBit;
            proxy.Modulate = MaskFlagModulate;
            parent.AddChild(proxy);
        }
    }
}
