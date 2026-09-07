using Godot;

namespace ManyWinters.Godot;

// True per-pixel hit testing for a billboarded Sprite3D: is the actual pixel under the
// cursor opaque, not just "somewhere inside a bounding shape"? A round tree canopy touches
// most of its square canvas's edges, so no bounding box or capsule can approximate it - the
// corners beside a round canopy sit inside any such shape while still being visibly empty.
//
// Sprite3D's Billboard rendering is a shader-only trick - it never rotates the node's own
// Transform, so the invisible CollisionShape3D used for the initial broad-phase hit doesn't
// itself turn to face the camera the way the visible sprite does. This instead reconstructs
// where the pick ray crosses the sprite's actual rendered plane.
//
// Every sprite uses FixedY billboarding (see BillboardSprite.cs), not full/spherical - it
// only ever yaws to face the camera's *horizontal* direction, keeping local up pinned to
// world up regardless of camera pitch (that's what keeps a sprite's own base sitting at its
// real world-space height instead of floating - see BillboardSprite's own doc comment). The
// rendered plane's basis has to match that: up is always world up, and right/forward are
// derived from the horizontal component of the camera's own backward axis, not its full
// (pitched) basis vectors - those would describe a full/spherical billboard's plane instead,
// which tilts to match camera elevation and this game's sprites never do.
//
// Both the plane and the ray are taken from the camera, never reconstructed from the camera's
// position and the sprite's: Godot's shader orients every FixedY billboard by the camera's
// yaw alone (BaseMaterial3D's BILLBOARD_FIXED_Y builds the plane from INV_VIEW_MATRIX, which
// is the same for every sprite on screen), and a pick ray is only "camera position toward the
// hit" for a perspective camera - in orthographic (FreeCameraRig.ToggleProjection) every ray
// is parallel to the view axis and starts on the near plane, so that reconstruction sampled
// the wrong pixel for anything off the screen's center.
public static class SpritePixelHit
{
    private static readonly Dictionary<string, Image> _imageCache = new();

    // spriteCenterOverride lets a caller pin the test plane to a stable anchor instead of the
    // sprite's own GlobalPosition - PersonView's walk animation nudges the sprite's local
    // Position by a few centimeters every frame (the bob), which otherwise sweeps the sampled
    // pixel back and forth across silhouette edges under an unmoving cursor and reads as
    // hover flickering on and off. No other view animates its sprite's position like this.
    public static bool IsOpaqueAt(Camera3D camera, Vector3 rayHitPosition, Sprite3D sprite, string texturePath, Vector3? spriteCenterOverride = null)
    {
        // What the player can see through, they can click and hover through: a canopy
        // ghosted by Main's occlusion fade counts as fully transparent here no matter what
        // its texture says, so the click falls through (via HoverRescue) to whatever is
        // visibly behind it - a mushroom, another person, or the ground. A tree's trunk is
        // never faded (BillboardSprite.IsExcludedFromOcclusionFade), so it stays as solid to
        // clicks as it looks.
        if (BillboardSprite.IsOcclusionFaded(sprite))
        {
            return false;
        }

        if (UvAt(camera, rayHitPosition, sprite, spriteCenterOverride ?? sprite.GlobalPosition) is not { } uv)
        {
            return false;
        }

        if (!_imageCache.TryGetValue(texturePath, out var image))
        {
            var texture = TextureCache.Get(texturePath);
            image = texture.GetImage();
            _imageCache[texturePath] = image;
        }

        var pixelX = Mathf.Clamp((int)(uv.X * image.GetWidth()), 0, image.GetWidth() - 1);
        var pixelY = Mathf.Clamp((int)(uv.Y * image.GetHeight()), 0, image.GetHeight() - 1);
        return image.GetPixel(pixelX, pixelY).A > 0.1f;
    }

    // Everything the engine has to be asked for, handed to BillboardUv for the geometry: the
    // camera's backward axis and the pick ray it cast, plus the sprite's rendered half-extents
    // (PixelSize alone is fixed at creation time and reflects neither the accumulated
    // parent+self scale nor per-axis scaling, which ResourceNodeView does use - a tall-narrow
    // tree scales width and height independently, so each axis reads its own).
    private static Vector2? UvAt(Camera3D camera, Vector3 rayHitPosition, Sprite3D sprite, Vector3 spriteCenter)
    {
        var screenPosition = camera.UnprojectPosition(rayHitPosition);
        var scale = sprite.GlobalTransform.Basis.Scale;

        return BillboardUv.At(
            camera.GlobalTransform.Basis.Z,
            camera.ProjectRayOrigin(screenPosition),
            camera.ProjectRayNormal(screenPosition),
            spriteCenter,
            (sprite.PixelSize * sprite.Texture.GetWidth() * scale.X) / 2f,
            (sprite.PixelSize * sprite.Texture.GetHeight() * scale.Y) / 2f,
            sprite.FlipH);
    }
}
