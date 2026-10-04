using Godot;

namespace ManyWinters.Presentation.Logic;

internal static class GroundContact
{
    // A node is placed half a nominal height above the ground, which seats its bottom edge on
    // the ground only at scale 1; scaling multiplies that half-height, so this is the shift
    // that cancels the difference.
    internal static Vector3 Lift(float nominalHeight, float heightScale) =>
        new(0f, nominalHeight / 2f * (heightScale - 1f), 0f);
}
