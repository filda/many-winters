using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record GatherCommand(PersonId PersonId, ResourceNodeId ResourceNodeId) : ICommand
{
    private const float BaseHarvestAmount = 20f;
    private const float EfficientHarvestAmount = 40f;
    private const float SkillGainPerGather = 1f;
    private const float DiscoveryThreshold = 5f;

    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        // Stryker disable once Equality: RemainingAmount never goes negative, and consuming zero is already a no-op below, so > 0 and >= 0 are indistinguishable here
        var node = world.ResourceNodes.FirstOrDefault(n => n.Id == ResourceNodeId && n.IsAlive && n.RemainingAmount > 0);
        if (person is null || node is null || WorldState.Distance(person.Position, node.Position) > WorldState.MaxInteractionDistance)
        {
            return;
        }

        var resource = world.ResourceCatalog.Get(node.Kind);
        var skill = resource.Skill;
        var skillDefinition = world.SkillCatalog.Get(skill);
        // Never self-taught, unlike the efficient technique below - has to come from the
        // player or another person first (see SkillDefinition.BaseTechnique's own doc comment).
        if (!person.KnownTechniques.Contains(skillDefinition.BaseTechnique))
        {
            return;
        }

        var technique = skillDefinition.EfficientTechnique;

        var harvestAmount = person.KnownTechniques.Contains(technique) ? EfficientHarvestAmount : BaseHarvestAmount;
        if (skillDefinition.Tool is { } tool && person.Inventory.Get(tool) > 0)
        {
            harvestAmount += skillDefinition.ToolHarvestBonus;
        }

        var climate = world.SeasonParameters.ClimateFor(world.CurrentSeason);
        harvestAmount *= resource.YieldMultiplierFor(climate);

        var potentialConsumed = Math.Min(node.RemainingAmount, harvestAmount);

        if (resource.YieldsItem is { } item)
        {
            // Only what actually fits in the inventory comes off the node - a full backpack
            // leaves the rest standing to gather later, rather than the excess vanishing.
            var added = person.Inventory.AddUpToCapacity(item, (int)potentialConsumed, world.ItemCatalog, world.MaxCarryWeightFor(person));
            node.RemainingAmount -= added;
        }
        else
        {
            node.RemainingAmount -= potentialConsumed;
            person.Needs.Hunger = Math.Max(0f, person.Needs.Hunger - potentialConsumed);
        }

        person.Skills.Increase(skill, SkillGainPerGather);
        if (person.Skills.Get(skill) >= DiscoveryThreshold)
        {
            person.KnownTechniques.Add(technique);
        }
    }
}
