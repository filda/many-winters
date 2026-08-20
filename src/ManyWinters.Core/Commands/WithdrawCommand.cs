using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record WithdrawCommand(PersonId PersonId, BuildingId BuildingId, ItemKindId Item, int Amount) : ICommand
{
    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        var building = world.Buildings.FirstOrDefault(b => b.Id == BuildingId);
        if (person is null || building is null || WorldState.Distance(person.Position, building.Position) > WorldState.MaxInteractionDistance)
        {
            return;
        }

        if (!building.Inventory.Remove(Item, Amount))
        {
            return;
        }

        person.Inventory.Add(Item, Amount);
    }
}
