using Godot;

namespace ManyWinters.Godot.Logic;

// Where a pick ray lands on a FixedY billboard, all the way from world metres through texture
// UV to the pixel index finally sampled - the geometry half of SpritePixelHit, with nothing of
// the engine left in it. See that class for why the plane has to be reconstructed at all
// rather than read off the node's own transform.
internal static class BillboardUv
{
    // Below this, a direction is too short to normalize or a ray too parallel to the plane for
    // the intersection to mean anything.
    private const float Degenerate = 0.0001f;

    // cameraBackward is the camera's own backward axis (its Basis.Z, pointing from the scene
    // toward the viewer); only its horizontal part matters, which is exactly what makes this a
    // FixedY billboard - camera pitch must not tilt the plane. rayOrigin/rayDirection are the
    // pick ray as the engine cast it, which is not "camera position toward the hit" under an
    // orthographic projection. width/height are the sprite's rendered size in world metres,
    // already scaled (see RenderedSize).
    //
    // Null means the ray does not land on the sprite at all: the camera is looking straight
    // down (no yaw left to face), the ray runs parallel to the plane, the sprite has no size,
    // or the hit falls outside its rectangle.
    internal static Vector2? At(
        Vector3 cameraBackward,
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 spriteCenter,
        float width,
        float height,
        bool flipH)
    {
        // Load-bearing, not an optimisation: a zero size divides a zero offset into NaN, and
        // every NaN comparison is false, so it would sail through the range check below and
        // come back as a NaN coordinate. A negative one divides cleanly into nonsense.
        if (width <= 0f || height <= 0f)
        {
            return null;
        }

        var look = new Vector3(cameraBackward.X, 0f, cameraBackward.Z);
        var horizontalLength = look.Length();
        // Stryker disable once Equality: a computed length landing exactly on the epsilon has probability zero
        if (horizontalLength < Degenerate)
        {
            return null;
        }

        look /= horizontalLength;
        var right = Vector3.Up.Cross(look);

        var denominator = rayDirection.Dot(look);
        // Stryker disable once Equality: as the length check above - a continuous value at exactly the epsilon
        if (Mathf.Abs(denominator) < Degenerate)
        {
            return null;
        }

        var t = (spriteCenter - rayOrigin).Dot(look) / denominator;
        var offset = rayOrigin + (rayDirection * t) - spriteCenter;
        var localRight = offset.Dot(right);
        var localUp = offset.Dot(Vector3.Up);

        // Image V grows downward; the sprite's local "up" (positive localUp = higher on
        // screen) is the opposite direction.
        var u = 0.5f + (localRight / width);
        var v = 0.5f - (localUp / height);
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

    // How big the sprite actually renders, in world metres. Each axis reads its own scale
    // rather than sharing one: ResourceNodeView gives a resource independent width/height
    // scaling (a tall-narrow versus a short-wide tree), so the two do not necessarily match.
    // PixelSize alone will not do - it is fixed when the sprite is created and reflects neither
    // the accumulated parent+self scale nor that per-axis part.
    internal static Vector2 RenderedSize(float pixelSize, int textureWidth, int textureHeight, float scaleX, float scaleY) =>
        new(pixelSize * textureWidth * scaleX, pixelSize * textureHeight * scaleY);

    // Which pixel a UV coordinate names. Truncating puts u in [0, 1) onto 0..width-1, and the
    // clamp catches the one value that would fall off the end: u exactly 1 - the sprite's own
    // right edge, which a ray through the corner really does produce - truncates to width
    // itself. One past the end is a one-pixel-wrong sample at best and out of bounds at worst.
    internal static (int X, int Y) PixelAt(Vector2 uv, int width, int height) =>
        (Mathf.Clamp((int)(uv.X * width), 0, width - 1), Mathf.Clamp((int)(uv.Y * height), 0, height - 1));
}
