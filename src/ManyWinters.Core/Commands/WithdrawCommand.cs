using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record WithdrawCommand(Person Person, Building Building, ItemKindId Item, int Amount) : ICommand
{
    public void Execute(WorldState world)
    {
        if (!Person.IsAlive || !world.IsWithinReach(Person.Position, Building.Position))
        {
            return;
        }

        if (!Building.Inventory.Remove(Item, Amount))
        {
            return;
        }

        // Unlike Deposit into a building's uncapped storage, this goes into the person's capped
        // inventory (see WorldState.MaxCarryWeightFor); what does not fit goes back into the
        // building.
        var added = Person.Inventory.AddUpToCapacity(Item, Amount, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Person));
        if (added < Amount)
        {
            Building.Inventory.Add(Item, Amount - added);
        }
    }
}
