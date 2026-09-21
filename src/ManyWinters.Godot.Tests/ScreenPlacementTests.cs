using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// Where a panel placed by the cursor ends up once the screen has had its say.
public class ScreenPlacementTests
{
    private static readonly Vector2 Screen = new(1000, 800);
    private static readonly Vector2 Size = new(200, 300);
    private const float Margin = 8f;

    [Fact]
    public void APanelWithRoomForItselfIsLeftWhereItWasPut()
    {
        Assert.Equal(new Vector2(100, 100), ScreenPlacement.KeptOnScreen(new Vector2(100, 100), Size, Screen, Margin));
    }

    [Fact]
    public void APanelOpenedNearTheRightEdgeIsPushedBackInside()
    {
        var placed = ScreenPlacement.KeptOnScreen(new Vector2(950, 100), Size, Screen, Margin);

        Assert.Equal(Screen.X - Size.X - Margin, placed.X);
        Assert.Equal(100, placed.Y);
    }

    [Fact]
    public void APanelOpenedNearTheBottomIsPushedBackUp()
    {
        var placed = ScreenPlacement.KeptOnScreen(new Vector2(100, 700), Size, Screen, Margin);

        Assert.Equal(100, placed.X);
        Assert.Equal(Screen.Y - Size.Y - Margin, placed.Y);
    }

    [Fact]
    public void APanelOpenedInTheCornerIsPushedBackOnBothAxes()
    {
        Assert.Equal(
            new Vector2(Screen.X - Size.X - Margin, Screen.Y - Size.Y - Margin),
            ScreenPlacement.KeptOnScreen(new Vector2(990, 790), Size, Screen, Margin));
    }

    // On a screen too small to hold the panel at all, overhanging the far edge is better than
    // overhanging the near one, where the heading and the first action are the part lost.
    [Fact]
    public void APanelTallerThanTheScreenKeepsItsTopRatherThanItsBottom()
    {
        var placed = ScreenPlacement.KeptOnScreen(new Vector2(100, 100), new Vector2(200, 2000), Screen, Margin);

        Assert.Equal(Margin, placed.Y);
    }

    [Fact]
    public void APanelPlacedOffTheNearEdgeIsBroughtBackToTheMargin()
    {
        Assert.Equal(new Vector2(Margin, Margin), ScreenPlacement.KeptOnScreen(new Vector2(-50, -50), Size, Screen, Margin));
    }

    // The workbench opens in the middle of the screen rather than in a corner.
    [Fact]
    public void ACentredPanelSitsInTheMiddleOfTheScreen()
    {
        Assert.Equal(new Vector2(400, 250), ScreenPlacement.Centred(Size, Screen));
    }

    // Text set half a pixel off its grid is text the eye reads as blurred.
    [Fact]
    public void ACentredPanelLandsOnAWholePixel()
    {
        Assert.Equal(new Vector2(399, 250), ScreenPlacement.Centred(new Vector2(201, 300), Screen));
    }

    // Too big to centre is still shown from its top-left corner: the title and the first line are
    // what a panel cannot afford to lose - the same reasoning as KeptOnScreen.
    [Fact]
    public void APanelLargerThanTheScreenIsNotPushedOffItsNearEdge()
    {
        Assert.Equal(Vector2.Zero, ScreenPlacement.Centred(new Vector2(1400, 1200), Screen));
    }
}
