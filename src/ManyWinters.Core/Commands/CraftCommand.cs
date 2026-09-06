using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record CraftCommand(Person Person, ItemKindId Output) : ICommand
{
    public void Execute(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return;
        }

        var recipe = world.Configuration.RecipeCatalog.Get(Output);
        if (!Person.Inventory.Remove(recipe.InputItem, recipe.InputAmount))
        {
            return;
        }

        Person.Inventory.Add(Output, 1);
    }
}
