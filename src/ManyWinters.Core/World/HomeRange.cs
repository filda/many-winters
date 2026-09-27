namespace ManyWinters.Core.World;

// "Here live ..." - a shared anchor a herd wanders around instead of each member's own standing
// position, or a lone creature's own patch. The anchor drifts a fixed distance once per season,
// in a direction drawn from the range's own id and the season index, so the ground a herd calls
// home shifts slowly and reproducibly and never jumps mid-season.
public sealed class HomeRange
{
    public HomeRangeId Id { get; init; } = HomeRangeId.New();

    public Position Anchor { get; private set; }

    public required float Radius { get; init; }

    // From the species' own herd definition, kept here rather than in the shared rules so a
    // second species can drift at its own rate. 0 for a home range that never moves.
    public required float DriftMetresPerSeason { get; init; }

    // Null until the first Advance call, which only ever establishes which season "now" is - it
    // never moves the anchor, or a freshly spawned herd would jump the instant the world's first
    // tick ran. Every later call that crosses into a new season moves it exactly once.
    private long? _lastAdvancedSeason;

    public HomeRange(Position anchor) => Anchor = anchor;

    // Idempotent within a season: called every tick, but only moves the anchor on the tick that
    // crosses into a new season, so the drift is exactly DriftMetresPerSeason per season
    // regardless of how often this is called.
    public void Advance(long tick, long ticksPerSeason)
    {
        if (ticksPerSeason <= 0)
        {
            return;
        }

        var season = tick / ticksPerSeason;
        if (season == _lastAdvancedSeason)
        {
            return;
        }

        var establishingBaseline = _lastAdvancedSeason is null;
        _lastAdvancedSeason = season;
        if (establishingBaseline || DriftMetresPerSeason <= 0f)
        {
            return;
        }

        // Spread by SeedHash so a home range's own id and the season index don't correlate with
        // a neighbouring one's.
        var mixed = unchecked((uint)(Id.Seed * 73856093) ^ ((uint)season * 19349663u));
        var angle = new Random(SeedHash.Avalanche(mixed)).NextDouble() * Math.Tau;

        Anchor = new Position(
            Anchor.X + (Math.Cos(angle) * DriftMetresPerSeason),
            Anchor.Y + (Math.Sin(angle) * DriftMetresPerSeason));
    }
}
