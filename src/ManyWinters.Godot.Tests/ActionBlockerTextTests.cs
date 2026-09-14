using ManyWinters.Core.Commands;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// The words under a greyed-out button. A blocker nobody worded reads as an empty line, which
// looks to the player exactly like a button that is disabled for no reason at all.
public class ActionBlockerTextTests
{
    [Fact]
    public void EveryBlockerIsWorded()
    {
        var unworded = Enum.GetValues<ActionBlocker>()
            .Where(blocker => blocker != ActionBlocker.None)
            .Where(blocker => ActionBlockerText.For(blocker).Length == 0)
            .ToList();

        Assert.Empty(unworded);
    }

    // Nothing is wrong, so there is nothing to say - the panel hides the line rather than
    // printing a blank one.
    [Fact]
    public void NotBeingBlockedSaysNothing()
    {
        Assert.Equal(string.Empty, ActionBlockerText.For(ActionBlocker.None));
    }

    [Fact]
    public void TheSameBlockerAlwaysReadsTheSame()
    {
        Assert.Equal(ActionBlockerText.For(ActionBlocker.TooFar), ActionBlockerText.For(ActionBlocker.TooFar));
    }

    // Two refusals the player has to be able to tell apart: one is the store's shortage, the
    // other their own.
    [Fact]
    public void AnEmptyStoreAndAnEmptyPackReadDifferently()
    {
        Assert.NotEqual(
            ActionBlockerText.For(ActionBlocker.StoreIsEmpty),
            ActionBlockerText.For(ActionBlocker.MissingMaterials));
    }

    // Likewise "cannot teach at all" against "knows how to teach, just not this".
    [Fact]
    public void NotHavingLearnedAndNotKnowingTheTechniqueReadDifferently()
    {
        Assert.NotEqual(
            ActionBlockerText.For(ActionBlocker.NotLearned),
            ActionBlockerText.For(ActionBlocker.TeacherDoesNotKnowIt));
    }
}
