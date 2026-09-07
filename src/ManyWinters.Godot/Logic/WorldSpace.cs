using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// Between the simulation's coordinates and the renderer's. The simulation is flat: a `Position`
// is two numbers on the ground plane, X and Y. The renderer is not: X and *Z* are the ground
// plane there and Y is height. So the simulation's second number becomes the renderer's third,
// and both directions live here together so they cannot drift apart - a swap between them is
// invisible on anything square or centred, and shows up only as everything being mirrored
// across the diagonal.
//
// The simulation keeps its coordinates in double while the render space stays float. Safe
// while everything sits inside one small local patch; true continent-scale coordinates would
// need a floating origin here instead.
internal static class WorldSpace
{
    // `heightAboveGround` is measured from the real terrain under the point, not from a flat
    // zero - the ground is actual elevation, so anything standing on it has to follow.
    internal static Vector3 ToRender(Position position, float heightAboveGround, Func<float, float, float> groundHeightAt)
    {
        var x = (float)position.X;
        var z = (float)position.Y;

        return new Vector3(x, groundHeightAt(x, z) + heightAboveGround, z);
    }

    // Height is dropped rather than carried: the simulation has nowhere to put it, and a click
    // on a hillside means the ground under the cursor, not a point in the air.
    internal static Position ToSimulation(Vector3 renderPosition) =>
        new(renderPosition.X, renderPosition.Z);
}
