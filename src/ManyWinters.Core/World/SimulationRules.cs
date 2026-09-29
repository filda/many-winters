namespace ManyWinters.Core.World;

// The tunable numbers the simulation runs on. Configuration, not state: nothing here changes
// while a world runs and no save file stores it. Default is what the shipped game uses; tests
// shrink whichever rule they need instead of simulating thousands of ticks. Only rules a test
// overrides have an `init` setter - InspectCode's dead-code gate flags an unused one (see
// docs/development.md, "Inspections").
public sealed record SimulationRules
{
    // Not configurable - it's the length of the Season enum, and SeasonAt maps ticks onto that.
    private static readonly int SeasonsPerYear = Enum.GetValues<Season>().Length;

    public static SimulationRules Default { get; } = new();

    public long TicksPerSeason { get; init; } = 75;

    // Ticks after death before a corpse's perishable contents rot away - two seasons, so a band
    // that cannot reach a carcass in a season still has one more to try before its meat is gone.
    public long CorpseDecayTicks { get; init; } = 150;

    // Ticks after a corpse decays before its bones themselves are gone (a dead, unburied Animal
    // is removed from the world - a person's bones never are, since the record of a band is its
    // graves; see docs/chronicles-and-memory-architecture.md). A year, so a season missed still
    // leaves the rest of one to find the bones.
    public long BonesLingerTicks { get; init; } = 300;

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
    // the middle of a range, not a ceiling on hunger itself.
    public float MaxHunger { get; init; } = 100f;

    // Fraction of MaxHunger a person's own value may sit above or below it, so a famine thins a
    // band one by one instead of on a single tick. HungerEatThreshold and HungerSeekFoodThreshold
    // are fixed, so a low draw also shortens the warning before death; keep this well under half.
    public float MaxHungerVariation { get; init; } = 0.2f;

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

    // Metres per tick an infant follows its mother at: faster than her idle wander, slower than
    // a purposeful walk, so it trails behind but never loses her.
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
    // every proximity check goes through this.
    public float MaxInteractionDistance { get; init; } = 2f;

    // Metres within which a person can take from a ground pile. Tighter than
    // MaxInteractionDistance: unlike a tree or a building, a pile sits underfoot, and the shared
    // reach read as picking it up from too far away.
    public float PileReachDistance { get; } = 1f;

    // Cap on how far one tick of collision untangling may move a person, in metres, however
    // many things they overlap at once. Same order as a walking step, so a push never reads as
    // a hijacked walk order; someone deeply stuck clears over a few ticks instead.
    public float MaxCollisionPushPerTick { get; } = 1f;

    // A throw's reach - deliberately beyond a deer's own FleeDistance (8m in the shipped deer),
    // so a hunter who closes the gap before being noticed can still get a throw off.
    public float HuntingRange { get; } = 10f;

    // A thrown stone in bare hands: nearly hopeless.
    public float HuntingBaseHitChance { get; init; } = 0.05f;

    // Scales a tool's best chopping score into a hit chance on top of the base above - a sharp
    // stone hafted on a stick is a spear as much as an axe until form recognition tells them
    // apart (docs/materials-and-crafting-architecture.md section 8). Picked so the shipped
    // axe-grade sharp hafted tool - a knapped wedge lashed to a stick, both practised to mastery -
    // lands around 0.35 per attempt: that tool scores ChoppingScoreOf ~= EdgeSharpness(1) *
    // Hardness(1) * sqrt(weight 2) * (1 + HaftLeverage(1) * JointStrength(0.5)) ~= 2.121, so
    // (0.35 - 0.05) / 2.121 ~= 0.14.
    public float HuntingHitChancePerToolScore { get; init; } = 0.14f;

    // The bonus from knowing efficient hunting: applied to the whole chance rather than added
    // flat, so a practised hunter is proportionally better with whatever they carry, bare hands
    // included.
    public float HuntingEfficientMultiplier { get; init; } = 1.5f;

    public long TicksPerYear => TicksPerSeason * SeasonsPerYear;
    public float AdultBaseWeight { get; } = 50f;
    public float NewbornFraction { get; } = 0.2f;
    public float ElderEndFraction { get; } = 0.85f;
    public float SkillGainPerBurial { get; } = 1f;
    public int PracticesBeforeDiscovery { get; } = 5;
    public float SkillGainPerButchering { get; } = 1f;
    public float EfficientHungerRestoredMultiplier { get; } = 1.2f;
    public float SkillGainPerMeal { get; } = 1f;

    // How far a second (or later) leftover - a fallen log next to the stump a tree leaves in its
    // spot - lands from where the tree stood, so the two don't sit exactly on top of each other.
    public double SubsequentLeftoverDistance { get; } = 1.4;
    public float EfficientHarvestAmount { get; } = 40f;
    public float BaseHarvestAmount { get; } = 20f;
    public float SkillGainPerGather { get; } = 1f;
    public float SkillGainPerHuntAttempt { get; } = 1f;

    // Nothing scales past this - even a master hunter with the best tool in the game misses one
    // throw in ten.
    public float HuntingMaxHitChance { get; } = 0.9f;
    public float? StartingCondition { get; } = 100f;

