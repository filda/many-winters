using ManyWinters.Core.Items;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Gathering food no longer relieves hunger directly (see GatherCommand) - it only fills the
// gatherer's inventory, so something has to spend it back down again. Eats just enough of the
// given food item to reach zero hunger, or all of it if there isn't that much - not a fixed
// amount, since a UI "Eat" action shouldn't need the caller to first work out how much hunger
// is left to satisfy.
public sealed record EatCommand(PersonId PersonId, ItemKindId FoodItem) : ICommand
{
    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        if (person is null || person.Needs.Hunger <= 0f)
        {
            return;
        }

        var restoredPerUnit = world.ItemCatalog.HungerRestoredPerUnitFor(FoodItem);
        if (restoredPerUnit <= 0f)
        {
            return;
        }

        var available = person.Inventory.Get(FoodItem);
        var unitsNeeded = (int)MathF.Ceiling(person.Needs.Hunger / restoredPerUnit);
        var unitsEaten = Math.Min(available, unitsNeeded);
        if (unitsEaten <= 0)
        {
            return;
        }

        person.Inventory.Remove(FoodItem, unitsEaten);
        person.Needs.Hunger = Math.Max(0f, person.Needs.Hunger - (unitsEaten * restoredPerUnit));
    }
}
