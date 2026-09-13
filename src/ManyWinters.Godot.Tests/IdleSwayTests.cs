using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class IdleSwayTests
{
    private static readonly WalkCycle.Pose Leaning = new(new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0f, 0.12f));
    private static readonly WalkCycle.Pose Upright = new(Vector3.Zero, Vector3.Zero);

    [Fact]
    public void AValueEasesTowardItsTargetByTheTimeConstant()
    {
        // Half a second against a one-second constant: 1 - e^-0.5, about 39% of the way.
        Assert.Equal(1f - MathF.Exp(-0.5f), IdleSway.Settle(0f, 1f, delta: 0.5f, settleSeconds: 1f), 5);
    }

    [Fact]
    public void AValueEasesDownAsWellAsUp()
    {
        Assert.Equal(0.8f * MathF.Exp(-0.5f), IdleSway.Settle(0.8f, 0f, delta: 0.5f, settleSeconds: 1f), 5);
    }

    [Fact]
    public void APoseEasesBothItsOffsetAndItsRotationByTheSameShare()
    {
        var settled = IdleSway.Settle(Leaning, Upright, delta: 0.5f, settleSeconds: 1f);

        Assert.Equal(0.08f * MathF.Exp(-0.5f), settled.Offset.Y, 5);
        Assert.Equal(0.12f * MathF.Exp(-0.5f), settled.Rotation.Z, 5);
        Assert.Equal(0f, settled.Offset.X);
        Assert.Equal(0f, settled.Rotation.X);
    }

    [Fact]
    public void AShorterTimeConstantSettlesFaster()
    {
        var slow = IdleSway.Settle(Leaning, Upright, delta: 0.1f, settleSeconds: 2f);
        var fast = IdleSway.Settle(Leaning, Upright, delta: 0.1f, settleSeconds: 0.2f);

        Assert.True(fast.Offset.Y < slow.Offset.Y);
        Assert.True(IdleSway.Settle(0f, 1f, 0.1f, 0.2f) > IdleSway.Settle(0f, 1f, 0.1f, 2f));
    }

    [Fact]
    public void NoTimeMeansNoMovement()
    {
        Assert.Equal(Leaning, IdleSway.Settle(Leaning, Upright, delta: 0f, settleSeconds: 1f));
        Assert.Equal(0.3f, IdleSway.Settle(0.3f, 1f, delta: 0f, settleSeconds: 1f));
    }

    [Fact]
    public void ALongTimeArrivesWithoutOvershooting()
    {
        var settled = IdleSway.Settle(Leaning, Upright, delta: 100f, settleSeconds: 0.5f);

        Assert.Equal(0f, settled.Offset.Y, 5);
        Assert.Equal(0f, settled.Rotation.Z, 5);
        Assert.Equal(1f, IdleSway.Settle(0f, 1f, delta: 100f, settleSeconds: 0.5f), 5);
    }

    // The target need not be zero or one: settling toward it from either side moves the same
    // share of the remaining distance.
    [Fact]
    public void SettlingWorksTowardAnyTargetFromEitherSide()
    {
        var weight = 1f - MathF.Exp(-1f);

        Assert.Equal(0.08f + ((0.02f - 0.08f) * weight), IdleSway.Settle(0.08f, 0.02f, delta: 0.3f, settleSeconds: 0.3f), 5);
        Assert.Equal(0.02f * weight, IdleSway.Settle(0f, 0.02f, delta: 0.3f, settleSeconds: 0.3f), 5);
    }
}
