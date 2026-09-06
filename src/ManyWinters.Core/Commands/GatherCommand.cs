using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record GatherCommand(Person Person, ResourceNode Node) : ICommand
{
    private const float BaseHarvestAmount = 20f;
    private const float EfficientHarvestAmount = 40f;
    private const float SkillGainPerGather = 1f;
    private const float DiscoveryThreshold = 5f;

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
        // Never self-taught, unlike the efficient technique below - has to come from the
        // player or another person first (see SkillDefinition.BaseTechnique's own doc comment).
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
            // Only what actually fits in the inventory comes off the node - a full backpack
            // leaves the rest standing to gather later, rather than the excess vanishing.
            var added = Person.Inventory.AddUpToCapacity(item, (int)potentialConsumed, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Person));
            Node.RemainingAmount -= added;
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
