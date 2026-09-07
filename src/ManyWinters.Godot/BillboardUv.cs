using Godot;

namespace ManyWinters.Godot;

// Where a pick ray lands on a FixedY billboard's rendered plane, as texture UV - the geometry
// half of SpritePixelHit, with nothing of the engine left in it. See that class for why the
// plane has to be reconstructed at all rather than read off the node's own transform.
internal static class BillboardUv
{
    // Below this, a direction is too short to normalize or a ray too parallel to the plane for
    // the intersection to mean anything.
    private const float Degenerate = 0.0001f;

    // cameraBackward is the camera's own backward axis (its Basis.Z, pointing from the scene
    // toward the viewer); only its horizontal part matters, which is exactly what makes this a
    // FixedY billboard - camera pitch must not tilt the plane. rayOrigin/rayDirection are the
    // pick ray as the engine cast it, which is not "camera position toward the hit" under an
    // orthographic projection. halfWidth/halfHeight are the sprite's rendered half-extents in
    // world metres, already scaled.
    //
    // Null means the ray does not land on the sprite at all: the camera is looking straight
    // down (no yaw left to face), the ray runs parallel to the plane, the sprite has no size,
    // or the hit falls outside its rectangle.
    internal static Vector2? At(
        Vector3 cameraBackward,
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 spriteCenter,
        float halfWidth,
        float halfHeight,
        bool flipH)
    {
        if (halfWidth <= 0f || halfHeight <= 0f)
        {
            return null;
        }

        var up = Vector3.Up;
        var look = new Vector3(cameraBackward.X, 0f, cameraBackward.Z);
        var horizontalLength = look.Length();
        // Stryker disable once Equality: a computed length landing exactly on the epsilon has probability zero
        if (horizontalLength < Degenerate)
        {
            return null;
        }

        look /= horizontalLength;
        var right = up.Cross(look);

        var denominator = rayDirection.Dot(look);
        // Stryker disable once Equality: as the length check above - a continuous value at exactly the epsilon
        if (Mathf.Abs(denominator) < Degenerate)
        {
            return null;
        }

        var t = (spriteCenter - rayOrigin).Dot(look) / denominator;
        var offset = rayOrigin + (rayDirection * t) - spriteCenter;
        var localRight = offset.Dot(right);
        var localUp = offset.Dot(up);

        // Image V grows downward; the sprite's local "up" (positive localUp = higher on
        // screen) is the opposite direction.
        var u = 0.5f + (localRight / (halfWidth * 2f));
        var v = 0.5f - (localUp / (halfHeight * 2f));
        if (u is < 0f or > 1f || v is < 0f or > 1f)
        {
            return null;
        }

        // FlipH mirrors the rendered texture horizontally (ResourceNodeView's per-instance
        // mirroring) without touching the node's actual transform, so this manual UV lookup
        // has to mirror U itself too or it would sample the wrong side of an asymmetric
        // silhouette - reading opaque where the flipped render is actually transparent.
        return new Vector2(flipH ? 1f - u : u, v);
    }
}
