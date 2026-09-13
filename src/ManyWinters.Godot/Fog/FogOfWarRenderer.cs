using Godot;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Fog;

// Renders both non-visible fog-of-war tiers as one full-screen post-process overlay
// (Content/effects/fog_of_war_screen.gdshader has the technique):
//   - "Unknown" cells (never explored) are replaced with a flat fog colour.
//   - "Remembered" cells (explored, nobody has them in sight) get the already-rendered pixel
//     desaturated in place - a flat wash read as "ground turned to sand", not dim memory.
// Both tiers sample one small bitmap (RebuildExplorationTexture) by each screen pixel's own
// reconstructed world position, so the explored camp stays visible whatever the camera's
// distance. The bitmap holds a sharp and a blurred copy of the boundary; see that method.
public sealed class FogOfWarRenderer
{
    // The same muted cool grey the cloud sprites use (art/generate_sprites.py, _cloud), so the
    // unknown sheet and GroundClouds' low cover read as one bank of cloud; warm parchment clashed
    // with the clouds. The shader's mottling and the low cover, not the hue, keep this from
    // reading as flat fog or snow.
    private static readonly Color UnknownColor = new(0.70f, 0.73f, 0.78f);
    private static readonly Color RememberedTint = new(0.80f, 0.74f, 0.64f);

    // One texel per ExplorationState cell (TexelGrid.Covering). A coarser texel straddled two
    // cells, so the shader fogged part of an already-instantiated tree's canopy (WorldPresenter
    // only creates a ResourceNodeView once its own cell is Explored).
    //
    // The blur is applied to a separate copy and gated by the sharp mask in the shader
    // (`unexploredSharp * unexploredBlurred`): a genuinely Explored position multiplies its blur
    // contribution by zero, so softness only ever shows on the unexplored side. Blurring the
    // boundary itself bled a visible ghost of fog onto Explored trees.
    private const int BlurRadiusTexels = 3;

    private const string UnknownShaderPath = "res://Content/effects/fog_of_war_screen.gdshader";
    private const string RememberedShaderPath = "res://Content/effects/fog_of_war_remembered.gdshader";

    // The shader's vertex() writes straight to clip space and ignores the quad's real size; this
    // only has to cover the [-1, 1] clip range (2x2), never less.
    private const float OverlayQuadSize = 4f;

    // One below Godot's maximum: the sheets must draw over every piece of world content (see the
    // constructor - the quad sits at the near plane, so distance sorting alone would not settle
    // it), but under the hover rim, which HoverOutline draws at 127 - a remembered tree is still
    // a valid thing to point at.
    private const int OverlayRenderPriority = 126;

    private readonly RevealableExploration _exploration;
    private readonly TexelGrid _grid;
    private readonly ImageTexture _explorationTexture;

    // Metres from each texel to the nearest ever-explored cell (GridDistanceField), so the unknown
    // shader fades out with distance from where the band has actually been. Its own Rf texture
    // because _explorationTexture's four Rgba8 channels are all taken.
    private readonly ImageTexture _distanceTexture;

    // The same field on the CPU side, from the last rebuild, for GroundClouds to query.
    private float[,] _distanceCells;

    public FogOfWarRenderer(RevealableExploration exploration, float halfExtentMeters, Camera3D camera, CloudFogMask cloudFogMask)
    {
        _exploration = exploration;
        _grid = TexelGrid.Covering(halfExtentMeters, ExplorationState.CellSizeMeters);
        _distanceCells = new float[_grid.Size, _grid.Size];

        var initialImage = Image.CreateEmpty(_grid.Size, _grid.Size, false, Image.Format.Rgba8);
        _explorationTexture = ImageTexture.CreateFromImage(initialImage);
        var initialDistance = Image.CreateEmpty(_grid.Size, _grid.Size, false, Image.Format.Rf);
        _distanceTexture = ImageTexture.CreateFromImage(initialDistance);

        // A real render of just the cloud sprites (see CloudFogMask): "is this pixel a cloud" is a
        // direct lookup, never inferred from the depth-reconstructed position, which is unreliable
        // along the grazing view rays any play zoom produces.
        var cloudMaskTexture = cloudFogMask.Texture;

        // An alpha blend mode puts a material in the transparent pass, distance-sorted against the
        // person/resource sprites (OpaquePrepass still counts), which then drew on top of these
        // overlays. RenderPriority bypasses that sort: higher draws later regardless of depth. The
        // two overlays never compete for a pixel - a cell is never both unexplored and remembered.
        var unknownMaterial = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(UnknownShaderPath), RenderPriority = OverlayRenderPriority };
        unknownMaterial.SetShaderParameter("exploration_texture", _explorationTexture);
        unknownMaterial.SetShaderParameter("fog_albedo", UnknownColor);
        unknownMaterial.SetShaderParameter("distance_texture", _distanceTexture);
        // The sheet dissolves into the skyline instead of stopping at a seam, so it tracks the
        // painted sky minus most of its blue (SkyPalette.FogFar - a sheet as blue as the air stops
        // reading as ground).
        unknownMaterial.SetShaderParameter("far_color", SkyPalette.FogFar);
        unknownMaterial.SetShaderParameter("half_extent_meters", halfExtentMeters);
        unknownMaterial.SetShaderParameter("cloud_mask", cloudMaskTexture);

