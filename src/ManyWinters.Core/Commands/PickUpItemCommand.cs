using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record PickUpItemCommand(Person Person, ItemPile Pile) : ICommand
{
    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (Pile.Amount <= 0)
        {
            return ActionBlocker.TargetIsGone;
        }

        return world.IsWithinReach(Person.Position, Pile.Position)
            ? ActionBlocker.None
            : ActionBlocker.TooFar;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        // Only what fits comes off the pile; the rest stays on the ground (see
        // LootCommand.Execute, the same shape for a corpse's inventory).
        var taken = Person.Inventory.AddUpToCapacity(Pile.Kind, Pile.Amount, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Person));
        if (taken > 0)
        {
            Pile.Amount -= taken;
        }

        if (Pile.Amount <= 0)
        {
            world.RemoveItemPile(Pile);
        }
    }
}
