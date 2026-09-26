using ManyWinters.Core.Maps;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Milestones;

/// <summary>
/// Phase 1's own milestone (docs/todo/fauna-plan.md, phase 1: "stádo přežije rok na trávě a
/// rozmnoží se; bez trávy vyhyne"): a herd on grass survives a year and grows, the same herd with
/// no grass dies out, and nobody is born outside the species' breeding climate.
/// </summary>
public class DeerHerdMilestoneTests
{
    // Mirrors SimulationRules.Default (TicksPerSeason 75 * 4 seasons).
    private const long TicksPerSeason = 75;
    private const long TicksPerYear = TicksPerSeason * 4;

    // Old enough to be an adult (DeerAdultAgeYears 2) and nowhere near an elder (DeerElderAgeYears
    // 6), so nobody here is excluded from breeding by age.
    private const long AdultAgeTicks = 3 * TicksPerYear;

    private static HomeRange NewHome(Position anchor, float radius) => new(anchor) { Radius = radius, DriftMetresPerSeason = 0f };

    private static List<Animal> SpawnHerd(WorldState world, HomeRange home, int femaleCount, int maleCount)
    {
        var herd = new List<Animal>();
        for (var i = 0; i < femaleCount; i++)
        {
            herd.Add(world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(i, 0), home, AdultAgeTicks, Sex.Female));
        }

        for (var i = 0; i < maleCount; i++)
        {
            herd.Add(world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(-i - 1, 0), home, AdultAgeTicks, Sex.Male));
        }

        return herd;
    }

    // Two rings of near-endless grass around the anchor - close enough that a hungry deer never
    // has far to walk, plentiful enough that nobody's own gathering ever exhausts it.
    private static void ScatterGrass(WorldState world, Position center, int perRing)
    {
        void Ring(double distance)
        {
            for (var i = 0; i < perRing; i++)
            {
                var angle = i * (Math.Tau / perRing);
                var position = new Position(center.X + (Math.Cos(angle) * distance), center.Y + (Math.Sin(angle) * distance));
                world.SpawnResourceNode(TestCatalogs.Grass, position, 1_000_000f);
            }
        }

        Ring(3);
        Ring(8);
    }

    [Fact]
    public void AHerdOnPlentyOfGrassSurvivesAYearAndGrows()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var home = NewHome(new Position(0, 0), 15f);
        var herd = SpawnHerd(world, home, femaleCount: 4, maleCount: 4);
        ScatterGrass(world, home.Anchor, perRing: 6);

        world.Advance(TicksPerYear);

        var living = world.Animals.Count(a => a.IsAlive);
        Assert.True(living > herd.Count, $"expected more than the starting {herd.Count} deer alive, found {living}");
        Assert.Contains(world.Animals, a => a.BirthTick > 0);
    }

    [Fact]
    public void TheSameHerdWithNoGrassAnywhereDiesOutWellBeforeTheYearIsOut()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var home = NewHome(new Position(0, 0), 15f);
        SpawnHerd(world, home, femaleCount: 4, maleCount: 4);

        // No food anywhere: HungerPerTick 1 against MaxHunger ~100 (SimulationRules.Default)
        // starves everyone out well inside a year, the same arithmetic
        // WinterSurvivalMilestoneTests relies on for a person.
        world.Advance(200);

        Assert.DoesNotContain(world.Animals, a => a.IsAlive);
    }

    // Runs a fresh herd through exactly one season that is not the deer's own breeding climate
    // (Mild - deer.json, mirrored by TestCatalogs.DeerSpecies), with everything else that would
    // let them breed (a mate at home, plenty of grass) present. Nobody exists while the world
    // walks past the seasons before the one under test, so nothing can conceive on the way there.
    [Theory]
    [InlineData(TicksPerSeason, Season.Summer)]
    [InlineData(3 * TicksPerSeason, Season.Winter)]
    public void NoDeerIsBornOutsideTheBreedingClimate(long ticksToSkip, Season expectedSeason)
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var home = NewHome(new Position(0, 0), 15f);

        world.Advance(ticksToSkip);
        Assert.Equal(expectedSeason, world.CurrentSeason);

        var herd = SpawnHerd(world, home, femaleCount: 4, maleCount: 4);
        ScatterGrass(world, home.Anchor, perRing: 6);

        world.Advance(TicksPerSeason);

        Assert.Equal(herd.Count, world.Animals.Count);
    }

    // Two fixes made this hold (docs/todo/fauna-plan.md phase 1b, "the shipped map's herds
    // starving"): WorldState.FindNearestGatherableEntity no longer sends a whole herd at the
    // single node nearest its shared anchor, and its in-home tier now only counts a node that can
    // still give a full harvest (GatherCommand.WouldYieldAFullHarvest) rather than any sliver
    // above zero - a home tuft regrown to a crumb was "matching" and got nibbled at regen speed
    // forever. MapLoader.SpawnAnimalHerds also now places a herd where the grass actually is
    // (BestHerdCenter), not merely far enough from camp.
    //
    // Not asserted as "more than it started with": the two shipped herds (18 deer total measured
    // by hand) still end the year down to 6, despite 5 births along the way - net decline, just
    // no longer extinction. GroundCoverAmount (100) and RegenPerTick (1) are still sized for
    // scattered individual foragers, not a dozen-odd deer sharing a neighbourhood, so a herd still
    // outstrips its own patch faster than it regrows even once spread across several nodes and
    // placed where grass is dense. That remaining gap is food-density/regen tuning, out of this
    // task's scope - reported rather than chased here.
    [Fact]
    public void TheShippedWorldStillHasLivingDeerAfterAYear()
    {
        var map = MapLoader.LoadDefault(TestCatalogs.CreateConfigurationWithDeer());

        map.World.Advance(TicksPerYear);

        Assert.Contains(map.World.Animals, a => a.IsAlive);
    }
}
