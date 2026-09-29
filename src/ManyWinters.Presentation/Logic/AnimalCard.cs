using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Presentation.Logic;

// Everything the selection panel says about an animal that is selected - the same idea as the
// person's own card, narrowed to what there is to say about one: no pack, no knowledge, no
// actions - nothing can be done to a deer yet.
//
// Carcass is empty for the living - there is nothing on a deer to take yet, and the panel hides
// the line rather than show it blank. For the dead it is what butchering has not yet taken off,
// so the player can tell at a glance whether the trip is still worth the walk.
internal sealed record AnimalCard(string Title, string Beside, string Task, MeterReading Fed, string Carcass)
{
    internal static AnimalCard For(WorldState world, Animal animal)
    {
        var lifeCycle = world.Configuration.SpeciesCatalog.Get(animal.Species).LifeCycle;

        return new AnimalCard(
            world.Configuration.SpeciesCatalog.Get(animal.Species).DisplayName,
            animal.IsAlive
                ? InspectorText.ForAgeAndSex(world.AgeInYears(animal), lifeCycle, animal.Sex)
                // As the person's own card words it: once decayed there is nothing left to call
                // it but its bones.
                : world.IsDecayed(animal) ? "Bones" : "deceased",
            animal.IsAlive ? InspectorText.ForTask(animal) : string.Empty,
            SelectionCard.FedFor(animal, world.Configuration.Rules.HungerSeekFoodThreshold),
            animal.IsAlive ? string.Empty : CarcassLine(world.Configuration.ItemCatalog, animal));
    }

    // What is left to butcher, named the way the rest of the panel names things and sorted by
    // item id, not by the order butchering happens to take things off in - so the line does not
    // reshuffle between one look and the next.
    private static string CarcassLine(ItemCatalog items, Animal carcass)
    {
        var parts = carcass.Inventory.Counts
            .Where(entry => entry.Value > 0)
            .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
            .Select(entry => $"{items.Get(entry.Key).DisplayName} {entry.Value}")
            .ToList();

        return parts.Count > 0 ? $"Carcass: {string.Join(", ", parts)}" : "Nothing left";
    }
}
