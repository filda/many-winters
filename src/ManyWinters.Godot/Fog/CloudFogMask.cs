using Godot;

namespace ManyWinters.Godot.Fog;

// A real screen-space render used by FogOfWarRenderer to exempt cloud pixels from
// fog-of-war exactly, without depth reconstruction. Reconstructing a pixel's world position
// from the depth buffer is unreliable along grazing view rays (any real play zoom): a
// world.y height test lost fog over the whole open field, and comparing against each
// cloud's projected position punched a hole through the ground fog around every cloud.
//
// The mask camera renders CloudScatter's mask-only proxies together with ordinary scene
// geometry (default layer), so a hill in front of a cloud occludes its proxy by normal depth
// testing, as it does in the main view - a proxy-only render left the hill unfogged where a
// distant cloud met the horizon. Telling a proxy pixel from a terrain pixel is the flag
// colour's job: CloudScatter.MaskFlagModulate, tested in fog_of_war_screen.gdshader.
public sealed class CloudFogMask
{
    // CloudScatter's mask-only proxies: excluded from the main camera's CullMask
    // (FreeCameraRig), rendered only by the mask camera.
    public const uint CloudLayerBit = 1u << 1;

    // The visible cloud sprite, on its own bit rather than the default layer: the mask camera
    // renders the default layer for occlusion but must not also draw the real sprite at the
    // same position as its proxy - the two would z-fight and corrupt the flag-colour test.
    public const uint VisibleCloudLayerBit = 1u << 2;

    // FogOfWarRenderer's two full-screen quads, on their own bit so the mask camera does not
    // render them: on the default layer they sampled this very cloud_mask mid-render and painted
    // flat fog over the proxies underneath.
    public const uint FogOverlayLayerBit = 1u << 3;

    // Full main-viewport resolution: a downscaled mask, bilinear-upscaled and thresholded, bled
    // a fringe of unfogged ground past each cloud's true edge. The extra full-size pass is cheap.
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
            // A SubViewport gets its own empty World3D by default and would render nothing;
            // share the main camera's so this one sees the same scene through another cull mask.
            World3D = mainCamera.GetWorld3D(),
        };
        // Layer 1 (default - terrain, trees, people) for real occlusion, plus the proxies;
        // VisibleCloudLayerBit is deliberately left out (see the class comment).
        _maskCamera = new Camera3D { CullMask = 1 | CloudLayerBit, Current = true };
        _maskViewport.AddChild(_maskCamera);
        parent.AddChild(_maskViewport);

        SyncViewportSize();
        SyncCamera();
    }

    public Texture2D Texture => _maskViewport.GetTexture();

    // Called every frame (Main._Process): the mask camera must track the main camera's
    // transform and projection exactly, or the mask will not line up with the main view.
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
