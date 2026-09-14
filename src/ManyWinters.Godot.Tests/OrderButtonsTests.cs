using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// Which mouse buttons are a click on something in the world. Godot reports a wheel turn as a
// pressed mouse button like any other, so getting this wrong means a scroll over a bush gathers
// from the tree the player was only zooming towards.
public class OrderButtonsTests
{
    [Fact]
    public void TheLeftButtonGivesAnOrder()
    {
        Assert.True(OrderButtons.Includes(MouseButton.Left));
    }

    [Fact]
    public void TheRightButtonGivesAnOrder()
    {
        Assert.True(OrderButtons.Includes(MouseButton.Right));
    }

    [Theory]
    [InlineData(MouseButton.WheelUp)]
    [InlineData(MouseButton.WheelDown)]
    [InlineData(MouseButton.WheelLeft)]
    [InlineData(MouseButton.WheelRight)]
    public void AWheelTurnIsNotAnOrder(MouseButton button)
    {
        Assert.False(OrderButtons.Includes(button));
    }

    [Theory]
    [InlineData(MouseButton.Middle)]
    [InlineData(MouseButton.Xbutton1)]
    [InlineData(MouseButton.Xbutton2)]
    [InlineData(MouseButton.None)]
    public void NoOtherButtonGivesAnOrder(MouseButton button)
    {
        Assert.False(OrderButtons.Includes(button));
    }
}
