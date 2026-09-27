using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

// ButcherCommand follows GatherCommand's pattern exactly: a base technique that must be taught
// before anything can be taken at all, and an efficient one earned through practice that gets
// more out of the same carcass.
public class ButcherCommandTests
{
    private static Animal DeadDeer(
        WorldState world,
        Position position,
        int meat = TestCatalogs.DeerCarcassMeat,
        int hide = TestCatalogs.DeerCarcassHide,
        int bone = TestCatalogs.DeerCarcassBone,
        int sinew = TestCatalogs.DeerCarcassSinew)
    {
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position);
        deer.IsAlive = false;
        if (meat > 0)
        {
            deer.Inventory.Add(TestCatalogs.MeatItem, meat);
        }

        if (hide > 0)
        {
            deer.Inventory.Add(TestCatalogs.RawhideItem, hide);
        }

        if (bone > 0)
        {
            deer.Inventory.Add(TestCatalogs.BoneItem, bone);
        }

        if (sinew > 0)
        {
            deer.Inventory.Add(TestCatalogs.SinewItem, sinew);
        }

        return deer;
    }

    private static Person Butcher(WorldState world, Position position, bool knowsEfficientButchering = false)
    {
        var person = world.SpawnPerson("Ava", position, initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicButchering);
        if (knowsEfficientButchering)
        {
            person.KnownTechniques.Add(TestCatalogs.EfficientButchering);
        }

        return person;
    }

    [Fact]
    public void NothingBlocksAKnowledgeableButcherWithACarcassWithinReach()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));
        var carcass = DeadDeer(world, new Position(0, 0));

        Assert.Equal(ActionBlocker.None, new ButcherCommand(butcher, carcass).Blocker(world));
    }

    [Fact]
    public void ButcheringRequiresTheButcherToBeAlive()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));
        butcher.IsAlive = false;
        var carcass = DeadDeer(world, new Position(0, 0));

        Assert.Equal(ActionBlocker.ActorIsDead, new ButcherCommand(butcher, carcass).Blocker(world));
    }

    [Fact]
    public void ALivingDeerBlocksButcheringAsTargetIsAlive()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));
        var livingDeer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0));

        Assert.Equal(ActionBlocker.TargetIsAlive, new ButcherCommand(butcher, livingDeer).Blocker(world));
    }

    [Fact]
    public void ACarcassAlreadyPickedCleanBlocksButcheringAsNothingLeft()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));
        var carcass = DeadDeer(world, new Position(0, 0), meat: 0, hide: 0, bone: 0, sinew: 0);

        Assert.Equal(ActionBlocker.NothingLeft, new ButcherCommand(butcher, carcass).Blocker(world));
    }

    // A decayed carcass has only bone left, the perishables gone - a beginner takes it exactly
    // as they always could, since bone was always the beginner's share.
    [Fact]
    public void ABeginnerButcherTakesOnlyBoneFromABoneOnlyCarcass()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));
        var carcass = DeadDeer(world, new Position(0, 0), meat: 0, hide: 0, bone: TestCatalogs.DeerCarcassBone, sinew: 0);

        world.Execute(new ButcherCommand(butcher, carcass));

        Assert.Equal(TestCatalogs.DeerCarcassBone, butcher.Inventory.Get(TestCatalogs.BoneItem));
        Assert.Equal(0, carcass.Inventory.Get(TestCatalogs.BoneItem));
    }

    // Same, with the efficient technique already known - it changes nothing about a carcass
    // that only ever had bone to give.
    [Fact]
    public void AnExpertButcherAlsoTakesOnlyBoneFromABoneOnlyCarcass()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0), knowsEfficientButchering: true);
        var carcass = DeadDeer(world, new Position(0, 0), meat: 0, hide: 0, bone: TestCatalogs.DeerCarcassBone, sinew: 0);

        world.Execute(new ButcherCommand(butcher, carcass));

        Assert.Equal(TestCatalogs.DeerCarcassBone, butcher.Inventory.Get(TestCatalogs.BoneItem));
    }

    [Fact]
    public void ACarcassOutOfReachBlocksButcheringAsTooFar()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));
        var carcass = DeadDeer(world, new Position(world.Configuration.Rules.PileReachDistance + 1, 0));

        Assert.Equal(ActionBlocker.TooFar, new ButcherCommand(butcher, carcass).Blocker(world));
    }

    [Fact]
    public void ACarcassAtExactlyThePileReachDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));
        var carcass = DeadDeer(world, new Position(world.Configuration.Rules.PileReachDistance, 0));

        Assert.Equal(ActionBlocker.None, new ButcherCommand(butcher, carcass).Blocker(world));
    }

    [Fact]
    public void AFullPackBlocksButcheringAsInventoryFull()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));
        // Wood weighs exactly 1 per unit (density 0.5 * volume 2), so this fills the pack to the
        // last gram, leaving no room for even a single unit of meat.
        butcher.Inventory.Add(TestCatalogs.WoodItem, (int)world.MaxCarryWeightFor(butcher));
        var carcass = DeadDeer(world, new Position(0, 0));

        Assert.Equal(ActionBlocker.InventoryFull, new ButcherCommand(butcher, carcass).Blocker(world));
    }

    [Fact]
    public void ButcheringWithoutHavingLearnedItDoesNothing()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var carcass = DeadDeer(world, new Position(0, 0));

        var command = new ButcherCommand(butcher, carcass);
        Assert.Equal(ActionBlocker.NotLearned, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(butcher.Inventory.Counts);
        Assert.Equal(TestCatalogs.DeerCarcassMeat, carcass.Inventory.Get(TestCatalogs.MeatItem));
    }

    // Knowledge is asked last: a butcher with a full pack and no training at all hears about the
    // pack, not that they never learned to butcher.
    [Fact]
    public void AFullPackIsBlamedBeforeNeverHavingLearnedToButcher()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        butcher.Inventory.Add(TestCatalogs.WoodItem, (int)world.MaxCarryWeightFor(butcher));
        var carcass = DeadDeer(world, new Position(0, 0));

        Assert.Equal(ActionBlocker.InventoryFull, new ButcherCommand(butcher, carcass).Blocker(world));
    }

    [Fact]
    public void ABeginnerButcherTakesMeatAndBoneButLeavesHideAndSinewOnTheCarcass()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));
        var carcass = DeadDeer(world, new Position(0, 0));

        world.Execute(new ButcherCommand(butcher, carcass));

        Assert.Equal(TestCatalogs.DeerCarcassMeat, butcher.Inventory.Get(TestCatalogs.MeatItem));
        Assert.Equal(TestCatalogs.DeerCarcassBone, butcher.Inventory.Get(TestCatalogs.BoneItem));
        Assert.Equal(0, butcher.Inventory.Get(TestCatalogs.RawhideItem));
        Assert.Equal(0, butcher.Inventory.Get(TestCatalogs.SinewItem));

        Assert.Equal(0, carcass.Inventory.Get(TestCatalogs.MeatItem));
        Assert.Equal(0, carcass.Inventory.Get(TestCatalogs.BoneItem));
        Assert.Equal(TestCatalogs.DeerCarcassHide, carcass.Inventory.Get(TestCatalogs.RawhideItem));
        Assert.Equal(TestCatalogs.DeerCarcassSinew, carcass.Inventory.Get(TestCatalogs.SinewItem));
    }

    [Fact]
    public void AnExpertButcherTakesEverythingOffTheCarcass()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0), knowsEfficientButchering: true);
        var carcass = DeadDeer(world, new Position(0, 0));

        world.Execute(new ButcherCommand(butcher, carcass));

        Assert.Equal(TestCatalogs.DeerCarcassMeat, butcher.Inventory.Get(TestCatalogs.MeatItem));
        Assert.Equal(TestCatalogs.DeerCarcassHide, butcher.Inventory.Get(TestCatalogs.RawhideItem));
        Assert.Equal(TestCatalogs.DeerCarcassBone, butcher.Inventory.Get(TestCatalogs.BoneItem));
        Assert.Equal(TestCatalogs.DeerCarcassSinew, butcher.Inventory.Get(TestCatalogs.SinewItem));
        Assert.Empty(carcass.Inventory.Counts);
    }

    [Fact]
    public void ButcheringIsPractice()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));
        var carcass = DeadDeer(world, new Position(0, 0));

        world.Execute(new ButcherCommand(butcher, carcass));

        Assert.True(butcher.Skills.Get(ButcherCommand.Skill) > 0f);
    }

    // Miming butchering with nothing on the carcass, or without ever having learned it, must not
    // count as practice - the same rule GatherCommand and EatCommand hold to.
    [Fact]
    public void AnEmptyCarcassIsNotPractice()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));
        var carcass = DeadDeer(world, new Position(0, 0), meat: 0, hide: 0, bone: 0, sinew: 0);

        world.Execute(new ButcherCommand(butcher, carcass));

        Assert.Equal(0f, butcher.Skills.Get(ButcherCommand.Skill));
    }

    // Five butcherings is exactly the threshold: the one that reaches it is the one that teaches
    // the efficient technique. The pack is emptied between carcasses -
    // a butcher bringing a haul home before going back out for the next deer - so capacity never
    // gets in the way of counting practice.
    [Fact]
    public void FiveButcheringsDiscoverTheEfficientTechnique()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));

        for (var i = 0; i < 5; i++)
        {
            var carcass = DeadDeer(world, new Position(0, 0));
            world.Execute(new ButcherCommand(butcher, carcass));
            butcher.Inventory.Remove(TestCatalogs.MeatItem, butcher.Inventory.Get(TestCatalogs.MeatItem));
            butcher.Inventory.Remove(TestCatalogs.BoneItem, butcher.Inventory.Get(TestCatalogs.BoneItem));
        }

        Assert.Contains(TestCatalogs.EfficientButchering, butcher.KnownTechniques);
    }

    [Fact]
    public void FourButcheringsFallShortOfDiscoveringTheEfficientTechnique()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var butcher = Butcher(world, new Position(0, 0));

        for (var i = 0; i < 4; i++)
        {
            var carcass = DeadDeer(world, new Position(0, 0));
            world.Execute(new ButcherCommand(butcher, carcass));
            butcher.Inventory.Remove(TestCatalogs.MeatItem, butcher.Inventory.Get(TestCatalogs.MeatItem));
            butcher.Inventory.Remove(TestCatalogs.BoneItem, butcher.Inventory.Get(TestCatalogs.BoneItem));
        }

        Assert.DoesNotContain(TestCatalogs.EfficientButchering, butcher.KnownTechniques);
    }
}
