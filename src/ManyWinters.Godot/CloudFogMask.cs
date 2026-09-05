using Godot;

namespace ManyWinters.Godot;

// A real screen-space render, used by FogOfWarRenderer to exempt cloud pixels from
// fog-of-war precisely, without touching depth reconstruction at all.
//
// Two reconstruction-based approaches were tried first and both failed the same way,
// just less obviously each time:
//   1. Testing the reconstructed world.y of each screen pixel against a height floor -
//      broke outright: a grazing view ray toward the horizon (any real play zoom) is so
//      sensitive to the depth buffer's own limited precision that ordinary ground pixels
//      reconstructed a wildly wrong world.y, and the entire open field beyond camp lost
//      its own fog cover, not just the clouds.
//   2. Projecting each cloud's own *known* position forward into screen space (reliable)
//      and comparing it against the pixel's reconstructed position - first in screen-space
//      UV plus a separate view-space depth tolerance, then as one real 3D view-space
//      distance - both still compared against a *reconstructed* pixel position, and the
//      reconstruction itself (not the coordinate space it's expressed in afterward - a
//      rigid rotation into world space doesn't change how wrong a vector already is)
//      carries the same depth-precision error as approach 1. The result was the same
//      symptom, smaller: a circular hole through the ground fog around every cloud,
//      because the "ground" pixel there reconstructed close enough to the cloud's own
//      position to pass whatever tolerance was tried.
//
// A third approach - rendering *only* CloudScatter's own sprites here, nothing else, and
// treating any pixel with real alpha as "a cloud" - fixed both of those but broke in a
// new, narrower way: a cloud actually hidden behind a hill from the main camera's own
// point of view had nothing here to occlude it (there was no terrain in this render at
// all), so it stayed fully "visible" to the mask while genuinely invisible in the color
// buffer - the hill in front of it inherited the cloud's own fog-of-war exemption
// instead, showing as a real patch of unfogged ground breaking through right where a
// distant cloud's low edge met the horizon.
//
// This mask camera's own cull mask now includes ordinary scene geometry too (terrain,
// trees, people - whatever's on the default layer), so a hill really does occlude a
// cloud proxy behind it via normal depth testing, the same as it would in the main
// view. That still leaves the "how do I tell a cloud pixel from a terrain pixel, both
// opaque here" problem the single-layer version didn't have to solve - see
// cloud_mask_proxy.gdshader's own doc comment for that half.
public sealed class CloudFogMask
{
    // Dedicated to CloudScatter's mask-only proxy sprites - invisible to the main
    // camera (FreeCameraRig's own CullMask excludes it), visible only to this class's
    // own mask camera.
    public const uint CloudLayerBit = 1u << 1;

    // The real, visible cloud sprite gets its *own* dedicated bit rather than sharing
    // the default layer everything else (terrain, trees, people) is on - the mask
    // camera's own cull mask below needs the default layer for real occlusion (see this
    // class's own doc comment) but must NOT also render the real cloud sprite sitting
    // at the exact same position as its own proxy: two overlapping, differently-colored
    // surfaces at an identical depth is a coin-flip (z-fighting) over which one a given
    // pixel actually shows, silently corrupting the very flag-color test this whole mask
    // exists to make reliable.
    public const uint VisibleCloudLayerBit = 1u << 2;

    // FogOfWarRenderer's own two full-screen overlay quads need this too, and for a
    // similar reason: they default to the same layer everything ordinary is on, and the
    // mask camera including that layer (for real occlusion, see this class's own doc
    // comment) meant it rendered *those quads too* - each one sampling this very
    // cloud_mask texture while it was still mid-render, painting flat fog color over the
    // proxies underneath (mostly hiding them entirely, since almost the whole map reads
    // as unexplored) instead of leaving them for the main camera's own composite alone.
    public const uint FogOverlayLayerBit = 1u << 3;

    // Full main-viewport resolution, not downscaled - a coarser render (tried first) bled
    // a visible green fringe of *actual unfogged ground* out past each cloud's own true
    // edge once its soft, bilinear-upscaled alpha was thresholded, most visible along a
    // cloud's flatter bottom edge. Only 40 sparse clouds ever draw into this, so a second
    // full-res pass costs nothing that matters next to that.
    private const int MaskResolutionDivisor = 1;

    private readonly Camera3D _mainCamera;
    private readonly Camera3D _maskCamera;
    private readonly SubViewport _maskViewport;

    public CloudFogMask(Node3D parent, Camera3D mainCamera)
    {
        _mainCamera = mainCamera;

        _maskViewport = new SubViewport
        {
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            // A SubViewport gets its own brand-new, empty World3D by default - without
            // this, the mask camera rendered nothing at all (not the proxies, not the
            // terrain added for occlusion, nothing), the same empty transparent-black
            // regardless of where a cloud actually was. Sharing the main camera's own
            // World3D instead means this camera looks at the *same* scene, just through
            // a different lens/cull mask.
            World3D = mainCamera.GetWorld3D(),
        };
        // Layer 1 (the default - terrain, trees, people, everything ordinary) so a real
        // occluder in front of a cloud proxy actually blocks it via normal depth
        // testing, plus CloudLayerBit for the proxies themselves - VisibleCloudLayerBit
        // (the real cloud sprites) is deliberately left out; see this class's own doc
        // comment on why the two must never both render here.
        _maskCamera = new Camera3D { CullMask = 1 | CloudLayerBit, Current = true };
        _maskViewport.AddChild(_maskCamera);
        parent.AddChild(_maskViewport);

        SyncViewportSize();
        SyncCamera();
    }

    public Texture2D Texture => _maskViewport.GetTexture();

    // Called every frame (Main._Process) - the mask camera has to track the main
    // camera's own movement/zoom/projection exactly, or its render won't line up with
    // the main view the fog shaders are compositing over.
    public void Update()
    {
        SyncViewportSize();
        SyncCamera();
    }

    private void SyncViewportSize()
    {
        var mainSize = _mainCamera.GetViewport().GetVisibleRect().Size;
        var maskSize = new Vector2I(
            Mathf.Max(1, (int)mainSize.X / MaskResolutionDivisor),
            Mathf.Max(1, (int)mainSize.Y / MaskResolutionDivisor));
        if (_maskViewport.Size != maskSize)
        {
            _maskViewport.Size = maskSize;
        }
    }

    private void SyncCamera()
    {
        _maskCamera.GlobalTransform = _mainCamera.GlobalTransform;
        _maskCamera.Projection = _mainCamera.Projection;
        _maskCamera.Fov = _mainCamera.Fov;
        _maskCamera.Size = _mainCamera.Size;
        _maskCamera.Near = _mainCamera.Near;
        _maskCamera.Far = _mainCamera.Far;
    }
}
