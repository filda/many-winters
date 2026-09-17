using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record DepositCommand(Person Person, Entity Building, ItemKindId Item, int Amount) : ICommand
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

        return Person.Inventory.Get(Item) < Amount ? ActionBlocker.MissingMaterials : ActionBlocker.None;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        Person.Inventory.Remove(Item, Amount);
        Building.Storage!.Add(Item, Amount);
    }
}
