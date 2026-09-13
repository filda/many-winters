using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record LootCommand(Person LootingPerson, Person Deceased) : ICommand
{
    public ActionBlocker Blocker(WorldState world)
    {
        if (!LootingPerson.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (Deceased.IsAlive)
        {
            return ActionBlocker.TargetIsAlive;
        }

        return world.IsWithinReach(LootingPerson.Position, Deceased.Position)
            ? ActionBlocker.None
            : ActionBlocker.TooFar;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        // Only what fits comes off the corpse; the rest stays lootable. Capacity is recomputed
        // per iteration: looting a basket partway through raises the room for what follows.
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
