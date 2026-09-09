using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// Everything about an infant that Advance decides: who feeds it, what it does with its day,
// and what happens the moment its mother is no longer there to do either.
public class WorldStateNursingTests
{
    private static long AdultAgeTicks(WorldState world) => world.Configuration.Rules.TicksPerYear * LifeStages.AdultAgeYears;

    private static Person SpawnMother(WorldState world, Position position) =>
        world.SpawnPerson("Sela", position, initialAgeTicks: AdultAgeTicks(world), sex: Sex.Female);

    private static Person SpawnInfant(WorldState world, Person mother, Position position, long ageTicks = 0) =>
        world.SpawnPerson("Bran", position, initialAgeTicks: ageTicks, mother: mother);

    // The game itself only ever steps one tick at a time (Main.cs), and age is read off the
    // clock, which Advance moves before the loop runs - so a multi-tick call would age
    // everyone to the end of it on the very first tick.
    private static void AdvanceTickByTick(WorldState world, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Advance(1);
        }
    }

    [Fact]
    public void AnInfantAtItsMothersSideDoesNotGetHungry()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var infant = SpawnInfant(world, mother, new Position(0, 0));

        AdvanceTickByTick(world, 20);

        Assert.Equal(0f, infant.Needs.Hunger);
        Assert.True(mother.Needs.Hunger > 0f);
    }

    [Fact]
    public void AnInfantWhoseMotherHasDiedGetsHungryLikeAnybodyElse()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var infant = SpawnInfant(world, mother, new Position(0, 0));
        mother.IsAlive = false;

        AdvanceTickByTick(world, 20);

        Assert.True(infant.Needs.Hunger > 0f);
    }

    [Fact]
    public void AnInfantLeftOutOfReachGetsHungry()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var infant = SpawnInfant(world, mother, new Position(50, 0));

        world.Advance(1);

        Assert.True(infant.Needs.Hunger > 0f);
    }

    [Fact]
    public void AnOrphanedInfantEventuallyStarves()
    {
        // The stake the whole arrangement rests on: nothing else in the world will feed a
        // child whose mother is gone.
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = SpawnMother(world, new Position(0, 0));
        var infant = SpawnInfant(world, mother, new Position(0, 0));
        mother.IsAlive = false;

        AdvanceTickByTick(world, (int)rules.MaxHunger + 1);

        Assert.False(infant.IsAlive);
        Assert.Equal(DeathCause.Hunger, infant.CauseOfDeath);
    }

    [Fact]
    public void ANursingMotherGetsHungrierThanOneWhoIsNotFeedingAnybody()
    {
        var world = TestCatalogs.CreateWorld();
        var nursing = SpawnMother(world, new Position(0, 0));
        var childless = world.SpawnPerson("Tora", new Position(20, 0), initialAgeTicks: AdultAgeTicks(world));
        SpawnInfant(world, nursing, new Position(0, 0));

        AdvanceTickByTick(world, 10);

        Assert.True(nursing.Needs.Hunger > childless.Needs.Hunger);
    }

    [Fact]
    public void FeedingStopsAtWeaningAndTheChildStartsGettingHungryOnItsOwn()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = SpawnMother(world, new Position(0, 0));
        var child = SpawnInfant(world, mother, new Position(0, 0), ageTicks: (rules.TicksPerYear * LifeStages.WeaningAgeYears) - 1);

        world.Advance(1);

        Assert.Equal(LifeStage.Child, world.LifeStageOf(child));
        Assert.True(child.Needs.Hunger > 0f);
    }

    [Fact]
    public void NursingInfantOfFindsTheChildBeingFed()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var infant = SpawnInfant(world, mother, new Position(0, 0));

        Assert.Same(infant, world.NursingInfantOf(mother));
        Assert.True(world.IsBeingNursed(infant));
    }

    [Fact]
    public void NursingInfantOfFindsNobodyForSomeoneWithNoChildren()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));

        Assert.Null(world.NursingInfantOf(mother));
    }

    [Fact]
    public void NursingInfantOfIgnoresAChildWhoIsAlreadyWeaned()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = SpawnMother(world, new Position(0, 0));
        SpawnInfant(world, mother, new Position(0, 0), ageTicks: rules.TicksPerYear * LifeStages.WeaningAgeYears);

        Assert.Null(world.NursingInfantOf(mother));
    }

    [Fact]
    public void NursingInfantOfIgnoresADeadChild()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var infant = SpawnInfant(world, mother, new Position(0, 0));
        infant.IsAlive = false;

        Assert.Null(world.NursingInfantOf(mother));
    }

    [Fact]
    public void NursingInfantOfIgnoresSomebodyElsesChild()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var otherWoman = world.SpawnPerson("Tora", new Position(0, 0), initialAgeTicks: AdultAgeTicks(world));
        SpawnInfant(world, otherWoman, new Position(0, 0));

        Assert.Null(world.NursingInfantOf(mother));
    }

    [Fact]
    public void SomebodyWithNoRecordedMotherIsNeverNursed()
    {
        // Person.Unknown is long dead (see its own doc comment), so this falls out of the
        // living-mother check rather than needing a case of its own.
        var world = TestCatalogs.CreateWorld();
        var foundling = world.SpawnPerson("Bran", new Position(0, 0));

        Assert.False(world.IsBeingNursed(foundling));
    }

    [Fact]
    public void AnInfantKeepsUpWithItsMotherRatherThanForagingOrWandering()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var infant = SpawnInfant(world, mother, new Position(0, 0));

        world.Advance(1);

        var follow = Assert.IsType<FollowTask>(infant.Tasks.Current);
        Assert.Same(mother, follow.Target);
    }

    [Fact]
    public void TheSameFollowTaskIsKeptFromTickToTick()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var infant = SpawnInfant(world, mother, new Position(0, 0));
        world.Advance(1);
        var first = infant.Tasks.Current;

        world.Advance(1);

        Assert.Same(first, infant.Tasks.Current);
    }

    [Fact]
    public void AWeanedChildStopsFollowingItsMotherAround()
    {
        // FollowTask never completes, so this only works because Advance reconsiders it - see
        // ShouldReconsiderIdleTask.
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = SpawnMother(world, new Position(0, 0));
        var child = SpawnInfant(world, mother, new Position(0, 0), ageTicks: (rules.TicksPerYear * LifeStages.WeaningAgeYears) - 2);
        world.Advance(1);
        Assert.IsType<FollowTask>(child.Tasks.Current);

        world.Advance(1);

        Assert.IsNotType<FollowTask>(child.Tasks.Current);
    }

    [Fact]
    public void AnOrphanedInfantIsLeftToWanderLikeAnybodyElseWithNoSkill()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var infant = SpawnInfant(world, mother, new Position(0, 0));
        mother.IsAlive = false;

        world.Advance(1);

        Assert.IsType<IdleTask>(infant.Tasks.Current);
    }

    [Fact]
    public void AnInfantStaysWithinReachOfAMotherWhoWandersOffOnHerOwn()
    {
        // The mother's own idle wandering is what would otherwise strand the child - the
        // infant has to be able to out-walk her, not merely walk. Her id is pinned because
        // IdleTask's wander is seeded from it (see IdleTask.SeedFor): with a random one, how
        // far she actually gets in a given number of ticks - and so whether this test is
        // testing anything - would be a fresh coin flip every run.
        //
        // Fifty ticks: long enough for her to cover several times the reach the infant has to
        // stay inside, short enough that she has not yet starved (a nursing mother with
        // nothing to eat is on a much shorter clock than usual, which
        // AnOrphanedInfantEventuallyStarves picks up from there).
        var world = TestCatalogs.CreateWorld();
        var mother = world.SpawnPerson(TestIds.Person(1), "Sela", new Position(0, 0), initialAgeTicks: AdultAgeTicks(world), sex: Sex.Female);
        var infant = SpawnInfant(world, mother, new Position(0, 0));

        AdvanceTickByTick(world, 50);

        Assert.True(mother.IsAlive);
        Assert.True(WorldState.Distance(mother.Position, new Position(0, 0)) > world.Configuration.Rules.MaxInteractionDistance);
        Assert.True(world.IsWithinReach(infant.Position, mother.Position));
        Assert.Equal(0f, infant.Needs.Hunger);
    }
}
