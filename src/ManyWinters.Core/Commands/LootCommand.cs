using ManyWinters.Core.Population;
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

        // Only what fits comes off the corpse - a looter who's already full leaves the rest
        // behind (still lootable later, e.g. by someone else) rather than it vanishing.
        foreach (var (item, count) in deceased.Inventory.Counts.ToList())
        {
            var taken = lootingPerson.Inventory.AddUpToCapacity(item, count, world.ItemCatalog, Person.MaxCarryWeight);
            if (taken > 0)
            {
                deceased.Inventory.Remove(item, taken);
            }
        }
    }
}
