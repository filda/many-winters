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
        var node = world.ResourceNodes.FirstOrDefault(n => n.Id == ResourceNodeId && n.RemainingAmount > 0);
        if (person is null || node is null)
        {
            return;
        }

        var resource = world.ResourceCatalog.Get(node.Kind);
        var skill = resource.Skill;
        var skillDefinition = world.SkillCatalog.Get(skill);
        var technique = skillDefinition.EfficientTechnique;

        var harvestAmount = person.KnownTechniques.Contains(technique) ? EfficientHarvestAmount : BaseHarvestAmount;
        if (skillDefinition.Tool is { } tool && person.Inventory.Get(tool) > 0)
        {
            harvestAmount += skillDefinition.ToolHarvestBonus;
        }

        if (world.CurrentSeason == Season.Winter)
        {
            harvestAmount *= resource.WinterYieldMultiplier;
        }

        var consumed = Math.Min(node.RemainingAmount, harvestAmount);
        node.RemainingAmount -= consumed;

        if (resource.YieldsItem is { } item)
        {
            person.Inventory.Add(item, (int)consumed);
        }
        else
        {
            person.Needs.Hunger = Math.Max(0f, person.Needs.Hunger - consumed);
        }

        person.Skills.Increase(skill, SkillGainPerGather);
        if (person.Skills.Get(skill) >= DiscoveryThreshold)
        {
            person.KnownTechniques.Add(technique);
        }
    }
}
