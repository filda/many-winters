using Godot;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class WorldSpaceTests
{
    // Ground that rises along one axis only, so a swapped axis changes the answer rather than
    // hiding behind a flat plane.
    private static float SlopeAlongX(float x, float z) => x * 0.5f;

    private static float Flat(float x, float z) => 0f;

    [Fact]
    public void TheSimulationsSecondNumberBecomesTheRenderersThird()
    {
        // Deliberately unequal, and negative on one axis: on a square or centred point a swap
        // between them is invisible.
        var rendered = WorldSpace.ToRender(new Position(3, -7), heightAboveGround: 0f, Flat);

        Assert.Equal(3f, rendered.X, 5);
        Assert.Equal(-7f, rendered.Z, 5);
    }

    [Fact]
    public void HeightIsMeasuredFromTheGroundUnderThePointNotFromZero()
    {
        // The ground is real elevation, so anything standing on it has to follow the terrain
        // rather than hovering at a fixed absolute height.
        var rendered = WorldSpace.ToRender(new Position(8, -7), heightAboveGround: 1.5f, SlopeAlongX);

        Assert.Equal(5.5f, rendered.Y, 5);
    }

    [Fact]
    public void TheGroundIsSampledAtTheRenderedPointNotTheSimulatedOne()
    {
        // The sampler takes render coordinates, so it must be handed x and z - passing the
        // simulation's pair straight through would read the height from the wrong spot on any
        // ground that is not flat.
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
        // The two directions have to be exact inverses on the ground plane, whatever the
        // height in between - a click on a hillside means the ground under the cursor.
        var original = new Position(13.5, -4.25);

        var roundTripped = WorldSpace.ToSimulation(WorldSpace.ToRender(original, 3f, SlopeAlongX));

        Assert.Equal(original.X, roundTripped.X, 4);
        Assert.Equal(original.Y, roundTripped.Y, 4);
    }

    [Fact]
    public void ComingBackDropsHeightRatherThanFoldingItIntoTheGroundPosition()
    {
        // Two points one above the other are the same place as far as the simulation is
        // concerned; height has nowhere to go there.
        var low = WorldSpace.ToSimulation(new Vector3(5f, 0f, -9f));
        var high = WorldSpace.ToSimulation(new Vector3(5f, 40f, -9f));

        Assert.Equal(low, high);
        Assert.Equal(5.0, low.X, 4);
        Assert.Equal(-9.0, low.Y, 4);
    }
}
