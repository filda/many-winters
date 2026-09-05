using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

// Renders both non-"currently visible" fog-of-war tiers (todo #13) as a single full-screen
// post-process overlay (Content/effects/fog_of_war_screen.gdshader - see its own doc comment
// for the technique, and this class's git history for two earlier approaches - a polygon
// mesh ceiling and real volumetric fog - that were tried and reverted):
//   - "Unknown" cells (never explored) get replaced with a flat fog color.
//   - "Remembered" cells (explored, but nobody currently has them in sight) get the actual
//     already-rendered pixel desaturated/dimmed in place, not a flat color blended over it -
//     an earlier mesh-based version of this tier used a flat sepia wash, which read as "the
//     ground turned to sand" rather than "this same ground, dimly remembered".
// Both tiers are driven by one small bitmap (RebuildExplorationTexture) that the shader
// samples using each screen pixel's own reconstructed world position - the per-pixel test
// (not per-vertex, not per-ray-step) is what keeps the explored camp visible regardless of
// the camera's own distance from it. That bitmap carries both an exact ("sharp") record of
// the boundary and a blurred copy of it - see RebuildExplorationTexture's own doc comment for
// why a boundary that's soft everywhere ended up visibly fogging trees whose cell was
// actually already Explored.
public sealed class FogOfWarRenderer
{
    private static readonly Color UnknownColor = new(0.6f, 0.62f, 0.66f);
    private static readonly Color RememberedTint = new(0.72f, 0.72f, 0.76f);

    // Resolution of the *sharp* channels (R/B below) - one texel per ExplorationState cell
    // (computed in the constructor from halfExtentMeters and CellSizeMeters), not some
    // independent fixed value. A fixed 200 (Half=500 -> 5m/texel) used to be coarser than
    // CellSizeMeters (2.5m): one texel then covered *two* actual cells, so
    // RebuildExplorationTexture's single sample at that texel's center could land on the
    // explored side of the true cell boundary while a tree just past it, in the same texel but
    // the still-unexplored cell, was already real, already-instantiated geometry (WorldPresenter
    // only creates a ResourceNodeView once its own cell is Explored) - the shader then fogged
    // part of that already-Explored tree's own canopy, screen pixels of the same object
    // reconstructing to world positions on both sides of one oversized texel. One texel per
    // cell removes that mismatch entirely: every texel's sampled cell is the same cell any
    // instantiated object in it belongs to, so there is no boundary for a single object's own
    // geometry to straddle.
    private readonly int _explorationTextureResolution;

    // Widening the sharp boundary itself (lowering ExplorationTextureResolution) was tried
    // first to fix canopies getting sliced by a too-narrow transition - it did, but a soft
    // blur has no notion of "which side of the true boundary this position is actually on":
    // it also bled a visible ghost of fog onto trees whose cell genuinely *is* already
    // Explored (confirmed - see "ale to nemá bejt uřízlý vůbec" and the two-trees screenshot:
    // the farther one still showing a partial silhouette meant it was already real, already-
    // instantiated geometry, since an unexplored cell has no ResourceNodeView to show at all).
    // Blurring a *separate* copy instead, then gating it by the exact sharp test
    // (`unexploredSharp * unexploredBlurred` in the shader), keeps that impossible: a
    // genuinely Explored position always multiplies its own blur contribution by zero, no
    // matter how much nearby unexplored fog bleeds toward it. The softness only ever shows on
    // the unexplored side, fading deeper the further past the true edge it goes.
    private const int BlurRadiusTexels = 3;

    private const string UnknownShaderPath = "res://Content/effects/fog_of_war_screen.gdshader";
    private const string RememberedShaderPath = "res://Content/effects/fog_of_war_remembered.gdshader";

    // Local-space size of the overlay quad - the shader's own vertex() override writes
    // straight to clip space ignoring the quad's actual world transform/size, so this only
    // has to safely cover the [-1, 1] clip-space range on both axes (2x2), never less.
    private const float OverlayQuadSize = 4f;

    private readonly ExplorationState _exploration;
    private readonly float _halfExtentMeters;
    private readonly ImageTexture _explorationTexture;

    public FogOfWarRenderer(ExplorationState exploration, float halfExtentMeters, Camera3D camera, CloudFogMask cloudFogMask)
    {
        _exploration = exploration;
        _halfExtentMeters = halfExtentMeters;
        _explorationTextureResolution = (int)MathF.Ceiling((2f * halfExtentMeters) / ExplorationState.CellSizeMeters);

        var initialImage = Image.CreateEmpty(_explorationTextureResolution, _explorationTextureResolution, false, Image.Format.Rgba8);
        _explorationTexture = ImageTexture.CreateFromImage(initialImage);

        // Two reconstruction-based ways to exempt CloudScatter's sprites from fog-of-war
        // were tried and rejected first - see CloudFogMask's own doc comment for the full
        // history (a world.y height test, then two variants of projecting each cloud's
        // own known position into screen/view space and comparing against the pixel's
        // reconstructed one). Both failed for the same underlying reason: reconstructing
        // *this pixel's own* position from the depth buffer is only reliable along a
        // steep view ray, and any real play zoom routinely looks close enough to the
        // horizon to break that badly. cloudFogMask sidesteps the whole problem - it's a
        // real render of just the cloud sprites, so "is this pixel a cloud" is a direct
        // lookup, never an inference from an unreliable reconstructed position.
        var cloudMaskTexture = cloudFogMask.Texture;

        // Using ALPHA/a blend mode at all puts a material in the transparent render pass,
        // sorted by distance among everything else transparent there - including
        // person/resource sprites (their own AlphaCutMode.OpaquePrepass still counts), which
        // that sort otherwise put on top of these overlays despite them sitting closer to the
        // camera than anything else in the scene. RenderPriority sidesteps distance sorting
        // entirely: within the transparent pass, a higher value always draws later (on top),
        // regardless of depth - Godot's actual max (127) guarantees both overlays are among
        // the last things composited, every frame (order between the two doesn't matter - see
        // their own shaders: a cell is never both unexplored and remembered at once, so they
        // never compete over the same pixel).
        var unknownMaterial = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(UnknownShaderPath), RenderPriority = 127 };
        unknownMaterial.SetShaderParameter("exploration_texture", _explorationTexture);
        unknownMaterial.SetShaderParameter("fog_albedo", UnknownColor);
        unknownMaterial.SetShaderParameter("half_extent_meters", halfExtentMeters);
        unknownMaterial.SetShaderParameter("cloud_mask", cloudMaskTexture);

