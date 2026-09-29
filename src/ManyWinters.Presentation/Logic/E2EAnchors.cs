using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Presentation.Logic;

// Which person, resource node and animal the E2E suite's "E2E anchor" log lines, printed once
// the prologue is dismissed, describe. The rule for each - "the first living person", "the
// nearest wood-yielding resource node to the camp", "the nearest living animal" - is picked here
// so it is unit-testable without the engine; turning the pick into a screen pixel needs a
// Camera3D and belongs in the engine layer.
public static class E2EAnchors
{
    // Whoever the golden-path tests already assume they can select - stable regardless of how
    // far people have wandered from the camp anchor.
    public static Person? FirstLivingPerson(IReadOnlyList<Person> people) =>
        people.FirstOrDefault(person => person.IsAlive);

    // "The wood pile in the camp" the crafting/building golden-path tests send a person to
    // gather from (one "wood" node spawns beside the camp centre) - found by what it yields, not
    // by its kind id, so a second wood-yielding kind placed nearer camp is still picked correctly.
    public static Entity? NearestWoodResourceNode(IReadOnlyList<Entity> entities, ResourceCatalog resourceCatalog, Position campCenter)
    {
        var wood = new ItemKindId("wood");
        return entities
            .Where(entity => entity.Category == EntityCategory.Growable && resourceCatalog.Get(entity.Kind).YieldsItem == wood)
            .OrderBy(entity => WorldState.Distance(entity.Position, campCenter))
            .FirstOrDefault();
    }

    // For the deer tests not written yet - costs nothing to have ready.
    public static Animal? NearestLivingAnimal(IReadOnlyList<Animal> animals, Position campCenter) =>
        animals
            .Where(animal => animal.IsAlive)
            .OrderBy(animal => WorldState.Distance(animal.Position, campCenter))
            .FirstOrDefault();
}
