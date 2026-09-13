using Godot;

namespace ManyWinters.Godot.Logic;

// Where a pick ray lands on a FixedY billboard, from world metres through texture UV to the
// pixel sampled - the geometry half of SpritePixelHit, with nothing of the engine in it. See
// that class for why the plane is reconstructed rather than read off the node's transform.
internal static class BillboardUv
{
    // Below this, a direction is too short to normalize or a ray too parallel to the plane for
    // the intersection to mean anything.
    private const float Degenerate = 0.0001f;

    // cameraBackward is the camera's Basis.Z (scene toward viewer); only its horizontal part
    // counts, which is what keeps the plane FixedY under camera pitch. rayOrigin/rayDirection
    // are the pick ray as the engine cast it - under an orthographic projection that is not
    // "camera position toward the hit". width/height are the rendered size in world metres,
    // already scaled (see RenderedSize). Null when the ray misses: camera looking straight
    // down, ray parallel to the plane, zero size, or the hit outside the rectangle.
    internal static Vector2? At(
        Vector3 cameraBackward,
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 spriteCenter,
        float width,
        float height,
        bool flipH)
    {
        // Load-bearing: a zero size divides into NaN, and NaN fails every comparison, so it
        // would pass the range check below. A negative one divides cleanly into nonsense.
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

        // Image V grows downward; the sprite's local up is the opposite direction.
        var u = 0.5f + (localRight / width);
        var v = 0.5f - (localUp / height);
        if (u is < 0f or > 1f || v is < 0f or > 1f)
        {
            return null;
        }

        // FlipH mirrors the rendered texture without touching the node's transform
        // (ResourceNodeView's per-instance mirroring), so U has to be mirrored here too or an
        // asymmetric silhouette samples the wrong side.
        return new Vector2(flipH ? 1f - u : u, v);
    }

    // Rendered size in world metres. Per-axis scale because ResourceNodeView scales width and
    // height independently. PixelSize alone will not do: it is fixed at creation and reflects
    // neither the accumulated parent+self scale nor the per-axis part.
    internal static Vector2 RenderedSize(float pixelSize, int textureWidth, int textureHeight, float scaleX, float scaleY) =>
        new(pixelSize * textureWidth * scaleX, pixelSize * textureHeight * scaleY);

    // Which pixel a UV names. Truncation maps [0, 1) onto 0..width-1; the clamp catches u
    // exactly 1 (a ray through the sprite's own edge produces it), which would land one past
    // the end.
    internal static (int X, int Y) PixelAt(Vector2 uv, int width, int height) =>
        (Mathf.Clamp((int)(uv.X * width), 0, width - 1), Mathf.Clamp((int)(uv.Y * height), 0, height - 1));
}
