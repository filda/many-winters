using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class HoverArbiterTests
{
    [Fact]
    public void TakingHoverLightsTheNewTargetUp()
    {
        var arbiter = new HoverArbiter();
        var person = new FakeHoverable();

        arbiter.Set(person, true);

        Assert.True(person.IsShowingHovered);
    }

    [Fact]
    public void OnlyOneThingIsEverLitAtOnce()
    {
        // The cursor is over one thing, so the previous holder loses the highlight even though
        // nothing told it directly - a view lit through HoverRescue never gets mouse_exited.
        var arbiter = new HoverArbiter();
        var rescued = new FakeHoverable();
        var nearest = new FakeHoverable();

        arbiter.Set(rescued, true);
        arbiter.Set(nearest, true);

        Assert.False(rescued.IsShowingHovered);
        Assert.True(nearest.IsShowingHovered);
    }

    [Fact]
    public void ReTakingHoverWithTheSameTargetLeavesItAloneRatherThanReApplyingIt()
    {
        // Mouse motion arrives every frame; re-lighting an already lit view churns Modulate/Scale.
        var arbiter = new HoverArbiter();
        var person = new FakeHoverable();

        arbiter.Set(person, true);
        arbiter.Set(person, true);

        Assert.Equal(1, person.ShownHoveredCount);
    }

    [Fact]
    public void GivingUpHoverPutsTheTargetBackToNormal()
    {
        var arbiter = new HoverArbiter();
        var person = new FakeHoverable();

        arbiter.Set(person, true);
        arbiter.Set(person, false);

        Assert.False(person.IsShowingHovered);
    }

    [Fact]
    public void GivingUpHoverFromSomethingThatNeverHadItLeavesTheRealOneLit()
    {
        // Every view answers "no" to its own pixel test far more often than "yes"; those answers
        // must not put out somebody else's highlight.
        var arbiter = new HoverArbiter();
        var lit = new FakeHoverable();
        var missed = new FakeHoverable();

        arbiter.Set(lit, true);
        arbiter.Set(missed, false);

        Assert.True(lit.IsShowingHovered);
    }

    [Fact]
    public void ClearingPutsOutWhateverWasLit()
    {
        var arbiter = new HoverArbiter();
        var person = new FakeHoverable();

        arbiter.Set(person, true);
        arbiter.Clear();

        Assert.False(person.IsShowingHovered);
    }

    [Fact]
    public void ClearingTwiceOnlyPutsTheTargetOutOnce()
    {
        var arbiter = new HoverArbiter();
        var person = new FakeHoverable();

        arbiter.Set(person, true);
        arbiter.Clear();
        arbiter.Clear();

        Assert.Equal(1, person.ShownNormalCount);
    }

    [Fact]
    public void RevalidatingPutsOutATargetTheCursorHasLeft()
    {
        // A person walks out from under a resting cursor: no picking event of any kind is coming.
        var arbiter = new HoverArbiter();
        var walkedAway = new FakeHoverable { IsUnderCursor = true };

        arbiter.Set(walkedAway, true);
        walkedAway.IsUnderCursor = false;
        arbiter.Revalidate();

        Assert.False(walkedAway.IsShowingHovered);
    }

    [Fact]
    public void RevalidatingLeavesATargetTheCursorIsStillOnAlone()
    {
        var arbiter = new HoverArbiter();
        var stillThere = new FakeHoverable { IsUnderCursor = true };

        arbiter.Set(stillThere, true);
        arbiter.Revalidate();

        Assert.True(stillThere.IsShowingHovered);
    }

    [Fact]
    public void RevalidatingWithNothingLitAsksNobodyAnything()
    {
        // Runs every frame, mostly with nothing hovered; it must not reach for an absent target.
        var arbiter = new HoverArbiter();
        var person = new FakeHoverable { IsUnderCursor = false };

        arbiter.Revalidate();

        Assert.Equal(0, person.UnderCursorChecks);
    }

    [Fact]
    public void AForgottenTargetIsNeverCalledBackInto()
    {
        // A view is forgotten because it is queued for freeing; calling into it afterwards crashes.
        var arbiter = new HoverArbiter();
        var removed = new FakeHoverable();

        arbiter.Set(removed, true);
        arbiter.Forget(removed);
        arbiter.Clear();

        Assert.Equal(0, removed.ShownNormalCount);
    }

    [Fact]
    public void ForgettingSomethingThatWasNotLitLeavesTheRealOneAlone()
    {
        // Views leave the scene constantly, almost never while lit.
        var arbiter = new HoverArbiter();
        var lit = new FakeHoverable { IsUnderCursor = true };
        var removed = new FakeHoverable();

        arbiter.Set(lit, true);
        arbiter.Forget(removed);
        arbiter.Revalidate();

        Assert.True(lit.IsShowingHovered);
    }

    private sealed class FakeHoverable : IHoverable
    {
        public bool IsShowingHovered { get; private set; }

        public bool IsUnderCursor { get; set; }

        public int ShownHoveredCount { get; private set; }

        public int ShownNormalCount { get; private set; }

        public int UnderCursorChecks { get; private set; }

        public void ShowHovered(bool hovered)
        {
            IsShowingHovered = hovered;
            if (hovered)
            {
                ShownHoveredCount++;
            }
            else
            {
                ShownNormalCount++;
            }
        }

        public bool IsStillUnderCursor()
        {
            UnderCursorChecks++;
            return IsUnderCursor;
        }
    }
}
