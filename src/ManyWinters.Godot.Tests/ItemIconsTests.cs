using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// Which picture stands for a thing in somebody's pack. Paths only - whether one of them exists is
// ResourceLoader's question, and asking it here would abort the run (see this project's README).
public class ItemIconsTests
{
    // Gathered stuff is drawn as the resource it came off; a few things were drawn an icon of
    // their own, and that one wins where it exists.
    [Fact]
    public void GatheredStuffOffersItsOwnIconFirstAndTheResourceItCameOffSecond()
    {
        Assert.Equal(
            ["res://Content/items/wood/wood.png", "res://Content/resources/wood/wood.png"],
            ItemIcons.For(new CarriedThing.Stock(new ItemKindId("wood"))));
    }

    // A worked thing is its shape before it is its substance: one picture of a stick, whatever it
    // was cut from.
    [Fact]
    public void AWorkedThingIsDrawnByTheShapeItWasWorkedInto()
    {
        var cord = new Assembly.Part(new MaterialId("plant_fibre"), new FormId("cord"));

        Assert.Equal(["res://Content/forms/cord/cord.png"], ItemIcons.For(new CarriedThing.Worked(cord)));
    }

    // Nothing is drawn for a joint, and neither half of it stands for the whole: a bound thing
    // gets a blank of its own rather than the picture of one of its parts.
    [Fact]
    public void TwoThingsBoundTogetherAreDrawnByNeitherOfThem()
    {
        var left = new Assembly.Part(new MaterialId("wood"), new FormId("stick"));
        var right = new Assembly.Part(new MaterialId("plant_fibre"), new FormId("cord"));
        var bound = new Assembly.Joined(0.5f, 1f, left, right);

        Assert.Empty(ItemIcons.For(new CarriedThing.Worked(bound)));
    }
}
