using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// Everything the selection panel says about an animal that is selected - the same idea as
// SelectionCard, narrowed to what there is to say about one (docs/todo/fauna-plan.md, phase 2b):
// no pack, no knowledge, no actions - nothing can be done to a deer yet.
internal sealed record AnimalCard(string Title, string Beside, string Task, MeterReading Fed)
{
    internal static AnimalCard For(WorldState world, Animal animal)
    {
        var lifeCycle = world.Configuration.SpeciesCatalog.Get(animal.Species).LifeCycle;

        return new AnimalCard(
            world.Configuration.SpeciesCatalog.Get(animal.Species).DisplayName,
            animal.IsAlive ? InspectorText.ForAgeAndSex(world.AgeInYears(animal), lifeCycle, animal.Sex) : "deceased",
            animal.IsAlive ? InspectorText.ForTask(animal) : string.Empty,
            SelectionCard.FedFor(animal, world.Configuration.Rules.HungerSeekFoodThreshold));
    }
}
