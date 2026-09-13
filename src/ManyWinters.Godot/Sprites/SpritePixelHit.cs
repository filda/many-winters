using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Sprites;

// Per-pixel hit testing for a billboarded Sprite3D: is the pixel under the cursor opaque? A
// round canopy touches most edges of its square canvas, so no bounding shape approximates it.
//
// Sprite3D's Billboard is a shader-only effect - the node's Transform never rotates, so the
// CollisionShape3D used for the broad-phase hit does not face the camera. This reconstructs
// where the pick ray crosses the plane the sprite is actually rendered on.
//
// Plane basis: every sprite is FixedY (BillboardSprite), so up is world up and right/forward
// come from the horizontal component of the camera's backward axis, not its pitched basis.
//
// Both plane and ray come from the camera: the shader orients every FixedY billboard by camera
// yaw alone (INV_VIEW_MATRIX, the same for all sprites), and "camera position toward the hit"
// is only a valid ray in perspective - in orthographic (FreeCameraRig.ToggleProjection) rays
// are parallel and start on the near plane.
public static class SpritePixelHit
{
    private static readonly Dictionary<string, Image> _imageCache = new();

    // Below this a pixel counts as see-through. Not zero: the sprites' ink edges are
    // antialiased, so a hair of alpha at a silhouette's outer fringe is visually nothing.
    private const float OpaqueAlphaThreshold = 0.1f;

    // Takes a screen point, the one thing every caller has: HoverArbiter.Revalidate has only
    // the cursor, and SpriteEntityView projects a ray hit once and tests each layer against it.
    //
    // spriteCenterOverride pins the plane to a stable anchor: PersonView's walk bob moves its
    // layers' Position every frame, which otherwise sweeps the sampled pixel across silhouette
    // edges under a still cursor and flickers the hover.
    public static bool IsOpaqueAtScreen(Camera3D camera, Vector2 screenPosition, Sprite3D sprite, string texturePath, Vector3? spriteCenterOverride = null)
    {
        // What the player can see through, they can click through: a canopy ghosted by the
        // occlusion fade is transparent here, so the click falls through (HoverRescue) to what
        // is visibly behind it. Trunks are never faded, so they stay solid to clicks.
        if (BillboardSprite.IsOcclusionFaded(sprite))
        {
            return false;
        }

        if (UvAt(camera, screenPosition, sprite, spriteCenterOverride ?? sprite.GlobalPosition) is not { } uv)
        {
            return false;
        }

        if (!_imageCache.TryGetValue(texturePath, out var image))
        {
            var texture = TextureCache.Get(texturePath);
            image = texture.GetImage();
            _imageCache[texturePath] = image;
        }

        var (pixelX, pixelY) = BillboardUv.PixelAt(uv, image.GetWidth(), image.GetHeight());
        return image.GetPixel(pixelX, pixelY).A > OpaqueAlphaThreshold;
    }

    // Gathers what needs the engine and hands the geometry to BillboardUv. Half-extents come
    // from the global scale, not PixelSize alone: it is fixed at creation and ignores parent
    // and per-axis scaling (ResourceNodeView scales width and height independently).
    private static Vector2? UvAt(Camera3D camera, Vector2 screenPosition, Sprite3D sprite, Vector3 spriteCenter)
    {
        var texture = sprite.Texture;
        var scale = sprite.GlobalTransform.Basis.Scale;
        var size = BillboardUv.RenderedSize(sprite.PixelSize, texture.GetWidth(), texture.GetHeight(), scale.X, scale.Y);

        return BillboardUv.At(
            camera.GlobalTransform.Basis.Z,
            camera.ProjectRayOrigin(screenPosition),
            camera.ProjectRayNormal(screenPosition),
            spriteCenter,
            size.X,
            size.Y,
            sprite.FlipH);
    }
}
