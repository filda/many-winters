using Godot;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class WorldSpaceTests
{
    // Ground rising along one axis only, so a swapped axis changes the answer.
    private static float SlopeAlongX(float x, float z) => x * 0.5f;

    private static float Flat(float x, float z) => 0f;

    [Fact]
    public void TheSimulationsSecondNumberBecomesTheRenderersThird()
    {
        // Unequal and negative on one axis: a swap is invisible on a square or centred point.
        var rendered = WorldSpace.ToRender(new Position(3, -7), heightAboveGround: 0f, Flat);

        Assert.Equal(3f, rendered.X, 5);
        Assert.Equal(-7f, rendered.Z, 5);
    }

    [Fact]
    public void HeightIsMeasuredFromTheGroundUnderThePointNotFromZero()
    {
        // Anything standing on the ground follows the terrain, not a fixed absolute height.
        var rendered = WorldSpace.ToRender(new Position(8, -7), heightAboveGround: 1.5f, SlopeAlongX);

        Assert.Equal(5.5f, rendered.Y, 5);
    }

    [Fact]
    public void TheGroundIsSampledAtTheRenderedPointNotTheSimulatedOne()
    {
        // The sampler takes render coordinates (x, z); passing the simulation's pair straight
        // through would read the height from the wrong spot.
        float SlopeAlongZ(float x, float z) => z * 0.5f;

        var rendered = WorldSpace.ToRender(new Position(8, -6), heightAboveGround: 0f, SlopeAlongZ);

        Assert.Equal(-3f, rendered.Y, 5);
    }

    [Fact]
    public void SomethingStandingOnFlatGroundSitsAtItsOwnOffset()
    {
        Assert.Equal(2f, WorldSpace.ToRender(new Position(0, 0), 2f, Flat).Y, 5);
    }

    [Fact]
    public void ComingBackTheOtherWayRecoversTheSameGroundPosition()
    {
        // Exact inverses on the ground plane whatever the height: a click on a hillside means
        // the ground under the cursor.
        var original = new Position(13.5, -4.25);

        var roundTripped = WorldSpace.ToSimulation(WorldSpace.ToRender(original, 3f, SlopeAlongX));

        Assert.Equal(original.X, roundTripped.X, 4);
        Assert.Equal(original.Y, roundTripped.Y, 4);
    }

    [Fact]
    public void ComingBackDropsHeightRatherThanFoldingItIntoTheGroundPosition()
    {
        // Two points one above the other are the same place to the simulation.
        var low = WorldSpace.ToSimulation(new Vector3(5f, 0f, -9f));
        var high = WorldSpace.ToSimulation(new Vector3(5f, 40f, -9f));

        Assert.Equal(low, high);
        Assert.Equal(5.0, low.X, 4);
        Assert.Equal(-9.0, low.Y, 4);
    }
}
