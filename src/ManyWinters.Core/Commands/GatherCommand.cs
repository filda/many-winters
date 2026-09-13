using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record GatherCommand(Person Person, ResourceNode Node) : ICommand
{
    private const float BaseHarvestAmount = 20f;
    private const float EfficientHarvestAmount = 40f;
    private const float SkillGainPerGather = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // Stated in tries, not as a level: the practice curve is not linear (see Skills.Increase).
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    public void Execute(WorldState world)
    {
        // Stryker disable once Equality: RemainingAmount never goes negative, and consuming zero is already a no-op below, so > 0 and >= 0 are indistinguishable here
        if (!Person.IsAlive || !Node.IsAlive || Node.RemainingAmount <= 0 || !world.IsWithinReach(Person.Position, Node.Position))
        {
            return;
        }

        var resource = world.Configuration.ResourceCatalog.Get(Node.Kind);
        var skill = resource.Skill;
        var skillDefinition = world.Configuration.SkillCatalog.Get(skill);
        // Never self-taught, unlike the efficient technique: it has to be taught first
        // (see SkillDefinition.BaseTechnique).
        if (!Person.KnownTechniques.Contains(skillDefinition.BaseTechnique))
        {
            return;
        }

        var technique = skillDefinition.EfficientTechnique;

        var harvestAmount = Person.KnownTechniques.Contains(technique) ? EfficientHarvestAmount : BaseHarvestAmount;
        if (skillDefinition.Tool is { } tool && Person.Inventory.Get(tool) > 0)
        {
            harvestAmount += skillDefinition.ToolHarvestBonus;
        }

        var climate = world.Configuration.SeasonParameters.ClimateFor(world.CurrentSeason);
        harvestAmount *= resource.YieldMultiplierFor(climate);

        var potentialConsumed = Math.Min(Node.RemainingAmount, harvestAmount);

        if (resource.YieldsItem is { } item)
        {
            // A hungry picker eats as they go before pocketing anything - how someone with a full
            // pack still gets fed. Only what was eaten or fits comes off the node; the rest stays
            // for later. Same hunger test as the autonomous pass (WorldState.IsHungryEnoughToEat),
            // or a picker at a food source would eat one unit every tick and practice forever.
            var eaten = world.IsHungryEnoughToEat(Person) ? EatCommand.Eat(world, Person, item, (int)potentialConsumed) : 0;
            var added = Person.Inventory.AddUpToCapacity(item, (int)potentialConsumed - eaten, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Person));
            var taken = eaten + added;
            // Coming away with nothing earns no practice, so a full pack cannot grind a technique.
            if (taken <= 0)
            {
                return;
            }

            Node.RemainingAmount -= taken;
        }
        else
        {
            Node.RemainingAmount -= potentialConsumed;
            Person.Needs.Hunger = Math.Max(0f, Person.Needs.Hunger - potentialConsumed);
        }

        Person.Skills.Increase(skill, SkillGainPerGather);
        if (Person.Skills.Get(skill) >= DiscoveryThreshold)
        {
            Person.KnownTechniques.Add(technique);
        }
    }
}
