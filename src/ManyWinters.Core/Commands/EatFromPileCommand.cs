using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Eating straight off a pile somebody put down, the ground's counterpart of GatherCommand's
// eating at the source. Taking from a pile needs no skill (see PickUpItemCommand), so knowing
// how to eat is enough: a band can live off what one picker brings back. Only what the meal
// needs comes off the pile, and the rest stays there for the next hungry person.
public sealed record EatFromPileCommand(Person Person, Entity Pile) : ICommand
{
    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (Pile.StaticAmount is not { } amount || amount <= 0)
        {
            return ActionBlocker.TargetIsGone;
        }

        return world.IsWithinReach(Person.Position, Pile.Position)
            ? EatCommand.EatingBlocker(world, Person, FoodOf(Pile), amount)
            : ActionBlocker.TooFar;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        Pile.StaticAmount -= EatCommand.Eat(world, Person, FoodOf(Pile), Pile.StaticAmount!.Value);
        if (Pile.StaticAmount <= 0)
        {
            world.RemoveEntity(Pile);
        }
    }

    // A pile of stock carries the item's own kind (see DropCommand).
    public static ItemKindId FoodOf(Entity pile) => new(pile.Kind.Value);
}
