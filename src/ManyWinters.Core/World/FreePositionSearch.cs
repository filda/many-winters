namespace ManyWinters.Core.World;

// Rejection sampling for dropping something onto open ground: draw a candidate, keep it if
// free, else draw another. Genuinely full ground would loop forever, so after a fixed number
// of attempts it hands back the last candidate rather than nothing: overlap is cosmetic,
// having nowhere to put someone is not.
public static class FreePositionSearch
{
    // Draws at most maxAttempts + 1 candidates; the extra one is what giving up returns,
    // untested.
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
