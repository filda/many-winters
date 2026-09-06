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

        // Unlike Deposit (a building's own storage stays uncapped - see
        // WorldState.MaxCarryWeightFor's own doc comment), withdrawing goes into the person's
        // limited inventory - whatever doesn't fit goes right back into the building rather
        // than being destroyed.
        var added = Person.Inventory.AddUpToCapacity(Item, Amount, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Person));
        if (added < Amount)
        {
            Building.Inventory.Add(Item, Amount - added);
        }
    }
}
