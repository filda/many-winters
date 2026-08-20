using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record LootCommand(PersonId LootingPersonId, PersonId DeceasedPersonId) : ICommand
{
    public void Execute(WorldState world)
    {
        var lootingPerson = world.People.FirstOrDefault(p => p.Id == LootingPersonId && p.IsAlive);
        var deceased = world.People.FirstOrDefault(p => p.Id == DeceasedPersonId && !p.IsAlive);
        if (lootingPerson is null
            || deceased is null
            || WorldState.Distance(lootingPerson.Position, deceased.Position) > WorldState.MaxInteractionDistance)
        {
            return;
        }

        foreach (var (item, count) in deceased.Inventory.Counts.ToList())
        {
            deceased.Inventory.Remove(item, count);
            lootingPerson.Inventory.Add(item, count);
        }
    }
}
