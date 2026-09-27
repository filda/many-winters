using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Actor is a Creature, not a Person: an animal grazing is the same command as a person gathering
// (docs/todo/fauna-plan.md, phase 1a) - only its species' known techniques and carry capacity
// differ, both already read off Creature.
public sealed record GatherCommand(Creature Actor, Entity Node) : ICommand
{
    private const float BaseHarvestAmount = 20f;
    private const float EfficientHarvestAmount = 40f;
    private const float SkillGainPerGather = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // Stated in tries, not as a level: the practice curve is not linear (see Skills.Increase).
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Actor.IsAlive)
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

        if (!world.IsWithinReach(Actor.Position, Node.Position))
        {
            return ActionBlocker.TooFar;
        }

        var resource = world.Configuration.ResourceCatalog.Get(Node.Kind);
        if (resource.YieldsItem is not null)
        {
            if (PotentialHarvestUnits(world, Actor, resource, growth.RemainingAmount) <= 0)
            {
                return ActionBlocker.NothingLeft;
            }

            if (!CanTakeAnythingFrom(world, Actor, resource, growth.RemainingAmount))
            {
                return ActionBlocker.InventoryFull;
            }
        }

        // Never self-taught, unlike the efficient technique: it has to be taught first (see
        // SkillDefinition.BaseTechnique). Asked last, like every knowledge gate
        // (see ActionBlocker.NotLearned).
        var skillDefinition = world.Configuration.SkillCatalog.Get(resource.Skill);
        return Actor.KnownTechniques.Contains(skillDefinition.BaseTechnique)
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
            var availableUnits = PotentialHarvestUnits(world, Actor, resource, growth.RemainingAmount);
            // A hungry picker eats as they go before pocketing anything, so a full pack still
            // gets fed; only what's eaten or fits comes off the node. Same hunger test as the
            // autonomous pass (WorldState.IsHungryEnoughToEat), or a picker at a food source
            // would eat one unit every tick and practice forever.
            var eaten = world.IsHungryEnoughToEat(Actor) ? EatCommand.Eat(world, Actor, item, availableUnits) : 0;
            // A freshly picked unit comes into being right now (docs/todo/fauna-plan.md phase 4c) -
            // unlike a transfer, there is no earlier age to preserve: the node it came off grows
            // rather than spoiling.
            var added = Actor.Inventory.AddUpToCapacity(item, availableUnits - eaten, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Actor), world.Clock.CurrentTick);
            var taken = eaten + added;

            growth.RemainingAmount -= taken;
        }
        else
        {
            var potentialConsumed = PotentialHarvestAmount(world, Actor, resource, growth.RemainingAmount);
            growth.RemainingAmount -= potentialConsumed;
            Actor.Needs.Hunger = Math.Max(0f, Actor.Needs.Hunger - potentialConsumed);
        }

        Actor.Skills.Increase(skill, SkillGainPerGather);
        if (Actor.Skills.Get(skill) >= DiscoveryThreshold)
        {
            Actor.KnownTechniques.Add(technique);
        }
    }

    // Used by WorldState before it sends someone walking to a source: range is deliberately not
    // part of this question, because a distant useful source is still a good GatherTask target.
    public static bool CanTakeAnythingFrom(
        WorldState world,
        Creature actor,
        ResourceDefinition resource,
        float remainingAmount)
    {
        if (resource.YieldsItem is not { } item)
        {
            return true;
        }

        var units = PotentialHarvestUnits(world, actor, resource, remainingAmount);
        if (units <= 0)
        {
            return false;
        }

        var canEatOnTheSpot =
            world.IsHungryEnoughToEat(actor)
            && EatCommand.EatingBlocker(world, actor, item, units) is ActionBlocker.None;

        return canEatOnTheSpot
            || actor.Inventory.HasRoomFor(item, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(actor));
    }

    private static int PotentialHarvestUnits(
        WorldState world,
        Creature actor,
        ResourceDefinition resource,
        float remainingAmount) =>
        (int)PotentialHarvestAmount(world, actor, resource, remainingAmount);

    private static float PotentialHarvestAmount(
        WorldState world,
        Creature actor,
        ResourceDefinition resource,
        float remainingAmount) =>
        Math.Min(remainingAmount, HarvestAmountFor(world, actor, resource));

    // What one gather would take home for this creature, this technique and this season, with no
    // cap from what the node actually has left - the very thing WouldYieldAFullHarvest asks
    // remainingAmount against, and PotentialHarvestAmount's own Math.Min caps away.
    private static float HarvestAmountFor(WorldState world, Creature actor, ResourceDefinition resource)
    {
        var skillDefinition = world.Configuration.SkillCatalog.Get(resource.Skill);
        var technique = skillDefinition.EfficientTechnique;
        var harvestAmount = actor.KnownTechniques.Contains(technique) ? EfficientHarvestAmount : BaseHarvestAmount;
        if (skillDefinition.UsesChoppingScore)
        {
            harvestAmount += actor.Inventory.BestChoppingScore(world.Configuration.ItemCatalog);
        }

        var climate = world.Configuration.SeasonParameters.ClimateFor(world.CurrentSeason);
        return harvestAmount * resource.YieldMultiplierFor(climate);
    }

    // Whether this node currently holds enough to give this creature a full gather rather than a
    // dwindling nibble - what WorldState.FindNearestGatherableEntity's in-home tier asks instead
    // of IsWorthGathering's plain "more than zero left", so a herd doesn't converge on whichever
    // home tuft has barely regrown and nibble it at regen speed forever (docs/todo/fauna-plan.md
    // phase 1b: the shipped map's herds starving).
    public static bool WouldYieldAFullHarvest(WorldState world, Creature actor, ResourceDefinition resource, float remainingAmount) =>
        remainingAmount >= HarvestAmountFor(world, actor, resource);
}
