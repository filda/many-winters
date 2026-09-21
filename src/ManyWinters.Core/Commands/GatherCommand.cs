using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record GatherCommand(Person Person, Entity Node) : ICommand
{
    private const float BaseHarvestAmount = 20f;
    private const float EfficientHarvestAmount = 40f;
    private const float SkillGainPerGather = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // Stated in tries, not as a level: the practice curve is not linear (see Skills.Increase).
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (Node.Growth is not { } growth || !growth.IsAlive)
        {
            return ActionBlocker.TargetIsGone;
        }

        // Stryker disable once Equality: RemainingAmount never goes negative, and consuming zero is already a no-op below, so > 0 and >= 0 are indistinguishable here
        if (growth.RemainingAmount <= 0)
        {
            return ActionBlocker.NothingLeft;
        }

        if (!world.IsWithinReach(Person.Position, Node.Position))
        {
            return ActionBlocker.TooFar;
        }

        var resource = world.Configuration.ResourceCatalog.Get(Node.Kind);
        if (resource.YieldsItem is not null)
        {
            if (PotentialHarvestUnits(world, Person, resource, growth.RemainingAmount) <= 0)
            {
                return ActionBlocker.NothingLeft;
            }

            if (!CanTakeAnythingFrom(world, Person, resource, growth.RemainingAmount))
            {
                return ActionBlocker.InventoryFull;
            }
        }

        // Never self-taught, unlike the efficient technique: it has to be taught first (see
        // SkillDefinition.BaseTechnique). Asked last, like every knowledge gate
        // (see ActionBlocker.NotLearned).
        var skillDefinition = world.Configuration.SkillCatalog.Get(resource.Skill);
        return Person.KnownTechniques.Contains(skillDefinition.BaseTechnique)
            ? ActionBlocker.None
            : ActionBlocker.NotLearned;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        var growth = Node.Growth!;
        var resource = world.Configuration.ResourceCatalog.Get(Node.Kind);
        var skill = resource.Skill;
        var skillDefinition = world.Configuration.SkillCatalog.Get(skill);
        var technique = skillDefinition.EfficientTechnique;

        if (resource.YieldsItem is { } item)
        {
            var availableUnits = PotentialHarvestUnits(world, Person, resource, growth.RemainingAmount);
            // A hungry picker eats as they go before pocketing anything, so a full pack still
            // gets fed; only what's eaten or fits comes off the node. Same hunger test as the
            // autonomous pass (WorldState.IsHungryEnoughToEat), or a picker at a food source
            // would eat one unit every tick and practice forever.
            var eaten = world.IsHungryEnoughToEat(Person) ? EatCommand.Eat(world, Person, item, availableUnits) : 0;
            var added = Person.Inventory.AddUpToCapacity(item, availableUnits - eaten, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Person));
            var taken = eaten + added;

            growth.RemainingAmount -= taken;
        }
        else
        {
            var potentialConsumed = PotentialHarvestAmount(world, Person, resource, growth.RemainingAmount);
            growth.RemainingAmount -= potentialConsumed;
            Person.Needs.Hunger = Math.Max(0f, Person.Needs.Hunger - potentialConsumed);
        }

        Person.Skills.Increase(skill, SkillGainPerGather);
        if (Person.Skills.Get(skill) >= DiscoveryThreshold)
        {
            Person.KnownTechniques.Add(technique);
        }
    }

    // Used by WorldState before it sends someone walking to a source: range is deliberately not
    // part of this question, because a distant useful source is still a good GatherTask target.
    public static bool CanTakeAnythingFrom(
        WorldState world,
        Person person,
        ResourceDefinition resource,
        float remainingAmount)
    {
        if (resource.YieldsItem is not { } item)
        {
            return true;
        }

        var units = PotentialHarvestUnits(world, person, resource, remainingAmount);
        if (units <= 0)
        {
            return false;
        }

        var canEatOnTheSpot =
            world.IsHungryEnoughToEat(person)
            && EatCommand.EatingBlocker(world, person, item, units) is ActionBlocker.None;

        return canEatOnTheSpot
            || person.Inventory.HasRoomFor(item, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(person));
    }

    private static int PotentialHarvestUnits(
        WorldState world,
        Person person,
        ResourceDefinition resource,
        float remainingAmount) =>
        (int)PotentialHarvestAmount(world, person, resource, remainingAmount);

    private static float PotentialHarvestAmount(
        WorldState world,
        Person person,
        ResourceDefinition resource,
        float remainingAmount)
    {
        var skillDefinition = world.Configuration.SkillCatalog.Get(resource.Skill);
        var technique = skillDefinition.EfficientTechnique;
        var harvestAmount = person.KnownTechniques.Contains(technique) ? EfficientHarvestAmount : BaseHarvestAmount;
        if (skillDefinition.UsesChoppingScore)
        {
            harvestAmount += person.Inventory.BestChoppingScore(world.Configuration.ItemCatalog);
        }

        var climate = world.Configuration.SeasonParameters.ClimateFor(world.CurrentSeason);
        harvestAmount *= resource.YieldMultiplierFor(climate);

        return Math.Min(remainingAmount, harvestAmount);
    }
}
