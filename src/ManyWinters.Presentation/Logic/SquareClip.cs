using Godot;

namespace ManyWinters.Presentation.Logic;

// Cuts a line segment to the terrain patch, a square centred on the origin. A river mapped
// past the patch's edge has no ground under it there, and drawn anyway it hangs in the void.
internal static class SquareClip
{
    // Liang-Barsky: the segment is a + t(b - a) for t in [0, 1], narrowed by each of the four
    // edges in turn. Null when nothing of it lies inside.
    internal static (Vector2 A, Vector2 B)? Clip(Vector2 a, Vector2 b, float half)
    {
        var delta = b - a;
        var enter = 0f;
        var leave = 1f;

        bool Narrow(float towardEdge, float room)
        {
            if (towardEdge == 0f)
            {
                return room >= 0f;
            }

            var t = room / towardEdge;
            if (towardEdge < 0f)
            {
                enter = Math.Max(enter, t);
            }
            else
            {
                leave = Math.Min(leave, t);
            }

            return enter <= leave;
        }

        var inside = Narrow(-delta.X, a.X + half)
            && Narrow(delta.X, half - a.X)
            && Narrow(-delta.Y, a.Y + half)
            && Narrow(delta.Y, half - a.Y);

        return inside ? (a + (delta * enter), a + (delta * leave)) : null;
    }
}