    // The speed every player-directed walk uses - a purposeful trip, not the idle AI's unhurried
    // pace. Public: TargetActions builds HuntTask/ButcherTask with this same number rather than a
    // copy of it.
    public float SpeedPerTick { get; } = 1f;
    public float SkillGainPerAttempt { get; } = 1f;
    public float RepairConditionAmount { get; } = 25f;
    public float MaxCondition { get; } = 100f;
    public float SkillGainPerLesson { get; } = 1f;

    // A teacher who knows the efficient technique reaches a little further - a lesson to a
    // small group, not a whisper.
    public float EfficientTeachingRangeMultiplier { get; } = 2f;

    // Slow enough that no single generation overwrites the naming tradition it was handed
    // (docs/Procedural Name Generation Plan.md, "Cultural Memory"); the last ~12 births (roughly
    // one generation under the default simulation rules) count for the separate NamingTrend on top.
    public float CultureDecayPerObservation { get; } = 0.98f;

    public int RecentTrendWindow { get; } = 12;

    // The plan's 50/30/20 split between long-term culture, recent trend and either parent.
    public float CultureWeight { get; } = 0.5f;
    public float TrendWeight { get; } = 0.3f;
    public float ParentWeight { get; } = 0.2f;
    public float ApproachFractionOfReach { get; } = 0.6f;

    // Public: the idle AI reuses this exact number for the autonomous HuntTask and ButcherTask it
    // installs, so the three autonomous foraging tasks all walk at the same unhurried pace; tests
    // reuse it too, to build an idle-speed task without duplicating the number.
    public float GatherSpeedPerTick { get; } = 0.3f;

    // How far a band's camp reaches: people wander within it and search for food from its centre.
    // Public: whoever founds a camp - the map at load, the debug spawn button, the simulation
    // runner - uses the same one.
    public float CampHomeRadius { get; } = 8f;

    public float IdleSpeedPerTick { get; } = 0.15f;

    // A pause between wander legs (and before the first), or idle reads as restless constant
    // walking. The ceiling is public because startup runs the world that long before the player
    // sees it, so the band is already on the move.
    public int MinPauseTicks { get; } = 3;
    public int MaxPauseTicks { get; } = 10;
    public float SkillGainPerBind { get; } = 1f;

    // Nothing was done to it, so nothing was gained or spoiled: a raw stick is exactly as sound as wood is.
    public float UnworkedQuality { get; } = 1f;

    // Smaller than the sky clouds but big enough to read as a bank of cloud, not a row of
    // bushes. The wide size spread keeps the layout from looking stamped out.
    public float MinWorldSize { get; } = 7f;
    public float MaxWorldSize { get; } = 20f;

    // Where the sprite's centre sits relative to the terrain, as a fraction of its height, picked
    // per cloud. The cloud art occupies roughly the middle 27%-72% of its canvas (see
    // art/generate_sprites.py), so a centre at ground level shows the upper half of
    // the puff rising out of the terrain; the top of the range lifts it clear. Standing the canvas
    // bottom on the ground (+0.5) floated the puff like a shrub, and one shared height read as a
    // row of puffs stuck into the terrain.
    public float MinCenterAboveGroundFraction { get; } = -0.05f;
    public float MaxCenterAboveGroundFraction { get; } = 0.3f;

    // Mean centre-to-centre spacing the scatter aims for; actual gaps vary (CloudSpotScatter).
    public float MeanSpacingMeters { get; } = 5f;

    // Fixed for reproducibility; distinct from the other cloud layer's own seed so the two
    // layers do not share a pattern.
    public int GroundCloudSeed { get; } = 23;

    // Two spots must be at least this fraction of their combined size apart. Cloud art spans
    // ~90% of its canvas, so 0.45 would be edge-to-edge; well under that lets neighbours overlap
    // by more than half a width, as puffs in a bank of low cloud do. The gap, not the requested
    // count, is what caps the density.
    public float MinGapFactor { get; } = 0.2f;

    // Random candidates tried per spot wanted: enough headroom for rejection sampling to
    // saturate the space without looping long over a full map.
    public int AttemptsPerTargetSpot { get; } = 6;

    // Wavelength of the spatial grain mixed into each spot's roll, in metres - the size of
    // the clumps and gaps the thinning cover breaks into.
    public float ClumpScaleMeters { get; } = 22f;

    // How much of the roll is spatial grain rather than independent chance. An independent
    // roll thins the cover as an even sprinkle, which still reads as regular; shared grain
    // makes whole patches drop out together, so the cover tears into clumps and openings.
    public float ClumpWeight { get; } = 0.6f;

    // A person's own MaxHunger, drawn once from their id via SeedHash like every other per-entity
    // draw: the same on every reload without being saved, and independent of creation order.
    public float MaxHungerFor(CreatureId id)
    {
        // Bit 0 of the spread is what determines a creature's sex; skipping it keeps hunger
        // tolerance independent of sex.
        var spread = unchecked((uint)SeedHash.Avalanche(unchecked((uint)id.Seed))) >> 1;
        var fraction = ((spread / (float)(uint.MaxValue >> 1)) * 2f) - 1f;

        return MaxHunger * (1f + (fraction * MaxHungerVariation));
    }

    public Season SeasonAt(long tick) => (Season)((tick / TicksPerSeason) % SeasonsPerYear);
}
