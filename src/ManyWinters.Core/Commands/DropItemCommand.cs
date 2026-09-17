using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record DropItemCommand(Person Person, ItemKindId Item, int Amount) : ICommand
{
    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
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
        world.AddEntity(new Entity
        {
            Kind = new EntityKindId(Item.Value),
            Category = EntityCategory.Pile,
            Position = Person.Position,
            StaticAmount = Amount,
        });
    }
}
