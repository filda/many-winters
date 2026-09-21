using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

// Putting down anything a person carries - a stack of stock or one thing they made. One command
// covers both, because putting something down is one act; what differs is only what lands (see
// DropCommand, CarriedThing).
public class DropCommandTests
{
    [Fact]
    public void DroppingMovesItemsFromInventoryToANewPileAtThePersonsPosition()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(3, 4));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);

        world.Execute(new DropCommand(person, new CarriedThing.Stock(TestCatalogs.WoodItem, 3)));

        Assert.Equal(2, person.Inventory.Get(TestCatalogs.WoodItem));
        var pile = Assert.Single(world.Entities, e => e.Category == EntityCategory.Pile);
        Assert.Equal(new EntityKindId(TestCatalogs.WoodItem.Value), pile.Kind);
        Assert.Equal(3, pile.StaticAmount);
        Assert.Equal(person.Position, pile.Position);
    }

    [Fact]
    public void DroppingMoreThanCarriedDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 2);

        world.Execute(new DropCommand(person, new CarriedThing.Stock(TestCatalogs.WoodItem, 3)));

        Assert.Equal(2, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.DoesNotContain(world.Entities, e => e.Category == EntityCategory.Pile);
    }

    [Fact]
    public void ADeadPersonCannotDropItems()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        person.IsAlive = false;

        world.Execute(new DropCommand(person, new CarriedThing.Stock(TestCatalogs.WoodItem, 3)));

        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.DoesNotContain(world.Entities, e => e.Category == EntityCategory.Pile);
    }

    [Fact]
    public void NothingBlocksDroppingItemsActuallyCarried()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);

        Assert.Equal(ActionBlocker.None, new DropCommand(person, new CarriedThing.Stock(TestCatalogs.WoodItem, 3)).Blocker(world));
    }

    [Fact]
    public void ADeadActorIsBlockedFromDropping()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        person.IsAlive = false;

        Assert.Equal(ActionBlocker.ActorIsDead, new DropCommand(person, new CarriedThing.Stock(TestCatalogs.WoodItem, 3)).Blocker(world));
    }

    [Fact]
    public void DroppingMoreThanCarriedIsBlockedAsMissingMaterials()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 2);

        Assert.Equal(ActionBlocker.MissingMaterials, new DropCommand(person, new CarriedThing.Stock(TestCatalogs.WoodItem, 3)).Blocker(world));
    }

    private static Assembly.Part Cord() => new(new MaterialId("plant_fibre"), TestCatalogs.Cord, 0.8f, 5f);

    private static Assembly.Joined Axe() =>
        new(
            0.8f,
            0.5f,
            new Assembly.Part(new MaterialId("stone"), TestCatalogs.Wedge, 1f, 1f),
            new Assembly.Part(new MaterialId("wood"), new FormId("stick"), 1f, 2f));

    private static Person Carrying(WorldState world, Assembly thing, Position? at = null)
    {
        var person = world.SpawnPerson("Ava", at ?? new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.Inventory.AddAssembly(thing);

        return person;
    }

    [Fact]
    public void PuttingSomethingDownLeavesItWhereThePersonIsStanding()
    {
        var world = TestCatalogs.CreateWorld();
        var cord = Cord();
        var person = Carrying(world, cord, new Position(3, 4));

        world.Execute(new DropCommand(person, new CarriedThing.Worked(cord)));

        Assert.Empty(person.Inventory.Assemblies);
        var dropped = Assert.Single(world.Entities, entity => entity.Category == EntityCategory.Pile);
        Assert.Equal(cord, dropped.Made);
        Assert.Equal(person.Position, dropped.Position);
    }

    // It lands as itself, not as a pile of one: a made thing has no count, and rounding it into
    // a number is what the two tiers exist to avoid (see Entity.Made).
    [Fact]
    public void WhatLandsIsTheThingItselfRatherThanACountOfIt()
    {
        var world = TestCatalogs.CreateWorld();
        var cord = Cord();
        var person = Carrying(world, cord);

        world.Execute(new DropCommand(person, new CarriedThing.Worked(cord)));

        var dropped = Assert.Single(world.Entities, entity => entity.Category == EntityCategory.Pile);
        Assert.Null(dropped.StaticAmount);
        Assert.Equal(DropCommand.MadeThingKind, dropped.Kind);
    }

    // Depth survives being put down: what is picked up again is the same object, joints and all.
    [Fact]
    public void PuttingSomethingDownAndTakingItBackUpLeavesItUnchanged()
    {
        var world = TestCatalogs.CreateWorld();
        var axe = Axe();
        var person = Carrying(world, axe);

        world.Execute(new DropCommand(person, new CarriedThing.Worked(axe)));
        var dropped = Assert.Single(world.Entities, entity => entity.Category == EntityCategory.Pile);
        world.Execute(new PickUpItemCommand(person, dropped));

        Assert.Equal(axe, Assert.Single(person.Inventory.Assemblies));
        Assert.DoesNotContain(world.Entities, entity => entity.Category == EntityCategory.Pile);
    }

    // Whole or not at all, as off a body: what will not fit stays where it lies rather than
    // being half-taken.
    [Fact]
    public void SomethingTooHeavyToCarryStaysOnTheGround()
    {
        var world = TestCatalogs.CreateWorld();
        var millstone = new Assembly.Part(new MaterialId("stone"), TestCatalogs.Wedge, 1f, 1000f);
        var person = Carrying(world, millstone);
        world.Execute(new DropCommand(person, new CarriedThing.Worked(millstone)));
        var dropped = Assert.Single(world.Entities, entity => entity.Category == EntityCategory.Pile);

        world.Execute(new PickUpItemCommand(person, dropped));

        Assert.Empty(person.Inventory.Assemblies);
        Assert.Contains(world.Entities, entity => entity.Category == EntityCategory.Pile);
    }

    [Fact]
    public void PuttingDownSomethingTheyAreNotCarryingDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Carrying(world, Cord());
        var elsewhere = new Assembly.Part(new MaterialId("stone"), TestCatalogs.Wedge, 1f, 1f);

        var command = new DropCommand(person, new CarriedThing.Worked(elsewhere));

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.Single(person.Inventory.Assemblies);
        Assert.DoesNotContain(world.Entities, entity => entity.Category == EntityCategory.Pile);
    }

    [Fact]
    public void ADeadPersonPutsNothingDown()
    {
        var world = TestCatalogs.CreateWorld();
        var cord = Cord();
        var person = Carrying(world, cord);
        person.IsAlive = false;

        var command = new DropCommand(person, new CarriedThing.Worked(cord));

        Assert.Equal(ActionBlocker.ActorIsDead, command.Blocker(world));
        world.Execute(command);

        Assert.Single(person.Inventory.Assemblies);
    }

    // Two people can carry the same thing between them, one leg each.
    [Fact]
    public void SomebodyElseCanTakeUpWhatWasPutDown()
    {
        var world = TestCatalogs.CreateWorld();
        var cord = Cord();
        var ava = Carrying(world, cord);
        var bran = world.SpawnPerson("Bran", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

        world.Execute(new DropCommand(ava, new CarriedThing.Worked(cord)));
        world.Execute(new PickUpItemCommand(bran, Assert.Single(world.Entities, entity => entity.Category == EntityCategory.Pile)));

        Assert.Equal(cord, Assert.Single(bran.Inventory.Assemblies));
        Assert.Empty(ava.Inventory.Assemblies);
    }
}
