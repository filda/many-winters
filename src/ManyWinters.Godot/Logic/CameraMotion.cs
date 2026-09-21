using Godot;

namespace ManyWinters.Godot.Logic;

// The arithmetic behind the free camera, apart from the nodes it moves and the keys it reads.
// Tuning stays with FreeCameraRig, which passes it in.
internal static class CameraMotion
{
    // Multiplicative: the range spans roughly 3 to 2000, so a fixed step is glacial at one
    // end and wild at the other. `signedSeconds` is how long the zoom was held, negative to
    // zoom in; a mouse notch passes a fixed fraction of a second so wheel and key share one
    // feel.
    internal static float Zoomed(float current, float signedSeconds, float ratePerSecond, float min, float max) =>
        Mathf.Clamp(current * MathF.Pow(ratePerSecond, signedSeconds), min, max);

    // Clamped because both ends break the illusion: edge-on flattens the world, and overhead
    // leaves a FixedY billboard nothing to yaw toward, so every sprite renders edge-on and
    // vanishes.
    internal static float Tilted(float currentDegrees, float deltaDegrees, float minDegrees, float maxDegrees) =>
        Mathf.Clamp(currentDegrees + deltaDegrees, minDegrees, maxDegrees);

    // Camera position relative to the rig it orbits, as a unit vector: height and horizontal
    // distance both come off this one angle, so they rise and fall together.
    internal static Vector3 OffsetDirection(float tiltDegrees)
    {
        var radians = Mathf.DegToRad(tiltDegrees);

        return new Vector3(0f, MathF.Sin(radians), MathF.Cos(radians));
    }

    // Pan runs along the rig's axes flattened to the horizontal, so it never lifts the view off
    // the ground whatever the tilt. Speed scales with zoom, as zoom itself is multiplicative. No
    // early-out for no input: Normalized() on a zero vector returns zero.
    internal static Vector3 PanVelocity(Basis rigBasis, Vector2 input, float speed)
    {
        var forward = new Vector3(rigBasis.Z.X, 0f, rigBasis.Z.Z).Normalized();
        var right = new Vector3(rigBasis.X.X, 0f, rigBasis.X.Z).Normalized();

        // Normalized after combining, so two keys held do not travel faster diagonally.
        return ((right * input.X) + (forward * input.Y)).Normalized() * speed;
    }

    // Exponential ease toward the target velocity, so starting and stopping feel smooth.
    // 1/easeRate is roughly the time constant: seconds to close about 63% of the gap.
    internal static Vector3 Eased(Vector3 current, Vector3 target, float easeRate, float delta) =>
        current.Lerp(target, 1f - MathF.Exp(-easeRate * delta));

    // The camera sits offset from the rig, so it can drift over a bump the rig is not standing
    // on (low tilt at close zoom). Never below the ground plus a little clearance.
    internal static float ClearedHeight(float height, float groundHeight, float clearance) =>
        MathF.Max(height, groundHeight + clearance);
}
