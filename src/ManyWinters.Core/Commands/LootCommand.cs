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

        // What they made comes off the body first. A worked thing is the one thing the band
        // cannot simply gather again - somebody's winter of practice is in it - and a pack filled
        // with the corpse's firewood would leave no room for the axe lying beside it.
        foreach (var made in Deceased.Inventory.Assemblies.ToList())
        {
            if (LootingPerson.Inventory.AddAssemblyIfItFits(made, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(LootingPerson)))
            {
                Deceased.Inventory.RemoveAssembly(made);
            }
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
