using ManyWinters.Core.Commands;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Commands;

public class GatherCommandTests
{
    [Fact]
    public void GatheringReducesHungerAndDepletesTheNode()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 50;
        var node = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(30f, person.Needs.Hunger);
        Assert.Equal(80f, node.RemainingAmount);
        Assert.Equal(1f, person.Skills.Get(SkillType.Foraging));
    }

    [Fact]
    public void GatheringNeverReducesHungerBelowZero()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 5;
        var node = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(0f, person.Needs.Hunger);
    }

    [Fact]
    public void GatheringNeverTakesMoreThanTheNodeHasRemaining()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 50;
        var node = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 5);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(45f, person.Needs.Hunger);
        Assert.Equal(0f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringFromAnAlreadyEmptyNodeDoesNothing()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 50;
        var node = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 0);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(0f, node.RemainingAmount);
        Assert.Equal(0f, person.Skills.Get(SkillType.Foraging));
    }

    [Fact]
    public void GatheringByADeadPersonDoesNothing()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.Needs.Hunger = 50;
        var node = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringWithAnUnknownPersonOrNodeDoesNothing()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 50;
        var node = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(new PersonId(999), node.Id));
        world.Execute(new GatherCommand(person.Id, new ResourceNodeId(999)));

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void FiveAppleGathersDiscoverEfficientForaging()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 1000);

        for (var i = 0; i < 4; i++)
        {
            world.Execute(new GatherCommand(person.Id, node.Id));
        }

        Assert.DoesNotContain(Technique.EfficientForaging, person.KnownTechniques);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(5f, person.Skills.Get(SkillType.Foraging));
        Assert.Contains(Technique.EfficientForaging, person.KnownTechniques);
    }

    [Fact]
    public void KnowingEfficientForagingHarvestsMorePerAction()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(Technique.EfficientForaging);
        person.Needs.Hunger = 100;
        var node = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 1000);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(60f, person.Needs.Hunger);
        Assert.Equal(960f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringPearsAlsoTrainsTheForagingSkill()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var appleNode = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 100);
        var pearNode = world.AddResourceNode(ResourceKind.Pear, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, appleNode.Id));
        world.Execute(new GatherCommand(person.Id, pearNode.Id));

        Assert.Equal(2f, person.Skills.Get(SkillType.Foraging));
    }

    [Fact]
    public void GatheringMushroomsTrainsADifferentSkillThanForaging()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var appleNode = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 100);
        var mushroomNode = world.AddResourceNode(ResourceKind.Mushroom, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, appleNode.Id));
        world.Execute(new GatherCommand(person.Id, mushroomNode.Id));

        Assert.Equal(1f, person.Skills.Get(SkillType.Foraging));
        Assert.Equal(1f, person.Skills.Get(SkillType.MushroomForaging));
    }

    [Fact]
    public void DiscoveringEfficientForagingDoesNotUnlockEfficientMushroomForaging()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 1000);

        for (var i = 0; i < 5; i++)
        {
            world.Execute(new GatherCommand(person.Id, node.Id));
        }

        Assert.Contains(Technique.EfficientForaging, person.KnownTechniques);
        Assert.DoesNotContain(Technique.EfficientMushroomForaging, person.KnownTechniques);
    }
}
