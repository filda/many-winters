using Godot;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Fog;

// Purely decorative sky clouds, scattered once across the whole terrain at startup. Unlike
// all other scenery these are never gated by fog-of-war: a cloud over unexplored ground is
// still just sky, and they should be visible at every distance from camp.
public static class CloudScatter
{
    // Sparse, independent puffs - not a continuous cover.
    private const int CloudCount = 40;

    private const float MinWorldSize = 25f;
    private const float MaxWorldSize = 70f;

    // Well above every tree and well below the camera's Far (FreeCameraRig, 5000): high enough
    // to read as sky, close enough to keep detail at a normal play zoom.
    private const float MinHeight = 60f;
    private const float MaxHeight = 140f;

    // Fixed for reproducibility, like every other scatter in this codebase.
    private const int Seed = 9;

    // Shared with GroundClouds - one set of cloud art for both the sky and the low cover.
    public static readonly string[] TexturePaths =
    [
        "res://Content/effects/cloud_1.png",
        "res://Content/effects/cloud_2.png",
        "res://Content/effects/cloud_3.png",
    ];

    private static readonly Color FallbackColor = new(0.85f, 0.87f, 0.90f);

    // Deliberately outside [0, 1] - Godot's Color does not clamp at construction. Multiplying the
    // cloud texture's (never fully black) pixels by this pushes all of them, hatch ink included,
    // past fog_of_war_screen.gdshader's cloud-pixel thresholds; the zero green channel is what
    // that shader tests. Modulate on the same Sprite3D pipeline as the real sprite, rather than a
    // custom flat-colour shader, means there is no second "face the camera" implementation whose
    // silhouette could disagree with the real sprite's.
    private static readonly Color MaskFlagModulate = new(12f, 0f, 12f);

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
            CreateCloudWithMaskProxy(parent, texturePath, size, new Vector3(x, y, z));
        }
    }

    // One cloud as the main camera sees it plus its fog-mask stand-in; the same pair serves sky
    // clouds and GroundClouds' low cover, so both are exempted from fog-of-war by one mechanism.
    public static (Sprite3D Sprite, Sprite3D Proxy) CreateCloudWithMaskProxy(Node3D parent, string texturePath, float size, Vector3 position, bool excludeFromOcclusionFade = false)
    {
        // Sky clouds keep the occlusion fade: one between the camera and the view target should
        // dim like a tree canopy would. GroundClouds opts out - see its comment. Mipmaps off,
        // unlike other billboards (see BillboardSprite.Create): sparse clouds never tile a
        // repeated silhouette, so there is no aliasing worth trading the hatching for.
        // VisibleCloudLayerBit, not the default layer - see CloudFogMask.
        var sprite = BillboardSprite.Create(texturePath, size, FallbackColor, excludeFromOcclusionFade: excludeFromOcclusionFade, useMipmaps: false);
        sprite.Position = position;
        sprite.Layers = CloudFogMask.VisibleCloudLayerBit;
        parent.AddChild(sprite);

        // A mask-only stand-in identical in everything that shapes its silhouette (texture, size,
        // position, material, billboard mode), seen only by CloudFogMask's mask camera.
        // OpaquePrepass's alpha-scissor depth write keeps it a real occluder there, and Modulate.A
        // is left alone so a cloud faded by occlusion fade stops registering as cloud in step.
        var proxy = BillboardSprite.Create(texturePath, size, FallbackColor, excludeFromOcclusionFade: excludeFromOcclusionFade, useMipmaps: false);
        proxy.Position = position;
        proxy.Layers = CloudFogMask.CloudLayerBit;
        proxy.Modulate = MaskFlagModulate;
        parent.AddChild(proxy);

        return (sprite, proxy);
    }
}