        var rememberedMaterial = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(RememberedShaderPath), RenderPriority = 127 };
        rememberedMaterial.SetShaderParameter("exploration_texture", _explorationTexture);
        rememberedMaterial.SetShaderParameter("remembered_tint", RememberedTint);
        rememberedMaterial.SetShaderParameter("half_extent_meters", halfExtentMeters);
        rememberedMaterial.SetShaderParameter("cloud_mask", cloudMaskTexture);

        // Both parented directly to the camera, just in front of it - their vertex shaders
        // ignore this transform for where they actually draw (always full-screen, see
        // fog_of_war_screen.gdshader's own doc comment), but each still needs *a* transform
        // close to the camera so Godot's ordinary frustum culling (evaluated before the
        // vertex override runs, on the mesh's real bounding box) doesn't cull it out as "off
        // in the distance". Must sit beyond the camera's own Near (FreeCameraRig.cs, 0.5) or
        // that same culling would discard it as "behind the near plane" instead.
        const float overlayLocalZ = -1f;
        var quadMesh = new QuadMesh { Size = new Vector2(OverlayQuadSize, OverlayQuadSize) };
        // Layers = CloudFogMask.FogOverlayLayerBit, not the default - see that constant's
        // own doc comment on why these two quads must be invisible to the mask camera
        // specifically (main camera's own cull mask has no exclusions besides
        // CloudFogMask.CloudLayerBit, so this doesn't affect the real, composited view).
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

    // One texel per (worldX, worldZ) sample across the whole map, in two layers:
    //   R/G: the *sharp* (exact, unblurred) state - R: 1 where that point's cell has never
    //   been explored, else 0. G: 1 where it's explored but not in anyone's current sight
    //   (the "remembered" tier), else 0. Both shaders re-threshold these back to a hard 0/1
    //   (step(0.5, ...)) even though bilinear sampling blends them a little right at the
    //   boundary - that's what lets them gate the blurred channels below without ever letting
    //   blur bleed across the true edge.
    //   B/A: the same two masks, *blurred* (BoxBlur) - these are what actually carry the
    //   soft falloff a fog boundary should have. Multiplying sharp*blurred in the shader is
    //   what confines that softness to the unexplored/not-visible side only.
    private void RebuildExplorationTexture()
    {
        var size = _explorationTextureResolution;
        var unexploredSharp = new float[size, size];
        var rememberedSharp = new float[size, size];

        for (var ty = 0; ty < size; ty++)
        {
            var worldZ = (((ty + 0.5f) / size) - 0.5f) * 2f * _halfExtentMeters;
            for (var tx = 0; tx < size; tx++)
            {
                var worldX = (((tx + 0.5f) / size) - 0.5f) * 2f * _halfExtentMeters;
                var cell = ExplorationState.CellFor(new Position(worldX, worldZ));
                var explored = _exploration.IsExplored(cell);
                unexploredSharp[ty, tx] = explored ? 0f : 1f;
                rememberedSharp[ty, tx] = explored && !_exploration.IsVisible(cell) ? 1f : 0f;
            }
        }

        var unexploredBlurred = BoxBlur(unexploredSharp, size);
        var rememberedBlurred = BoxBlur(rememberedSharp, size);

        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (var ty = 0; ty < size; ty++)
        {
            for (var tx = 0; tx < size; tx++)
            {
                image.SetPixel(tx, ty, new Color(unexploredSharp[ty, tx], rememberedSharp[ty, tx], unexploredBlurred[ty, tx], rememberedBlurred[ty, tx]));
            }
        }

        _explorationTexture.Update(image);
    }

    // Separable box blur (horizontal pass, then vertical) - simple and, at
    // ExplorationTextureResolution^2 texels and a small fixed radius, cheap enough to redo
    // every tick alongside the rest of this method. Samples past the texture's own edge clamp
    // to the nearest real one rather than wrapping, matching the sampler's own repeat_disable.
    private static float[,] BoxBlur(float[,] source, int size)
    {
        var horizontal = new float[size, size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var sum = 0f;
                for (var dx = -BlurRadiusTexels; dx <= BlurRadiusTexels; dx++)
                {
                    var sx = Math.Clamp(x + dx, 0, size - 1);
                    sum += source[y, sx];
                }

                horizontal[y, x] = sum / ((2 * BlurRadiusTexels) + 1);
            }
        }

        var result = new float[size, size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var sum = 0f;
                for (var dy = -BlurRadiusTexels; dy <= BlurRadiusTexels; dy++)
                {
                    var sy = Math.Clamp(y + dy, 0, size - 1);
                    sum += horizontal[sy, x];
                }

                result[y, x] = sum / ((2 * BlurRadiusTexels) + 1);
            }
        }

        return result;
    }
}
