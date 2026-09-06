using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record LootCommand(Person LootingPerson, Person Deceased) : ICommand
{
    public void Execute(WorldState world)
    {
        if (!LootingPerson.IsAlive
            || Deceased.IsAlive
            || !world.IsWithinReach(LootingPerson.Position, Deceased.Position))
        {
            return;
        }

        // Only what fits comes off the corpse - a looter who's already full leaves the rest
        // behind (still lootable later, e.g. by someone else) rather than it vanishing.
        // Recomputed every iteration, not hoisted: looting a capacity-boosting item (a
        // basket) partway through should raise the room left for whatever's looted next.
        foreach (var (item, count) in Deceased.Inventory.Counts.ToList())
        {
            var taken = LootingPerson.Inventory.AddUpToCapacity(item, count, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(LootingPerson));
            // Stryker disable once Equality: removing zero units leaves the count exactly as it
            // was, so skipping the call and making it are indistinguishable
            if (taken > 0)
            {
                Deceased.Inventory.Remove(item, taken);
            }
        }
    }
}
