using Godot;

namespace ManyWinters.Godot.Logic;

// Where a panel that was placed by the cursor ends up once the screen has had its say. A menu
// opened near the right edge would otherwise run off it, taking its own actions with it (see
// ContextMenu).
internal static class ScreenPlacement
{
    // Pushed left and up only as far as it takes, never past the margin on the near side: on a
    // screen too small to hold the panel at all, overhanging the far edge is better than
    // overhanging the near one, where the heading and the first action would be the part lost.
    internal static Vector2 KeptOnScreen(Vector2 position, Vector2 size, Vector2 screen, float margin) =>
        new(
            KeptOnAxis(position.X, size.X, screen.X, margin),
            KeptOnAxis(position.Y, size.Y, screen.Y, margin));

    private static float KeptOnAxis(float position, float size, float screen, float margin) =>
        Mathf.Max(margin, Mathf.Min(position, screen - size - margin));
}
