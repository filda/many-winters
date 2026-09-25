namespace ManyWinters.Core.World;

// The tunable numbers the simulation runs on. Configuration, not state: nothing here changes
// while a world runs and no save file stores it. Default is what the shipped game uses; tests
// shrink whichever rule they need instead of simulating thousands of ticks. Only rules a test
// overrides have an `init` setter - InspectCode's dead-code gate flags an unused one (see
// docs/development.md, "Inspections").
public sealed record SimulationRules
{
    public static SimulationRules Default { get; } = new();

    // Not configurable - it's the length of the Season enum, and SeasonAt maps ticks onto that.
    private static readonly int SeasonsPerYear = Enum.GetValues<Season>().Length;

    public long TicksPerSeason { get; init; } = 75;

    public float HungerPerTick { get; init; } = 1f;

    // What one directed attempt at the workbench costs in time. Working a thing over is not
    // free: somebody doing it is somebody not gathering, and a second try at something that
    // just failed is a second stretch of the same afternoon.
    public long TicksPerWorkAttempt { get; } = 3;

    // How much of a piece is ground away each time its edge is renewed. An edge is made by
    // taking material off, so sharpening trades mass for keenness - and both sit in the same
    // score, which is what stops a player sharpening a good edge over and over without any rule
    // forbidding it.
    public float VolumeLostPerSharpening { get; } = 0.1f;

    // The chance per tick that somebody idling with something in their hands works out how to do
    // a thing nobody showed them, before their own Curiosity multiplies it. Deliberately small:
    // idle discovery is what keeps knowledge living in people rather than in the player's head,
    // so it must be non-zero, but a band left alone should take winters to arrive at cord rather
    // than an afternoon.
    public float IdleDiscoveryChancePerTick { get; init; } = 0.002f;

    // What the player's own band works things out at, as a multiplier on the rate above. Below
    // one on purpose: a band that discovered things briskly by itself would leave the player
    // watching rather than playing, and teaching them is the game (see
    // docs/materials-and-crafting-architecture.md section 7). What they manage alone is a slow
    // floor under a player who has missed something, not a substitute for leading them. An NPC
    // band is spawned with its own number instead of this one.
    public float StartingBandCuriosity { get; } = 0.25f;

    // How much of a substance's nature somebody takes in per tick of carrying it about. Roughly
    // a season's handling for a full understanding - deliberately a little faster than exactly a
    // season, because a rate that reached certainty on the last tick of one would turn a hair of
    // floating-point drift into "they never quite learned it".
    public float MaterialUnderstandingPerTick { get; init; } = 1f / 70f;

    // What working a thing teaches about it, against the slow understanding that merely carrying
    // it brings. Certainty from a single go: they had it in their hands and saw what it did, and
    // a spoiled attempt says as much as a good one. This is the *reach* a player buys by
    // directing an attempt - somebody can be sent to try a substance nobody understands, and
    // they come back understanding it (see docs/materials-and-crafting-architecture.md
    // section 7, "How the two paths differ").
    public float UnderstandingFromWorkingIt { get; } = 1f;

    // The chance per tick that one person standing by another mentions what some substance is
    // like. Talk is cheap and constant, so this is far higher than a discovery roll: the slow
    // part of knowing things is finding them out, not telling somebody.
    public float BeliefSharingChancePerTick { get; init; } = 0.02f;

    // How far a retelling may stray from what the teller actually believes, before their skill
    // at teaching narrows it (see docs/knowledge-transmission-architecture.md section 4). Talk is
    // the lossy channel: a belief that only ever gets passed along by word drifts, while one
    // people keep checking against the stuff itself stays true. That is knowledge decaying
    // exactly when a settlement stops doing the thing, which is the point.
    public float HearsayDistortion { get; init; } = 0.15f;

    // How firmly a person holds what they were merely told, against the certainty that handling
    // a thing themselves eventually brings. Under certainty on purpose: hearsay once is talk,
    // hearsay twice - or once and then handling it - is something they will act on. It is also
    // the seam distortion will run along, when an account can arrive wrong.
    public float HearsayConfidence { get; } = 0.5f;

    // Hunger an average person dies at. Each person gets their own value around it, so this is
    // the middle of a range, not a ceiling on Needs.Hunger.
    public float MaxHunger { get; init; } = 100f;

    // Fraction of MaxHunger a person's own value may sit above or below it, so a famine thins a
    // band one by one instead of on a single tick. HungerEatThreshold and HungerSeekFoodThreshold
    // are fixed, so a low draw also shortens the warning before death; keep this well under half.
    public float MaxHungerVariation { get; init; } = 0.2f;

