using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

// HuntCommand (docs/todo/fauna-plan.md, phase 3): always a Person at a living Animal, gated only
// on knowledge - a bare-handed throw is allowed at HuntingBaseHitChance, so there is no
// MissingTool blocker the way FellCommand has one.
public class HuntCommandTests
{
    private static Person Hunter(WorldState world, Position position, bool knowsHunting = true, bool knowsEfficientHunting = false) =>
        Hunter(world, CreatureId.New(), position, knowsHunting, knowsEfficientHunting);

    // With a chosen id - AHitKillsThePreyWithHuntedAsTheCauseAndFillsItsCarcass pins the roll on it.
    private static Person Hunter(WorldState world, CreatureId id, Position position, bool knowsHunting = true, bool knowsEfficientHunting = false)
    {
        var person = world.SpawnPerson(id, "Ava", position, initialAgeTicks: TestCatalogs.AdultAgeTicks);
        if (knowsHunting)
        {
            person.KnownTechniques.Add(TestCatalogs.BasicHunting);
        }

        if (knowsEfficientHunting)
        {
            person.KnownTechniques.Add(TestCatalogs.EfficientHunting);
        }

        return person;
    }

    private static Animal Deer(WorldState world, Position position) =>
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position);

    private static Animal Deer(WorldState world, CreatureId id, Position position) =>
        DeerWithId(world, id, position);

    // Spawns with a chosen id - the determinism tests below pin the roll on it.
    private static Animal DeerWithId(WorldState world, CreatureId id, Position position)
    {
        var home = new HomeRange(position) { Radius = 10f, DriftMetresPerSeason = 0f };
        world.Execute(new SpawnAnimalCommand(id, TestCatalogs.DeerSpeciesId, position, home, Creature.SexOf(id), world.Clock.CurrentTick));
        return world.Animals[^1];
    }

    [Fact]
    public void NothingBlocksAKnowledgeableHunterWithLivingPreyWithinRange()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var hunter = Hunter(world, new Position(0, 0));
        var deer = Deer(world, new Position(0, 0));

        Assert.Equal(ActionBlocker.None, new HuntCommand(hunter, deer).Blocker(world));
    }

    [Fact]
    public void HuntingRequiresTheHunterToBeAlive()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var hunter = Hunter(world, new Position(0, 0));
        hunter.IsAlive = false;
        var deer = Deer(world, new Position(0, 0));

        Assert.Equal(ActionBlocker.ActorIsDead, new HuntCommand(hunter, deer).Blocker(world));
    }

    [Fact]
    public void ADeadDeerBlocksHuntingAsTargetIsGone()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var hunter = Hunter(world, new Position(0, 0));
        var deer = Deer(world, new Position(0, 0));
        deer.IsAlive = false;

        Assert.Equal(ActionBlocker.TargetIsGone, new HuntCommand(hunter, deer).Blocker(world));
    }

    [Fact]
    public void ADeerBeyondHuntingRangeBlocksHuntingAsTooFar()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var hunter = Hunter(world, new Position(0, 0));
        var deer = Deer(world, new Position(world.Configuration.Rules.HuntingRange + 1, 0));

        Assert.Equal(ActionBlocker.TooFar, new HuntCommand(hunter, deer).Blocker(world));
    }

    [Fact]
    public void ADeerAtExactlyHuntingRangeStillWorks()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var hunter = Hunter(world, new Position(0, 0));
        var deer = Deer(world, new Position(world.Configuration.Rules.HuntingRange, 0));

        Assert.Equal(ActionBlocker.None, new HuntCommand(hunter, deer).Blocker(world));
    }

    [Fact]
    public void HuntingWithoutHavingLearnedItDoesNothing()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var hunter = Hunter(world, new Position(0, 0), knowsHunting: false);
        var deer = Deer(world, new Position(0, 0));

        var command = new HuntCommand(hunter, deer);
        Assert.Equal(ActionBlocker.NotLearned, command.Blocker(world));
        world.Execute(command);

        Assert.True(deer.IsAlive);
        Assert.Equal(0f, hunter.Skills.Get(HuntCommand.Skill));
    }

    // Knowledge is asked last, and range is asked before it - a hunter who has never been shown
    // how, standing beyond HuntingRange, hears about the distance, not the missing lesson (see
    // ActionBlocker.NotLearned).
    [Fact]
    public void TooFarIsBlamedBeforeNeverHavingLearnedToHunt()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var hunter = Hunter(world, new Position(0, 0), knowsHunting: false);
        var deer = Deer(world, new Position(world.Configuration.Rules.HuntingRange + 1, 0));

        Assert.Equal(ActionBlocker.TooFar, new HuntCommand(hunter, deer).Blocker(world));
    }

    [Fact]
    public void HuntingIsPractice()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var hunter = Hunter(world, new Position(0, 0));
        var deer = Deer(world, new Position(0, 0));

        world.Execute(new HuntCommand(hunter, deer));

        Assert.True(hunter.Skills.Get(HuntCommand.Skill) > 0f);
    }

    [Fact]
    public void AHitKillsThePreyWithHuntedAsTheCauseAndFillsItsCarcass()
    {
        // The chance is capped at 0.9 (HuntCommand.MaxHitChance) however generous the rules -
        // there is no configuration that makes a hit literal certainty - so this pins the same
        // fixed hunter/prey ids TheSameHunterPreyPairAndTickAlwaysRollsTheSameOutcome relies on
        // for determinism, chosen because they roll a hit at tick 0 against this chance.
        var rules = SimulationRules.Default with { HuntingBaseHitChance = 1f };
        var world = new WorldState(TestCatalogs.CreateConfigurationWithDeer() with { Rules = rules });
        var hunter = Hunter(world, CreatureId.New(new Random(1)), new Position(0, 0));
        var deer = Deer(world, CreatureId.New(new Random(2)), new Position(0, 0));

        world.Execute(new HuntCommand(hunter, deer));

        Assert.False(deer.IsAlive);
        Assert.Equal(DeathCause.Hunted, deer.CauseOfDeath);
        Assert.Equal(world.Clock.CurrentTick, deer.DeathTick);
        Assert.Equal(TestCatalogs.DeerCarcassMeat, deer.Inventory.Get(TestCatalogs.MeatItem));
        Assert.Equal(TestCatalogs.DeerCarcassHide, deer.Inventory.Get(TestCatalogs.HideItem));
        Assert.Equal(TestCatalogs.DeerCarcassBone, deer.Inventory.Get(TestCatalogs.BoneItem));
        Assert.Equal(TestCatalogs.DeerCarcassSinew, deer.Inventory.Get(TestCatalogs.SinewItem));
    }

    // A miss is seen: the deer flees the hunter even though HuntingRange (10m) lies well beyond
    // its own FleeDistance (8m) - it would never have noticed on its own from here.
    [Fact]
    public void AMissSendsThePreyFleeingEvenFromBeyondItsOwnFleeDistance()
    {
        var rules = SimulationRules.Default with { HuntingBaseHitChance = 0f, HuntingHitChancePerToolScore = 0f };
        var world = new WorldState(TestCatalogs.CreateConfigurationWithDeer() with { Rules = rules });
        var hunter = Hunter(world, new Position(0, 0));
        var deer = Deer(world, new Position(TestCatalogs.DeerFleeDistance + 1, 0));

        world.Execute(new HuntCommand(hunter, deer));

        Assert.True(deer.IsAlive);
        var flee = Assert.IsType<FleeTask>(deer.Tasks.Current);
        Assert.Same(hunter, flee.Threat);
    }

    [Fact]
    public void FiveHuntsDiscoverTheEfficientTechnique()
    {
        var rules = SimulationRules.Default with { HuntingBaseHitChance = 1f };
        var world = new WorldState(TestCatalogs.CreateConfigurationWithDeer() with { Rules = rules });
        var hunter = Hunter(world, new Position(0, 0));

        for (var i = 0; i < 5; i++)
        {
            var deer = Deer(world, new Position(0, 0));
            world.Execute(new HuntCommand(hunter, deer));
        }

        Assert.Contains(TestCatalogs.EfficientHunting, hunter.KnownTechniques);
    }

    [Fact]
    public void FourHuntsFallShortOfDiscoveringTheEfficientTechnique()
    {
        var rules = SimulationRules.Default with { HuntingBaseHitChance = 1f };
        var world = new WorldState(TestCatalogs.CreateConfigurationWithDeer() with { Rules = rules });
        var hunter = Hunter(world, new Position(0, 0));

        for (var i = 0; i < 4; i++)
        {
            var deer = Deer(world, new Position(0, 0));
            world.Execute(new HuntCommand(hunter, deer));
        }

        Assert.DoesNotContain(TestCatalogs.EfficientHunting, hunter.KnownTechniques);
    }

    // Same hunter, same prey, same tick, on two entirely separate worlds: the roll comes from
    // SeedHash over those three facts alone, not from a shared Random or construction order.
    [Fact]
    public void TheSameHunterPreyPairAndTickAlwaysRollsTheSameOutcome()
    {
        var hunterId = CreatureId.New(new Random(1));
        var preyId = CreatureId.New(new Random(2));

        bool RunOnce()
        {
            var world = TestCatalogs.CreateWorldWithDeer();
            var hunter = world.SpawnPerson(hunterId, "Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
            hunter.KnownTechniques.Add(TestCatalogs.BasicHunting);
            var deer = DeerWithId(world, preyId, new Position(0, 0));

            world.Execute(new HuntCommand(hunter, deer));
            return deer.IsAlive;
        }

        Assert.Equal(RunOnce(), RunOnce());
    }

    // The same pair rolls differently at different ticks - the tick is part of the mix, not
    // merely a time the roll happens to run at.
    [Fact]
    public void ADifferentTickCanRollADifferentOutcomeForTheSamePair()
    {
        var rules = SimulationRules.Default with { HuntingBaseHitChance = 0.5f, HuntingHitChancePerToolScore = 0f };
        var hunterId = CreatureId.New(new Random(3));
        var preyId = CreatureId.New(new Random(4));

        bool RunAtTick(long tick)
        {
            var world = new WorldState(TestCatalogs.CreateConfigurationWithDeer() with { Rules = rules });
            world.Advance(tick);
            var hunter = world.SpawnPerson(hunterId, "Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
            hunter.KnownTechniques.Add(TestCatalogs.BasicHunting);
            var deer = DeerWithId(world, preyId, new Position(0, 0));

            world.Execute(new HuntCommand(hunter, deer));
            return deer.IsAlive;
        }

        var outcomes = Enumerable.Range(0, 40).Select(tick => RunAtTick(tick)).ToList();

        Assert.Contains(true, outcomes);
        Assert.Contains(false, outcomes);
    }

    // The chance arithmetic (SimulationRules.HuntingHitChancePerToolScore): rather than pin one
    // seed that happens to land where expected, this runs many independent (hunter, prey) pairs
    // at the same tick - deterministic (SeedHash, not real randomness) and reproducible every
    // run - and checks the observed hit rate against what the formula itself predicts.
    private static float ObservedHitRate(WorldState world, bool giveAxe, bool efficient, int trials)
    {
        var rng = new Random(12345);
        var hits = 0;

        for (var i = 0; i < trials; i++)
        {
            var hunter = world.SpawnPerson(CreatureId.New(rng), $"Hunter{i}", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
            hunter.KnownTechniques.Add(TestCatalogs.BasicHunting);
            if (efficient)
            {
                hunter.KnownTechniques.Add(TestCatalogs.EfficientHunting);
            }

            if (giveAxe)
            {
                hunter.Inventory.AddAssembly(TestCatalogs.CreateTestAxe(world));
            }

            var deer = DeerWithId(world, CreatureId.New(rng), new Position(0, 0));

            world.Execute(new HuntCommand(hunter, deer));
            if (!deer.IsAlive)
            {
                hits++;
            }
        }

        return (float)hits / trials;
    }

    [Fact]
    public void BareHandsHitRateMatchesTheBaseHitChance()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var expected = world.Configuration.Rules.HuntingBaseHitChance;

        var observed = ObservedHitRate(world, giveAxe: false, efficient: false, trials: 2000);

        Assert.InRange(observed, expected - 0.045f, expected + 0.045f);
    }

    // The shipped axe-grade sharp hafted tool lands the hit rate SimulationRules.
    // HuntingHitChancePerToolScore was picked for - see its own doc comment for the arithmetic.
    [Fact]
    public void TheShippedAxeGradeToolLandsAroundThePickedHitRate()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var rules = world.Configuration.Rules;
        var toolScore = world.Configuration.ItemCatalog.ChoppingScoreOf(TestCatalogs.CreateTestAxe(world));
        var expected = Math.Min(rules.HuntingBaseHitChance + (toolScore * rules.HuntingHitChancePerToolScore), 0.9f);

        var observed = ObservedHitRate(world, giveAxe: true, efficient: false, trials: 2000);

        Assert.InRange(observed, expected - 0.045f, expected + 0.045f);
        // The arithmetic the coefficient was picked around (see SimulationRules' own comment):
        // bare hands 0.05, the shipped tool lands near 0.35.
        Assert.InRange(expected, 0.3f, 0.4f);
    }

    [Fact]
    public void EfficientHuntingMultipliesTheWholeChance()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var rules = world.Configuration.Rules;
        var toolScore = world.Configuration.ItemCatalog.ChoppingScoreOf(TestCatalogs.CreateTestAxe(world));
        var expected = Math.Min((rules.HuntingBaseHitChance + (toolScore * rules.HuntingHitChancePerToolScore)) * rules.HuntingEfficientMultiplier, 0.9f);

        var observed = ObservedHitRate(world, giveAxe: true, efficient: true, trials: 2000);

        Assert.InRange(observed, expected - 0.045f, expected + 0.045f);
    }

    [Fact]
    public void TheChanceNeverExceedsTheCap()
    {
        // An absurdly generous coefficient, so the raw sum would be far above 1 without the cap.
        var rules = SimulationRules.Default with { HuntingHitChancePerToolScore = 10f, HuntingEfficientMultiplier = 3f };
        var world = new WorldState(TestCatalogs.CreateConfigurationWithDeer() with { Rules = rules });

        var observed = ObservedHitRate(world, giveAxe: true, efficient: true, trials: 2000);

        Assert.InRange(observed, 0.85f, 0.95f);
    }
}
