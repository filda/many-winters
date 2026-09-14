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
}
