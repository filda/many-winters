using Godot;

namespace ManyWinters.Godot.Logic;

// The line from the camera to what it is looking at, and the test for whether something
// stands in the way. Main decides the target and which sprites to ask about.
internal readonly record struct SightLine(Vector3 Origin, Vector3 Direction, float Length)
{
    // Below this the camera sits on its own target: no line to be in the way of, nothing fades.
    private const float DegenerateLength = 0.001f;

    // Null when the camera and its target have collapsed onto each other.
    internal static SightLine? From(Vector3 cameraPosition, Vector3 targetPosition)
    {
        var toTarget = targetPosition - cameraPosition;
        var length = toTarget.Length();

        // Stryker disable once Equality: a computed length landing exactly on the epsilon has probability zero
        return length <= DegenerateLength ? null : new SightLine(cameraPosition, toTarget / length, length);
    }

    // `radius` is how far the thing reaches sideways from its centre, in metres, and `margin`
    // how much closer than that still blocks - a wide tree needs a bigger reach than a blade
    // of grass. `lengthTolerance` extends the far end: something level with the target is not
    // what the view needs cleared.
    internal bool IsBlockedBy(Vector3 position, float radius, float margin, float lengthTolerance)
    {
        var along = (position - Origin).Dot(Direction);

        // Behind the camera or past the target - neither is between the two.
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
