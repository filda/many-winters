using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record PickUpItemCommand(Person Person, Entity Pile) : ICommand
{
    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (Pile.StaticAmount is null or <= 0)
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

        var item = new ItemKindId(Pile.Kind.Value);

        // Only what fits comes off the pile; the rest stays on the ground (see
        // LootCommand.Execute, the same shape for a corpse's inventory).
        var taken = Person.Inventory.AddUpToCapacity(item, Pile.StaticAmount!.Value, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Person));
        if (taken > 0)
        {
            Pile.StaticAmount -= taken;
        }

        if (Pile.StaticAmount <= 0)
        {
            world.RemoveEntity(Pile);
        }
    }
}
