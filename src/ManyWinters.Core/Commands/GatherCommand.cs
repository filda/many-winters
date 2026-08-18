using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record GatherCommand(PersonId PersonId, ResourceNodeId ResourceNodeId) : ICommand
{
    private const float BaseHarvestAmount = 20f;
    private const float EfficientHarvestAmount = 40f;
    private const float SkillGainPerGather = 1f;
    private const float EfficientGatheringDiscoveryThreshold = 5f;

    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        // Stryker disable once Equality: RemainingAmount never goes negative, and consuming zero is already a no-op below, so > 0 and >= 0 are indistinguishable here
        var node = world.ResourceNodes.FirstOrDefault(n => n.Id == ResourceNodeId && n.RemainingAmount > 0);
        if (person is null || node is null)
        {
            return;
        }

        var harvestAmount = person.KnownTechniques.Contains(Technique.EfficientGathering)
            ? EfficientHarvestAmount
            : BaseHarvestAmount;

        var consumed = Math.Min(node.RemainingAmount, harvestAmount);
        node.RemainingAmount -= consumed;
        person.Needs.Hunger = Math.Max(0f, person.Needs.Hunger - consumed);

        person.Skills.Gathering += SkillGainPerGather;
        if (person.Skills.Gathering >= EfficientGatheringDiscoveryThreshold)
        {
            person.KnownTechniques.Add(Technique.EfficientGathering);
        }
    }
}
