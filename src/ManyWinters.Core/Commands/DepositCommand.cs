using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record DepositCommand(Person Person, Building Building, ItemKindId Item, int Amount) : ICommand
{
    public void Execute(WorldState world)
    {
        if (!Person.IsAlive || !world.IsWithinReach(Person.Position, Building.Position))
        {
            return;
        }

        if (!Person.Inventory.Remove(Item, Amount))
        {
            return;
        }

        Building.Inventory.Add(Item, Amount);
    }
}
