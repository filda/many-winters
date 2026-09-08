namespace ManyWinters.Core.World;

// Rejection sampling for dropping something into open ground: draw a random candidate, keep
// it if that spot is free, otherwise draw another. Ground that is genuinely full - fifteen
// people in a four-metre disk, a camp already packed with huts - would loop forever, so the
// search gives up after a fixed number of attempts and hands back the last candidate it drew
// rather than nothing: two things overlapping is a cosmetic problem, having nowhere to put
// someone at all is not.
public static class FreePositionSearch
{
    // Draws at most maxAttempts + 1 candidates: the extra one is what gives up returns, which
    // is why it is never itself tested for a free spot.
    public static Position Find(Func<Position> nextCandidate, Func<Position, bool> isFree, int maxAttempts)
    {
        var candidate = nextCandidate();

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (isFree(candidate))
            {
                return candidate;
            }

            candidate = nextCandidate();
        }

        return candidate;
    }
}
