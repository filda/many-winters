using Godot;

namespace ManyWinters.Godot.Logic;

// The line from the camera to whatever it is meant to be looking at, and the test for whether
// something stands in the way of it. Main decides what the target is and which sprites are
// worth asking about; the geometry is here.
internal readonly record struct SightLine(Vector3 Origin, Vector3 Direction, float Length)
{
    // Below this the camera is effectively sitting on its own target, so there is no line to
    // be in the way of and nothing should fade.
    private const float DegenerateLength = 0.001f;

    // Null when the camera and its target have collapsed onto each other.
    internal static SightLine? From(Vector3 cameraPosition, Vector3 targetPosition)
    {
        var toTarget = targetPosition - cameraPosition;
        var length = toTarget.Length();

        // Stryker disable once Equality: a computed length landing exactly on the epsilon has probability zero
        return length <= DegenerateLength ? null : new SightLine(cameraPosition, toTarget / length, length);
    }

    // `radius` is how far the thing reaches to the side of its own centre, in metres, and
    // `margin` how much closer than that still counts as blocking - a wide tree needs a much
    // bigger "in the way" reach than a thin blade of grass, not one flat distance for both.
    //
    // `lengthTolerance` extends the far end a little: something standing essentially level
    // with the target is not what the view needs cleared.
    internal bool IsBlockedBy(Vector3 position, float radius, float margin, float lengthTolerance)
    {
        var along = (position - Origin).Dot(Direction);

        // Behind the camera, or past the target - neither is between the two, which is the
        // only place that counts.
        // Stryker disable once Equality: something exactly on the camera plane, or exactly at the far end, is a
        // measure-zero case for a computed position - either side of the boundary reads the same in play
        if (along <= 0f || along >= Length + lengthTolerance)
        {
            return false;
        }

        var closestPointOnLine = Origin + (Direction * along);

        // Stryker disable once Equality: a distance landing exactly on reach plus margin has probability zero
        return (position - closestPointOnLine).Length() < radius + margin;
    }
}
