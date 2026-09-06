namespace ManyWinters.Core.World;

public readonly record struct Position(double X, double Y)
{
    // A destination short of `to` by `standoffDistance`, on the straight line back toward
    // `from` - so whoever walks there ends up standing next to the thing rather than on top
    // of (and, on screen, visually covering) it. Shared by the player-directed gather-walk
    // (Main) and the autonomous one (GatherTask), so both approaches read the same. Already
    // within the standoff - including standing exactly on `to`, where there's no direction to
    // back off along - means there's nowhere closer worth walking to, so `from` itself.
    public static Position Approach(Position from, Position to, double standoffDistance)
    {
        var dx = from.X - to.X;
        var dy = from.Y - to.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance <= standoffDistance)
        {
            return from;
        }

        var ratio = standoffDistance / distance;
        return new Position(to.X + (dx * ratio), to.Y + (dy * ratio));
    }
}
