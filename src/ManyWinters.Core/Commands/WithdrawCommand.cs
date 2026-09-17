using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record WithdrawCommand(Person Person, Entity Building, ItemKindId Item, int Amount) : ICommand
{
    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (!world.IsWithinReach(Person.Position, Building.Position))
        {
            return ActionBlocker.TooFar;
        }

        // The store's shortage, not the person's - the two read differently to a player standing
        // at an empty hut (see ActionBlocker.MissingMaterials).
        return Building.Storage!.Get(Item) < Amount ? ActionBlocker.StoreIsEmpty : ActionBlocker.None;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        Building.Storage!.Remove(Item, Amount);

        // Unlike Deposit into a building's uncapped storage, this goes into the person's capped
        // inventory (see WorldState.MaxCarryWeightFor); what does not fit goes back into the
        // building.
        var added = Person.Inventory.AddUpToCapacity(Item, Amount, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Person));
        if (added < Amount)
        {
            Building.Storage!.Add(Item, Amount - added);
        }
    }
}
