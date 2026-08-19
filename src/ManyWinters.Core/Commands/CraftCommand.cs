using ManyWinters.Core.Items;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record CraftCommand(PersonId PersonId, ItemKindId Output) : ICommand
{
    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        if (person is null)
        {
            return;
        }

        var recipe = world.RecipeCatalog.Get(Output);
        if (!person.Inventory.Remove(recipe.InputItem, recipe.InputAmount))
        {
            return;
        }

        person.Inventory.Add(Output, 1);
    }
}
