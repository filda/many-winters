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
// where the pick ray crosses the sprite's actual (camera-facing) plane, using the camera's
// own basis vectors as that plane's real screen-facing right/up - which is what "Enabled"
// (full/spherical) billboard mode means by definition, not an approximation of it.
public static class SpritePixelHit
{
    private static readonly Dictionary<string, Image> _imageCache = new();

    public static bool IsOpaqueAt(Camera3D camera, Vector3 rayHitPosition, Sprite3D sprite, string texturePath)
    {
        if (!TryGetUv(camera, rayHitPosition, sprite, out var uv))
        {
            return false;
        }

        if (!_imageCache.TryGetValue(texturePath, out var image))
        {
            var texture = ResourceLoader.Load<Texture2D>(texturePath);
            image = texture.GetImage();
            _imageCache[texturePath] = image;
        }

        var pixelX = Mathf.Clamp((int)(uv.X * image.GetWidth()), 0, image.GetWidth() - 1);
        var pixelY = Mathf.Clamp((int)(uv.Y * image.GetHeight()), 0, image.GetHeight() - 1);
        return image.GetPixel(pixelX, pixelY).A > 0.1f;
    }

    private static bool TryGetUv(Camera3D camera, Vector3 rayHitPosition, Sprite3D sprite, out Vector2 uv)
    {
        uv = default;

        var basis = camera.GlobalTransform.Basis;
        var right = basis.X;
        var up = basis.Y;
        var forward = -basis.Z;

        var rayOrigin = camera.GlobalPosition;
        var toHit = rayHitPosition - rayOrigin;
        var rayLength = toHit.Length();
        if (rayLength < 0.0001f)
        {
            return false;
        }

        var rayDirection = toHit / rayLength;
        var denominator = rayDirection.Dot(forward);
        if (Mathf.Abs(denominator) < 0.0001f)
        {
            return false;
        }

        var spriteCenter = sprite.GlobalPosition;
        var t = (spriteCenter - rayOrigin).Dot(forward) / denominator;
        var pointOnBillboardPlane = rayOrigin + (rayDirection * t);
        var offset = pointOnBillboardPlane - spriteCenter;
        var localRight = offset.Dot(right);
        var localUp = offset.Dot(up);

        // Accumulated parent+self scale (EntityVisualVariation, HoverHighlight, ...) - PixelSize
        // alone is fixed at creation time and doesn't reflect either.
        var scale = sprite.GlobalTransform.Basis.Scale.X;
        var halfWidth = (sprite.PixelSize * sprite.Texture.GetWidth() * scale) / 2f;
        var halfHeight = (sprite.PixelSize * sprite.Texture.GetHeight() * scale) / 2f;
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

        uv = new Vector2(u, v);
        return true;
    }
}
