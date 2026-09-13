using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record CraftCommand(Person Person, ItemKindId Output) : ICommand
{
    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        var recipe = world.Configuration.RecipeCatalog.Get(Output);
        return Person.Inventory.Get(recipe.InputItem) < recipe.InputAmount
            ? ActionBlocker.MissingMaterials
            : ActionBlocker.None;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        var recipe = world.Configuration.RecipeCatalog.Get(Output);
        Person.Inventory.Remove(recipe.InputItem, recipe.InputAmount);
        Person.Inventory.Add(Output, 1);
    }
}
