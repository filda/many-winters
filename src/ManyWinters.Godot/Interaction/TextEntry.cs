using Godot;

namespace ManyWinters.Godot.Interaction;

// Who the keyboard belongs to. The workbench asks the player to name a thing nobody has a word
// for yet, and while that field has the focus every key is a letter: W is a letter rather than a
// pan north, Space is a space rather than a pause. Godot routes the key events to the field on
// its own - this is for the game's own polling and for the keys Main answers before anything
// else sees them.
public static class TextEntry
{
    public static bool HasTheKeyboard(Viewport viewport) => viewport.GuiGetFocusOwner() is LineEdit or TextEdit;
}