    // A person's own MaxHunger, drawn once from their id via SeedHash like every other per-entity
    // draw: the same on every reload without being saved, and independent of creation order.
    public float MaxHungerFor(PersonId id)
    {
        // Bit 0 of the spread is what Person.SexOf reads; skipping it keeps hunger tolerance
        // independent of sex.
        var spread = unchecked((uint)SeedHash.Avalanche(unchecked((uint)id.Seed))) >> 1;
        var fraction = ((spread / (float)(uint.MaxValue >> 1)) * 2f) - 1f;

        return MaxHunger * (1f + (fraction * MaxHungerVariation));
    }

    public long MaxLifespanYears { get; init; } = 10;

    // Below this a person leaves the food they carry alone; once they eat, EatCommand eats down
    // to zero, so meals are occasional events, not a bite per tick. Well below
    // HungerSeekFoodThreshold: someone carrying food eats long before anyone goes looking for it.
    public float HungerEatThreshold { get; init; } = 25f;

    // Where "hungry" starts for the idle AI: a hungry, empty-handed person seeks food before
    // putting a known skill to use.
    public float HungerSeekFoodThreshold { get; } = 50f;

    // A nursing mother gets hungry this much faster; the infant beside her does not get hungry
    // at all. A cost on her rather than a transfer, so the result does not depend on which of
    // the pair is processed first.
    public float NursingHungerMultiplier { get; } = 1.5f;

    // Metres per tick an infant follows its mother at (FollowTask): faster than her idle wander,
    // slower than a purposeful walk, so it trails behind but never loses her.
    public float InfantFollowSpeedPerTick { get; } = 0.25f;

    // Ceiling on what two people are worth to each other; without one a bond is just a count of
    // ticks spent in the same clearing.
    public float MaxAffection { get; } = 100f;

    // A bond grows while two people are together and fades apart, on one number. An order of
    // magnitude apart on purpose: a friendship is made faster than it is lost, so someone back
    // from a long errand still knows the band, while a person who leaves for good drifts out
    // of it.
    public float AffectionGainedPerTickTogether { get; } = 0.5f;

    public float AffectionLostPerTickApart { get; } = 0.05f;

    // Metres within which the two above count as "together". Wider than MaxInteractionDistance
    // (spending a day near somebody needs no reach) and wide enough to cover the starting camp
    // (the starting camp's crowd radius is 4m). Set too tight, no child is ever born in the
    // shipped game - a family-milestone test guards that.
    public float TogetherDistance { get; } = 5f;

    // A newborn's bond with each parent, on the same scale as every other bond. Below
    // AffectionNeededToHaveAChild so a child starts close to its parents, but nothing enforces
    // that by value - a grown child beside its mother passes the threshold on its own, which is
    // why Kinship is a structural check on the family tree.
    public float StartingAffectionWithParents { get; } = 50f;

    // Bond past which a child follows without the player asking. Reachable from nothing in a
    // couple of hundred ticks of company.
    public float AffectionNeededToHaveAChild { get; } = 60f;

    public float ConditionDecayPerTick { get; init; } = 0.05f;

    // Metres the idle AI searches for a resource to work, so nobody treks across the ~1km map
    // for one distant node. In the shipped world a match is normally much closer; this only
    // matters in sparse test worlds.
    public float IdleSearchRadius { get; } = 60f;

    // Per-tick chance that someone nearby picks up a technique just from being around a teacher,
    // as opposed to a deliberate lesson. Only ever the base technique, never the efficient one.
    // Rolled fresh each tick rather than once per pair, so the spread stays gradual and partial.
    public float CasualTeachingChancePerTick { get; } = 0.05f;

    // Higher chance for eating and teaching itself: both come naturally by watching, and no
    // casual teaching can start until somebody knows how to teach at all, so that bootstrap must
    // not be the bottleneck.
    public float CasualTeachingChancePerTickForCriticalSkills { get; } = 0.3f;

    // Metres within which a person can act on a thing (a node, a building, another person);
    // every proximity check goes through WorldState.IsWithinReach.
    public float MaxInteractionDistance { get; init; } = 2f;

    // Metres within which a person can take from a ground pile (EatFromPileCommand,
    // PickUpItemCommand). Tighter than MaxInteractionDistance: unlike a tree or a building, a
    // pile sits underfoot, and the shared reach read as picking it up from too far away.
    public float PileReachDistance { get; } = 1f;

    // Half-width of a person's footprint for collisions, in metres. Smaller than the rendered
    // sprite: it only needs to keep people from visibly overlapping.
    public float PersonCollisionRadius { get; } = 0.35f;

    // Cap on how far one tick of collision untangling may move a person, in metres, however
    // many things they overlap at once. Same order as a walking step, so a push never reads as
    // a hijacked walk order; someone deeply stuck clears over a few ticks instead.
    public float MaxCollisionPushPerTick { get; } = 1f;

    public long TicksPerYear => TicksPerSeason * SeasonsPerYear;

    public Season SeasonAt(long tick) => (Season)((tick / TicksPerSeason) % SeasonsPerYear);
}