        var rememberedMaterial = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(RememberedShaderPath), RenderPriority = OverlayRenderPriority };
        rememberedMaterial.SetShaderParameter("exploration_texture", _explorationTexture);
        rememberedMaterial.SetShaderParameter("remembered_tint", RememberedTint);
        rememberedMaterial.SetShaderParameter("half_extent_meters", halfExtentMeters);
        rememberedMaterial.SetShaderParameter("cloud_mask", cloudMaskTexture);

        // Parented to the camera, just in front of it: the vertex shaders ignore this transform
        // (always full-screen), but frustum culling runs on the mesh's real bounding box before the
        // vertex override, so it needs a transform inside the frustum - beyond Near (FreeCameraRig,
        // 0.5) or it is culled as behind the near plane.
        const float overlayLocalZ = -1f;
        var quadMesh = new QuadMesh { Size = new Vector2(OverlayQuadSize, OverlayQuadSize) };
        // Not the default layer: the mask camera must not render these quads (see
        // CloudFogMask.FogOverlayLayerBit). The main camera's cull mask includes this layer.
        camera.AddChild(new MeshInstance3D
        {
            Mesh = quadMesh,
            MaterialOverride = unknownMaterial,
            Position = new Vector3(0f, 0f, overlayLocalZ),
            Layers = CloudFogMask.FogOverlayLayerBit,
        });
        camera.AddChild(new MeshInstance3D
        {
            Mesh = quadMesh,
            MaterialOverride = rememberedMaterial,
            Position = new Vector3(0f, 0f, overlayLocalZ),
            Layers = CloudFogMask.FogOverlayLayerBit,
        });

        RebuildExplorationTexture();
    }

    public void Refresh() => RebuildExplorationTexture();

    // Metres from a world position to the nearest ever-explored cell, as of the last Refresh.
    public float DistanceToExploredMeters(float worldX, float worldZ) =>
        _distanceCells[_grid.TexelAt(worldZ), _grid.TexelAt(worldX)] * _grid.MetresPerTexel;

    // One texel per (worldX, worldZ) sample, two layers:
    //   R/G: sharp masks - R: 1 where the cell was never explored, G: 1 where it is explored but
    //   out of everyone's sight. The shaders re-threshold these to hard 0/1 (step(0.5, ...))
    //   despite bilinear sampling, so they can gate the blurred channels without bleed.
    //   B/A: the same two masks blurred (BoxBlur) - the soft falloff, confined to the unexplored
    //   or not-visible side by the sharp*blurred product in the shader.
    private void RebuildExplorationTexture()
    {
        var size = _grid.Size;
        var masks = ExplorationMasks.Build(_exploration, _grid);
        var unexploredSharp = masks.Unexplored;
        var rememberedSharp = masks.Remembered;
        var unexploredBlurred = BoxBlur.Blur(unexploredSharp, BlurRadiusTexels);
        var rememberedBlurred = BoxBlur.Blur(rememberedSharp, BlurRadiusTexels);

        _distanceCells = GridDistanceField.DistanceToNearestTrue(masks.Explored);

        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var distanceImage = Image.CreateEmpty(size, size, false, Image.Format.Rf);
        for (var ty = 0; ty < size; ty++)
        {
            for (var tx = 0; tx < size; tx++)
            {
                image.SetPixel(tx, ty, new Color(unexploredSharp[ty, tx], rememberedSharp[ty, tx], unexploredBlurred[ty, tx], rememberedBlurred[ty, tx]));
                distanceImage.SetPixel(tx, ty, new Color(_distanceCells[ty, tx] * _grid.MetresPerTexel, 0f, 0f));
            }
        }

        _explorationTexture.Update(image);
        _distanceTexture.Update(distanceImage);
    }
}
