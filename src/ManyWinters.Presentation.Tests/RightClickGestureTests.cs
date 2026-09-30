using Godot;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

// The right button turns the camera and opens the contextual menu, so a look around the camp
// must not end in a menu.
public class RightClickGestureTests
{
    private const float Threshold = 4f;

    [Fact]
    public void PressAndReleaseInTheSameSpotIsAClick()
    {
        var gesture = new RightClickGesture(Threshold);
        gesture.Press(new Vector2(100, 100));

        Assert.True(gesture.Release());
    }

    [Fact]
    public void ADragIsNotAClick()
    {
        var gesture = new RightClickGesture(Threshold);
        gesture.Press(new Vector2(100, 100));
        gesture.Moved(new Vector2(240, 180));

        Assert.False(gesture.Release());
    }

    // A mouse drifts under a real finger, so a nudge is still a click; a menu that refuses to
    // open half the time is worse than one that opens after a wobble.
    [Fact]
    public void ANudgeWithinTheSlackIsStillAClick()
    {
        var gesture = new RightClickGesture(Threshold);
        gesture.Press(new Vector2(100, 100));
        gesture.Moved(new Vector2(100 + Threshold, 100));

        Assert.True(gesture.Release());
    }

    [Fact]
    public void MotionPastTheSlackIsADragHoweverFarBackItComes()
    {
        var gesture = new RightClickGesture(Threshold);
        gesture.Press(new Vector2(100, 100));
        gesture.Moved(new Vector2(300, 100));
        gesture.Moved(new Vector2(100, 100));

        Assert.False(gesture.Release());
    }

    [Fact]
    public void AReleaseNobodyPressedIsNotAClick()
    {
        Assert.False(new RightClickGesture(Threshold).Release());
    }

    // Motion with the button up is nobody's business here: the camera has not been turned, and
    // the next press starts from where it went down.
    [Fact]
    public void MotionBeforeThePressDoesNotSpoilIt()
    {
        var gesture = new RightClickGesture(Threshold);
        gesture.Moved(new Vector2(500, 500));
        gesture.Press(new Vector2(100, 100));

        Assert.True(gesture.Release());
    }

    [Fact]
    public void AClickIsAnsweredOnceAndNotAgain()
    {
        var gesture = new RightClickGesture(Threshold);
        gesture.Press(new Vector2(100, 100));

        Assert.True(gesture.Release());
        Assert.False(gesture.Release());
    }

    [Fact]
    public void ADragIsForgottenByTheNextPress()
    {
        var gesture = new RightClickGesture(Threshold);
        gesture.Press(new Vector2(100, 100));
        gesture.Moved(new Vector2(400, 400));
        gesture.Release();

        gesture.Press(new Vector2(100, 100));

        Assert.True(gesture.Release());
    }
}
