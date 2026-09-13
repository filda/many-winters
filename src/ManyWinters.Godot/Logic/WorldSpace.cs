using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// Between the simulation's coordinates and the renderer's. The simulation is flat: a `Position`
// is X and Y on the ground plane. The renderer's ground plane is X and *Z*, with Y as height,
// so the simulation's second number becomes the renderer's third. Both directions live here so
// they cannot drift apart - a swap shows only as everything mirrored across the diagonal.
//
// The simulation uses double, render space float. Safe inside one small local patch;
// continent-scale coordinates would need a floating origin here.
internal static class WorldSpace
{
    // `heightAboveGround` is measured from the terrain under the point, not from a flat zero.
    internal static Vector3 ToRender(Position position, float heightAboveGround, Func<float, float, float> groundHeightAt)
    {
        var x = (float)position.X;
        var z = (float)position.Y;

        return new Vector3(x, groundHeightAt(x, z) + heightAboveGround, z);
    }

    // Height is dropped: the simulation has nowhere to put it, and a click on a hillside means
    // the ground under the cursor, not a point in the air.
    internal static Position ToSimulation(Vector3 renderPosition) =>
        new(renderPosition.X, renderPosition.Z);
}
