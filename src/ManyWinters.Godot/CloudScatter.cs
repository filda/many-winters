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

    private const string CloudMaskProxyShaderPath = "res://Content/effects/cloud_mask_proxy.gdshader";

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

            // A second, mask-only stand-in - never seen by the main camera (see
            // FreeCameraRig's own CullMask, which excludes CloudLayerBit), only by
            // CloudFogMask's mask camera. See cloud_mask_proxy.gdshader's own doc
            // comment for why this needs to be a *separate* object with its own flag-
            // color material rather than just adding this same visible sprite to that
            // camera's cull mask.
            var proxy = BillboardSprite.Create(texturePath, size, FallbackColor, useMipmaps: false);
            proxy.Position = position;
            proxy.Layers = CloudFogMask.CloudLayerBit;
            proxy.MaterialOverride = new ShaderMaterial
            {
                Shader = ResourceLoader.Load<Shader>(CloudMaskProxyShaderPath),
            };
            ((ShaderMaterial)proxy.MaterialOverride).SetShaderParameter("cloud_texture", TextureCache.Get(texturePath));
            parent.AddChild(proxy);
        }
    }
}
