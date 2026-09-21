namespace ManyWinters.Core.World;

public readonly record struct Position(double X, double Y)
{
    // A destination `standoffDistance` short of `to`, on the line back toward `from`, so the
    // walker stands next to the thing rather than on top of it. Shared by the player-directed
    // gather walk and the autonomous one. Already within the standoff (including exactly on
    // `to`, where there is no direction) returns `from`.
    public static Position Approach(Position from, Position to, double standoffDistance)
    {
        var dx = from.X - to.X;
        var dy = from.Y - to.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        // Stryker disable once Equality: at exactly the standoff the other branch returns `from` as well
        if (distance <= standoffDistance)
        {
            return from;
        }

        var ratio = standoffDistance / distance;
        return new Position(to.X + (dx * ratio), to.Y + (dy * ratio));
    }
}
