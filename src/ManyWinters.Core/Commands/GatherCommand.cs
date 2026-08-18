using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record GatherCommand(PersonId PersonId, ResourceNodeId ResourceNodeId) : ICommand
{
    private const float HarvestAmount = 20f;

    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        // Stryker disable once Equality: RemainingAmount never goes negative, and consuming zero is already a no-op below, so > 0 and >= 0 are indistinguishable here
        var node = world.ResourceNodes.FirstOrDefault(n => n.Id == ResourceNodeId && n.RemainingAmount > 0);
        if (person is null || node is null)
        {
            return;
        }

        var consumed = Math.Min(node.RemainingAmount, HarvestAmount);
        node.RemainingAmount -= consumed;
        person.Needs.Hunger = Math.Max(0f, person.Needs.Hunger - consumed);
    }
}
