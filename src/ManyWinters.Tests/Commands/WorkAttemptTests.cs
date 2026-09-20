using ManyWinters.Core.Commands;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

// The dice behind a directed attempt: skill moves the odds, it does not open or close the door
// (see docs/materials-and-crafting-architecture.md section 7).
public class WorkAttemptTests
{
    private static readonly SkillTypeId Skill = new("twisting");
    private static readonly TechniqueId Verb = new("twist");

    private static Person Somebody(WorldState world, string name = "Ava") =>
        world.SpawnPerson(name, new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

    private static void Practise(Person person, int times)
    {
        for (var i = 0; i < times; i++)
        {
            person.Skills.Increase(Skill, 1f);
        }
    }

    [Fact]
    public void SomebodyWhoHasNeverTriedStillHasAChance()
    {
        var world = TestCatalogs.CreateWorld();

        Assert.True(WorkAttempt.ChanceFor(Somebody(world), Skill) > 0f);
    }

    [Fact]
    public void APractisedHandNeverFails()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Somebody(world);
        Practise(person, 50);

        Assert.Equal(1f, WorkAttempt.ChanceFor(person, Skill), 5);
    }

    [Fact]
    public void PracticeImprovesTheOddsAllTheWayUp()
    {
        var world = TestCatalogs.CreateWorld();
        var beginner = Somebody(world);
        var middling = Somebody(world, "Bran");
        Practise(middling, 10);

        Assert.True(WorkAttempt.ChanceFor(middling, Skill) > WorkAttempt.ChanceFor(beginner, Skill));
        Assert.True(WorkAttempt.ChanceFor(middling, Skill) < 1f);
    }

    [Fact]
    public void TheOddsNeverPassCertaintyHoweverLongSomebodyPractises()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Somebody(world);
        Practise(person, 5000);

        Assert.Equal(1f, WorkAttempt.ChanceFor(person, Skill), 5);
    }

    // Deterministic from the person, the verb and the tick, like every other roll in the game -
    // asked twice about the same moment, it answers the same.
    [Fact]
    public void TheSameAttemptAtTheSameMomentRollsTheSameWay()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Somebody(world);

        Assert.Equal(
            WorkAttempt.Succeeds(person, Skill, Verb, tick: 12),
            WorkAttempt.Succeeds(person, Skill, Verb, tick: 12));
    }

    // Which is why an attempt has to cost time: a second try is only a second roll because the
    // clock moved (see SimulationRules.TicksPerWorkAttempt).
    [Fact]
    public void TheRollChangesFromOneMomentToTheNext()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Somebody(world);

        var outcomes = Enumerable.Range(0, 200)
            .Select(tick => WorkAttempt.Succeeds(person, Skill, Verb, tick))
            .Distinct()
            .ToList();

        Assert.Equal(2, outcomes.Count);
    }

    [Fact]
    public void TwoPeopleDoNotShareOneRoll()
    {
        var world = TestCatalogs.CreateWorld();
        var ava = Somebody(world);
        var bran = Somebody(world, "Bran");

        var differ = Enumerable.Range(0, 200)
            .Any(tick => WorkAttempt.Succeeds(ava, Skill, Verb, tick) != WorkAttempt.Succeeds(bran, Skill, Verb, tick));

        Assert.True(differ);
    }

    [Fact]
    public void TwoVerbsDoNotShareOneRoll()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Somebody(world);

        var differ = Enumerable.Range(0, 200)
            .Any(tick => WorkAttempt.Succeeds(person, Skill, Verb, tick)
                         != WorkAttempt.Succeeds(person, Skill, new TechniqueId("bind"), tick));

        Assert.True(differ);
    }

    // Roughly the odds it claims, over enough tries to tell - a roll that says 20% and lands 80%
    // of the time would pass every test above.
    [Fact]
    public void ABeginnerLandsAboutAsOftenAsTheChanceSays()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Somebody(world);
        var chance = WorkAttempt.ChanceFor(person, Skill);

        var wins = Enumerable.Range(0, 2000).Count(tick => WorkAttempt.Succeeds(person, Skill, Verb, tick));

        Assert.InRange(wins / 2000f, chance - 0.05f, chance + 0.05f);
    }
}
