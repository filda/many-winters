using Godot;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

public class GroundContactTests
{
    [Fact]
    public void ANodeAtFullScaleNeedsNoLift()
    {
        Assert.Equal(Vector3.Zero, GroundContact.Lift(1.8f, 1f));
    }

    [Fact]
    public void AShrunkNodeSinksByTheHalfHeightItLost()
    {
        var lift = GroundContact.Lift(1.8f, 0.5f);

        Assert.Equal(-0.45, lift.Y, 5);
        Assert.Equal(0f, lift.X);
        Assert.Equal(0f, lift.Z);
    }

    [Fact]
    public void AGrownNodeRisesByTheHalfHeightItGained()
    {
        Assert.Equal(0.2, GroundContact.Lift(2f, 1.2f).Y, 5);
    }
}
