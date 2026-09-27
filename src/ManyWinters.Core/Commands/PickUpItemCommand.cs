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

        // Either tier will do - a count of something, or one thing somebody made. Neither means
        // there is nothing there to pick up.
        if (Pile.Made is null && Pile.StaticAmount is null or <= 0)
        {
            return ActionBlocker.TargetIsGone;
        }

        return WorldState.Distance(Person.Position, Pile.Position) <= world.Configuration.Rules.PileReachDistance
            ? ActionBlocker.None
            : ActionBlocker.TooFar;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        if (Pile.Made is { } made)
        {
            // Whole or not at all, as off a body (see LootCommand): what will not fit stays
            // where it lies rather than being half-taken.
            if (Person.Inventory.AddAssemblyIfItFits(made, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Person)))
            {
                world.RemoveEntity(Pile);
            }

            return;
        }

        var item = new ItemKindId(Pile.Kind.Value);

        // Only what fits comes off the pile; the rest stays on the ground (see
        // LootCommand.Execute, the same shape for a corpse's inventory). Carries the pile's own
        // age into the pack rather than restamping it "now" (docs/todo/fauna-plan.md phase 4c).
        // The fallback is a real, reachable path, not defensive dead code: SaveGameService.Load
        // does not reject an older save by Version, and a pile saved before DroppedTick existed
        // deserializes with it null (same nullable-default backward compatibility every other
        // field added since version 1 uses) - such a pile is read as "just found", the same
        // forgiveness an untimed Add gives stock nobody ever dated.
        var taken = Person.Inventory.AddUpToCapacity(
            item,
            Pile.StaticAmount!.Value,
            world.Configuration.ItemCatalog,
            world.MaxCarryWeightFor(Person),
            Pile.DroppedTick ?? world.Clock.CurrentTick);
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
