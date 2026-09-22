using Godot;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// The player's view of whoever is selected. Everything here is read straight off the panel, so
// a wrong maximum or a stale label is visible in the game and invisible to every other test.
public class SelectionCardTests
{
    private static MeterReading Meter(WorldState world, Person person, string label) =>
        SelectionCard.For(world, person).Meters.Single(meter => meter.Label == label);

    [Fact]
    public void TheCardNamesThePersonAndWhatTheyAreDoing()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        var card = SelectionCard.For(world, person);

        Assert.Equal("Ava", card.Name);
        Assert.Equal("At rest", card.Task);
    }

    // Age and sex go beside the name, where a person is introduced rather than described; the life
    // stage is not repeated, because the age already says it.
    [Fact]
    public void AgeAndSexStandBesideTheName()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Equal($"{LifeStages.AdultAgeYears} winters, female", SelectionCard.For(world, person).Beside);
    }

    [Fact]
    public void TheDeadSayOnlyThat()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.IsAlive = false;

        Assert.Equal("deceased", SelectionCard.For(world, person).Beside);
    }

    [Fact]
    public void ParentsAreNamedUnderneath()
    {
        var world = TestWorld.Create();
        var mother = TestWorld.AddAdult(world, "Sela", new Position(0, 0));
        var father = TestWorld.AddAdult(world, "Doran", new Position(0, 0), Sex.Male);
        var child = TestWorld.AddChildOf(world, "Bran", mother, father);

        Assert.Equal("Child of Sela and Doran", SelectionCard.For(world, child).Parents);
    }

    // Person.Mother and Father are never null - an unremembered parent is Person.Unknown, and the
    // card must not introduce somebody called "Unknown".
    [Fact]
    public void APersonWithNoRememberedParentsSaysNothingAboutThem()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Equal(string.Empty, SelectionCard.For(world, person).Parents);
    }

    // Fatigue exists on Person but nothing in the simulation moves it, so a bar for it would sit
    // empty forever and teach the player it does not matter - which is not the intended lesson.
    [Fact]
    public void OnlyTheMeasuresThatActuallyMoveAreShown()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        var labels = SelectionCard.For(world, person).Meters.Select(meter => meter.Label).ToList();

        Assert.Equal(["Fed", "Carrying"], labels);
    }

    // The bar shows how full they are, not how hungry: it drains as hunger rises, so an empty bar
    // is a person in trouble rather than a person doing well.
    [Fact]
    public void TheFedBarEmptiesAsHungerRises()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Equal(1f, Meter(world, person, "Fed").Fraction, 3);

        person.Needs.Hunger = person.MaxHunger / 2f;

        Assert.Equal(0.5f, Meter(world, person, "Fed").Fraction, 3);
    }

    [Fact]
    public void CarryingMoreThanTheLimitStillFillsTheBarNoFurther()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 1000);

        Assert.Equal(1f, Meter(world, person, "Carrying").Fraction);
    }

    // Nothing to measure against divides by zero if it is not guarded; an empty bar is the honest
    // answer.
    [Fact]
    public void AMeasureWithNoLimitReadsEmpty()
    {
        Assert.Equal(0f, new MeterReading("Fed", 5f, 0f, Colors.White).Fraction);
    }

    // The colour turns at the moment the person's own behaviour does: below the threshold their
    // belly is their own business, at it they go looking for food by themselves.
    [Fact]
    public void TheFedBarKeepsItsColourUntilHungerSendsThemLookingForFood()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        var threshold = world.Configuration.Rules.HungerSeekFoodThreshold;

        person.Needs.Hunger = threshold - 1f;
        var fed = SelectionCard.HungerFill(person, threshold);

        person.Needs.Hunger = threshold;
        var hungry = SelectionCard.HungerFill(person, threshold);

        Assert.NotEqual(fed, hungry);
        Assert.Equal(fed, SelectionCard.HungerFill(person, threshold + 10f));
    }

    // Pinned to the exact colour the halfway point works out to, so the distance travelled is
    // measured against what is left above the threshold - not, say, the whole span above zero.
    [Fact]
    public void TheColourAtHalfwayBetweenThresholdAndDeathIsExactlyTheMidpointTint()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        var threshold = person.MaxHunger / 2f;
        person.Needs.Hunger = 3f * person.MaxHunger / 4f;

        var fill = SelectionCard.HungerFill(person, threshold);

        Assert.Equal(0.68f, fill.R, 4);
        Assert.Equal(0.38f, fill.G, 4);
        Assert.Equal(0.15f, fill.B, 4);
    }

    [Fact]
    public void TheFedBarDeepensAllTheWayToTheHungerThatKills()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        var threshold = world.Configuration.Rules.HungerSeekFoodThreshold;

        person.Needs.Hunger = threshold;
        var hungry = SelectionCard.HungerFill(person, threshold);

        person.Needs.Hunger = (threshold + person.MaxHunger) / 2f;
        var worse = SelectionCard.HungerFill(person, threshold);

        person.Needs.Hunger = person.MaxHunger;
        var starving = SelectionCard.HungerFill(person, threshold);

        Assert.True(worse.R > hungry.R || worse.G < hungry.G);
        Assert.True(starving.G < worse.G);
    }

    // A threshold set right at the maximum leaves no distance left to travel: the bar must still
    // read fully starving rather than dividing by that empty distance.
    [Fact]
    public void AThresholdRightAtTheMaximumStillReadsFullyStarving()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Needs.Hunger = person.MaxHunger;

        var fill = SelectionCard.HungerFill(person, person.MaxHunger);

        Assert.Equal(SelectionCard.HungerFill(person, 0f), fill);
        Assert.Equal(
            SelectionCard.HungerFill(person, person.MaxHunger - 1f),
            fill);
    }

    // Named the way the player met it - the skill's own name, never the technique id the debug
    // inspector prints - and one entry per skill, so the panel can give each its own line.
    [Fact]
    public void KnowledgeIsNamedBySkillNotByTechniqueId()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Empty(SelectionCard.For(world, person).Knowledge);

        person.KnownTechniques.Add(TestWorld.BasicForaging);

        Assert.Equal(["Foraging"], SelectionCard.For(world, person).Knowledge);
    }

    // "Knows" of somebody who may still learn; "Knew" of somebody who will not.
    [Fact]
    public void TheDeadKnewRatherThanKnow()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Equal("Knows", SelectionCard.For(world, person).KnowledgeLabel);

        person.IsAlive = false;

        Assert.Equal("Knew", SelectionCard.For(world, person).KnowledgeLabel);
    }

    // A corpse is not doing anything, and "At rest" under one reads as a joke.
    [Fact]
    public void TheDeadAreNotDoingAnything()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.IsAlive = false;

        Assert.Equal(string.Empty, SelectionCard.For(world, person).Task);
    }

    [Fact]
    public void TheDeadSayHowAndWhenTheyDied()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.IsAlive = false;
        person.DeathTick = world.Clock.CurrentTick;
        person.CauseOfDeath = DeathCause.Hunger;

        Assert.Equal($"Died at {LifeStages.AdultAgeYears} winters of hunger.", SelectionCard.For(world, person).Death);
    }

    // The age at death is fixed the moment they died, not whatever the clock reads when the
    // player happens to look - a grave visited a year later must not report a year older.
    [Fact]
    public void TheAgeAtDeathStaysFixedAsTheWorldMovesOn()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.IsAlive = false;
        person.DeathTick = world.Clock.CurrentTick;
        person.CauseOfDeath = DeathCause.Hunger;

        var atDeath = SelectionCard.For(world, person).Death;
        world.Advance(world.Configuration.Rules.TicksPerYear * 3);

        Assert.Equal(atDeath, SelectionCard.For(world, person).Death);
    }

    [Fact]
    public void TheLivingHaveNoDeathToReport()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Equal(string.Empty, SelectionCard.For(world, person).Death);
    }

    // Hunger stops mattering once someone is dead; what is on the body does not, because it can
    // still be taken.
    [Fact]
    public void TheDeadKeepOnlyTheMeasureThatStillMeansSomething()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.IsAlive = false;

        Assert.Equal(["Carrying"], SelectionCard.For(world, person).Meters.Select(meter => meter.Label).ToList());
    }

    [Fact]
    public void ThePackIsListedByItemNameAndSaysNothingWhenEmpty()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Equal("nothing", SelectionCard.For(world, person).Carried);

        person.Inventory.Add(TestWorld.Wood, 3);

        Assert.Equal("Wood x3", SelectionCard.For(world, person).Carried);
    }
}
