namespace ManyWinters.Core.World;

// The tunable numbers the simulation runs on - how long a season is, how fast hunger climbs,
// how close two things have to be to interact. Configuration, not state: nothing here changes
// while a world runs, and SaveGameService never stores it (it's handed back in on load, same as
// the catalogs). Default is what the shipped game uses; a test world shrinks whichever of these
// it needs (a one-tick season, a one-year lifespan) instead of simulating thousands of ticks.
// Only the rules some test actually overrides have an `init` setter - the rest are get-only
// until one does (InspectCode's dead-code gate treats an unused setter as dead, see
// docs/development.md "Inspections"); turning one into `init` when a test needs it is the
// whole change.
public sealed record SimulationRules
{
    public static SimulationRules Default { get; } = new();

    // Not configurable - it's the length of the Season enum, and SeasonAt maps ticks onto that.
    private static readonly int SeasonsPerYear = Enum.GetValues<Season>().Length;

    public long TicksPerSeason { get; init; } = 75;

    public float HungerPerTick { get; init; } = 1f;

    public float MaxHunger { get; init; } = 100f;

    public long MaxLifespanYears { get; init; } = 10;

    // "Idle" means "put whatever skill this person already has to use, or go find food if
    // hungry and empty-handed" (see WorldState.DecideIdleTask) - this is where "hungry" starts.
    // Hunger takes priority over an already-known skill: a hungry woodcutter with no food on
    // hand goes looking for something to eat before going back to chopping wood.
    public float HungerSeekFoodThreshold { get; } = 50f;

    public float ConditionDecayPerTick { get; init; } = 0.05f;

    // Nobody autonomously treks halfway across a real ~1km terrain patch (see
    // MapLoader.ScatterDecorations) for one distant resource - a search this wide only ever
    // matters in a sparse/test world; the real game's decoration density means a genuinely
    // reachable match is normally well within it anyway.
    public float IdleSearchRadius { get; } = 60f;

    // A lesson someone actually sat down to give (TeachFromSelectedPersonTo's right-click, a
    // deliberate full transfer) is a different thing from picking something up just from being
    // around someone - "tichá pošta": only ever the base technique, never the harder-earned
    // efficient one riding on top of it, and even that isn't guaranteed on any given tick
    // (rolled fresh each tick, not a permanent per-pair verdict - once someone picks something
    // up they can just as easily become a further relay for it, so a low per-tick chance,
    // not a one-time coin flip, is what actually keeps the spread gradual and partial).
    public float CasualTeachingChancePerTick { get; } = 0.05f;

    // Eating (and teaching itself, the one thing every other casual lesson depends on - see
    // WorldState.AutoTeachNearbyPeople) are different from a specialised craft skill: everyone's
    // watched someone else eat and copying it comes far more naturally than picking up
    // woodcutting from proximity alone, and the whole casual-teaching chain can't even start in
    // a population until at least one person knows how to teach at all. A much higher chance
    // for these two specifically keeps that bootstrap from being the bottleneck it would
    // otherwise be.
    public float CasualTeachingChancePerTickForCriticalSkills { get; } = 0.3f;

    // How close a person has to be to a thing (a resource node, a building, another person)
    // to act on it - every command that needs proximity checks against this one number, via
    // WorldState.IsWithinReach.
    public float MaxInteractionDistance { get; init; } = 2f;

    // A person's own footprint half-width for collision purposes - deliberately smaller than
    // PersonView's rendered sprite, this only needs to keep people from visibly overlapping,
    // not match their exact silhouette.
    public float PersonCollisionRadius { get; } = 0.35f;

    // Caps how far a single tick's worth of untangling can shove someone, regardless of how
    // many things they happen to be overlapping at once (a person standing in a dense thicket
    // could otherwise be touching several trees' trunks simultaneously, and summing every one
    // of those separations unclamped could shove them noticeably farther in one tick than
    // their own MoveTask/IdleTask step - reading as the person's walk order having been
    // silently hijacked toward some unrelated direction rather than a gentle nudge out of the
    // way). Same order of magnitude as MoveCommand's own walking speed, so being untangled
    // never outpaces an intentional step; a person still deeply stuck simply takes a couple of
    // extra ticks to fully clear, spread out rather than dumped in one lurch.
    public float MaxCollisionPushPerTick { get; } = 1f;

    public long TicksPerYear => TicksPerSeason * SeasonsPerYear;

    public Season SeasonAt(long tick) => (Season)((tick / TicksPerSeason) % SeasonsPerYear);
}
