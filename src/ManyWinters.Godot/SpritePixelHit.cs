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
// derived from the horizontal component of the direction to the camera, not the camera's own
// (pitched) basis vectors - those would describe a full/spherical billboard's plane instead,
// which tilts to match camera elevation and this game's sprites never do.
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
        if (!TryGetUv(camera, rayHitPosition, sprite, spriteCenterOverride ?? sprite.GlobalPosition, out var uv))
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

    private static bool TryGetUv(Camera3D camera, Vector3 rayHitPosition, Sprite3D sprite, Vector3 spriteCenter, out Vector2 uv)
    {
        uv = default;

        var up = Vector3.Up;
        var toCameraHorizontal = new Vector3(
            camera.GlobalPosition.X - spriteCenter.X,
            0f,
            camera.GlobalPosition.Z - spriteCenter.Z);
        var horizontalDistance = toCameraHorizontal.Length();
        if (horizontalDistance < 0.0001f)
        {
            // Camera directly overhead (or underneath) the sprite - a FixedY billboard has
            // nothing left to yaw toward and renders edge-on/degenerate here. FreeCameraRig's
            // own tilt clamp keeps normal play well clear of this.
            return false;
        }

        // The direction the billboard's plane faces (from the sprite toward the camera,
        // horizontally) - matches camera.GlobalTransform.Basis.X exactly whenever the camera
        // has zero pitch, and stays correct at any pitch since only yaw affects it.
        var look = toCameraHorizontal / horizontalDistance;
        var right = up.Cross(look);

        var rayOrigin = camera.GlobalPosition;
        var toHit = rayHitPosition - rayOrigin;
        var rayLength = toHit.Length();
        if (rayLength < 0.0001f)
        {
            return false;
        }

        var rayDirection = toHit / rayLength;
        var denominator = rayDirection.Dot(look);
        if (Mathf.Abs(denominator) < 0.0001f)
        {
            return false;
        }

        var t = (spriteCenter - rayOrigin).Dot(look) / denominator;
        var pointOnBillboardPlane = rayOrigin + (rayDirection * t);
        var offset = pointOnBillboardPlane - spriteCenter;
        var localRight = offset.Dot(right);
        var localUp = offset.Dot(up);

        // Accumulated parent+self scale (EntityVisualVariation, HoverHighlight, ...) - PixelSize
        // alone is fixed at creation time and doesn't reflect either. Width and height read
        // their own axis rather than sharing one - ResourceNodeView can give a resource
        // independent width/height scaling (a tall-narrow vs. short-wide tree), so the two no
        // longer necessarily match.
        var scale = sprite.GlobalTransform.Basis.Scale;
        var halfWidth = (sprite.PixelSize * sprite.Texture.GetWidth() * scale.X) / 2f;
        var halfHeight = (sprite.PixelSize * sprite.Texture.GetHeight() * scale.Y) / 2f;
        if (halfWidth <= 0f || halfHeight <= 0f)
        {
            return false;
        }

        // Image V grows downward; the sprite's local "up" (positive localUp = higher on
        // screen) is the opposite direction.
        var u = 0.5f + (localRight / (halfWidth * 2f));
        var v = 0.5f - (localUp / (halfHeight * 2f));
        if (u is < 0f or > 1f || v is < 0f or > 1f)
        {
            return false;
        }

        // FlipH mirrors the rendered texture horizontally (ResourceNodeView's per-instance
        // mirroring) without touching the node's actual transform, so this manual UV lookup
        // has to mirror U itself too or it would sample the wrong side of an asymmetric
        // silhouette - reading opaque where the flipped render is actually transparent.
        if (sprite.FlipH)
        {
            u = 1f - u;
        }

        uv = new Vector2(u, v);
        return true;
    }
}
