using Godot;

namespace ManyWinters.Godot.Logic;

// Which mouse buttons are a click on something in the world. Godot reports the wheel and the
// middle button as InputEventMouseButton presses like any other, so without asking this a scroll
// over a bush is a click on it - gathering from the very tree the player was zooming towards.
internal static class OrderButtons
{
    // Left points at a thing and means the one obvious thing to do with it; right asks what else
    // there is (see Main, ContextMenu). Nothing else gives an order.
    internal static bool Includes(MouseButton button) => button is MouseButton.Left or MouseButton.Right;
}
