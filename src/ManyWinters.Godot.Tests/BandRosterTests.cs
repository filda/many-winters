using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// The band as a list, the way the player reads it when they have lost track of where everybody
// went. Everything here is read straight off the panel, so a missing person or a stale line is
// visible in the game and invisible to every other test.
public class BandRosterTests
{
    [Fact]
    public void EachLineNamesSomebodyWithTheirWintersAndSaysWhatTheyAreDoing()
    {
        var world = TestWorld.Create();
        TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        var entry = Assert.Single(BandRoster.Of(world).People);

        Assert.Equal($"Ava ({LifeStages.AdultAgeYears})", entry.Heading);
        Assert.Equal("At rest", entry.Task);
    }

    // The same wording as the selection card's, because it is the same sentence about the same
    // person - a roster that called it "Idle" would be the debugger's word on a player's page.
    [Fact]
    public void WhatSomebodyIsDoingReadsTheSameAsOnTheirCard()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        world.Execute(new MoveCommand(person, new Position(10, 10)));

        var entry = Assert.Single(BandRoster.Of(world).People);

        Assert.Equal("Walking", entry.Task);
        Assert.Equal(SelectionCard.For(world, person).Task, entry.Task);
    }

    // Winters, counted down to nothing: the number is bare, so the first year of a life has to
    // read as a plain "(0)" rather than borrowing DurationText's seasons.
    [Fact]
    public void SomebodyInTheirFirstWinterIsNoWintersOld()
    {
        var world = TestWorld.Create();
        var mother = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        TestWorld.AddChildOf(world, "Cel", mother, mother);

        Assert.Contains("Cel (0)", Headings(world));
    }

    // The bar under each name is the card's own Fed meter, so a belly reads the same length and
    // the same colour whichever page the player is looking at.
    [Fact]
    public void TheBarUnderANameIsTheSameBellyAsOnTheCard()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Needs.Hunger = person.MaxHunger / 4f;

        var fed = Assert.Single(BandRoster.Of(world).People).Fed;

        Assert.Equal(SelectionCard.For(world, person).Meters.Single(meter => meter.Label == "Fed"), fed);
        Assert.Equal(0.75f, fed.Fraction, 3);
    }

    // The line carries the person themselves, so pressing it selects the one the player read.
    [Fact]
    public void ALineCarriesThePersonItNames()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Same(person, Assert.Single(BandRoster.Of(world).People).Person);
    }

    // The dead are found by their graves. A roster is read to go and look at somebody, which is
    // not a thing to do to a corpse.
    [Fact]
    public void TheDeadAreLeftOut()
    {
        var world = TestWorld.Create();
        TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        var dead = TestWorld.AddAdult(world, "Bora", new Position(1, 0));
        dead.IsAlive = false;

        Assert.Equal(["Ava"], Names(world));
    }

    // Ordered by name, so a line stays where the player last saw it and the list can be scanned
    // for the name they are hunting for.
    [Fact]
    public void LinesAreOrderedByName()
    {
        var world = TestWorld.Create();
        TestWorld.AddAdult(world, "Zora", new Position(0, 0));
        TestWorld.AddAdult(world, "Ava", new Position(1, 0));
        TestWorld.AddAdult(world, "Mira", new Position(2, 0));

        Assert.Equal(["Ava", "Mira", "Zora"], Names(world));
    }

    // Named after its eldest, as the band is named everywhere else (BandName) - but as the band
    // the player commands, not as a later band reading its graves will know it.
    [Fact]
    public void TheTitleNamesTheBandAfterItsEldest()
    {
        var world = TestWorld.Create();
        TestWorld.AddAdult(world, "Liska", new Position(0, 0));
        // A tick later, so Ava is the younger of the two by birth rather than by name.
        world.Advance(1);
        TestWorld.AddAdult(world, "Ava", new Position(1, 0));

        Assert.Equal("Liska's band", BandRoster.Of(world).Title);
    }

    // The count above the list, broken down the same three ways the pause panel and the prologue
    // break the band down - it is one band however the player comes to look at it.
    [Fact]
    public void TheSummaryCountsTheBandTheWayEveryOtherPageDoes()
    {
        var world = TestWorld.Create();
        var mother = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        var father = TestWorld.AddAdult(world, "Bor", new Position(1, 0), Sex.Male);
        TestWorld.AddChildOf(world, "Cel", mother, father);

        Assert.Equal("3 people: 1 man, 1 woman and 1 child.", BandRoster.Of(world).Summary);
    }

    // Opened over a band that has just died out. BandArrival.Of refuses to describe one, so the
    // roster answers for itself rather than throwing in the player's face.
    [Fact]
    public void ABandNobodySurvivedSaysSo()
    {
        var world = TestWorld.Create();
        TestWorld.AddAdult(world, "Ava", new Position(0, 0)).IsAlive = false;

        var roster = BandRoster.Of(world);

        Assert.Empty(roster.People);
        Assert.Equal("Nobody left.", roster.Summary);
        Assert.Equal("The band", roster.Title);
    }

    // The name off the front of a heading, where the winters follow it in brackets.
    private static IEnumerable<string> Names(WorldState world) =>
        Headings(world).Select(heading => heading.Split(' ')[0]);

    private static IEnumerable<string> Headings(WorldState world) =>
        BandRoster.Of(world).People.Select(entry => entry.Heading);
}
