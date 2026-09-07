using Godot;

namespace ManyWinters.Godot.Logic;

// The arithmetic behind the free camera, apart from the nodes it moves and the keys it reads.
// Tuning stays with FreeCameraRig, which passes it in - what lives here is only how a rate, a
// span of time and a current value combine, which is the part that can be wrong without
// anything failing to compile.
internal static class CameraMotion
{
    // Zoom is multiplicative, not additive: the range spans roughly 3 to 2000, so a fixed step
    // is glacial at one end and wild at the other. `signedSeconds` is how long the zoom was
    // held, negative to zoom in - a mouse notch passes a fixed fraction of a second so the
    // wheel and the held key share one feel instead of two separately tuned steps.
    internal static float Zoomed(float current, float signedSeconds, float ratePerSecond, float min, float max) =>
        Mathf.Clamp(current * MathF.Pow(ratePerSecond, signedSeconds), min, max);

    // Clamped because both ends break the illusion: fully edge-on flattens the world, and
    // fully overhead leaves a FixedY billboard nothing to yaw toward, so every sprite would
    // render edge-on and vanish (see BillboardSprite).
    internal static float Tilted(float currentDegrees, float deltaDegrees, float minDegrees, float maxDegrees) =>
        Mathf.Clamp(currentDegrees + deltaDegrees, minDegrees, maxDegrees);

    // Where the camera sits relative to the rig it orbits, as a unit vector: height and
    // horizontal distance both come off this one angle, so they rise and fall together rather
    // than being tuned separately.
    internal static Vector3 OffsetDirection(float tiltDegrees)
    {
        var radians = Mathf.DegToRad(tiltDegrees);

        return new Vector3(0f, MathF.Sin(radians), MathF.Cos(radians));
    }

    // Pan runs along the rig's own axes flattened to the horizontal, so panning never lifts
    // the view off the ground however far the camera is tilted. Speed scales with zoom, for
    // the same reason zoom itself is multiplicative.
    // No early-out for "no keys held": normalizing a zero vector gives zero back rather than
    // a division by its own length, so an idle frame already answers zero on its own.
    internal static Vector3 PanVelocity(Basis rigBasis, Vector2 input, float speed)
    {
        var forward = new Vector3(rigBasis.Z.X, 0f, rigBasis.Z.Z).Normalized();
        var right = new Vector3(rigBasis.X.X, 0f, rigBasis.X.Z).Normalized();

        // Normalized after combining, so holding two keys does not travel faster diagonally
        // than one key does straight.
        return ((right * input.X) + (forward * input.Y)).Normalized() * speed;
    }

    // Exponential ease rather than snapping to the target velocity, so starting and stopping
    // both feel smooth. 1/easeRate is roughly the time constant: the seconds it takes to close
    // about 63% of the remaining gap.
    internal static Vector3 Eased(Vector3 current, Vector3 target, float easeRate, float delta) =>
        current.Lerp(target, 1f - MathF.Exp(-easeRate * delta));

    // The camera sits offset from the rig, so it can drift over a bump the rig itself is not
    // standing on - a low tilt at close zoom is where that happens. Never below the ground it
    // is over, plus a little clearance.
    internal static float ClearedHeight(float height, float groundHeight, float clearance) =>
        MathF.Max(height, groundHeight + clearance);
}
