using ManyWinters.Core.Items;
using ManyWinters.Core.Population;

namespace ManyWinters.Core.World;

public static class Decay
{
    // The two passes after the creature loop: a dead animal's bones finally leaving the world,
    // then everything that spoils.
    public static void Advance(WorldState world, long currentTick)
    {
        var rules = world.Configuration.Rules;
        var itemCatalog = world.Configuration.ItemCatalog;

        // A year further on, a dead animal's bones themselves are gone - a person's never
        // are. Snapshotted: removing an animal mutates the list mid-iteration otherwise.
        foreach (var animal in world.Animals.ToList())
        {
            if (animal.IsAlive || animal.DeathTick is not { } deathTick)
            {
                continue;
            }

            if (currentTick - deathTick == rules.CorpseDecayTicks + rules.BonesLingerTicks)
            {
                world.RemoveAnimal(animal);
            }
        }

        // Spoilage: every stack and worked object, in every creature's pack (alive or dead -
        // a corpse's meat rots on its own clock, not at CorpseDecayTicks), in every
        // building's storage, and on every ground pile or Made thing lying loose. What a
        // resource node itself holds never spoils - it grows.
        foreach (var creature in world.People.Cast<Creature>().Concat(world.Animals))
        {
            creature.Inventory.Expire(currentTick, itemCatalog);
        }

        foreach (var entity in world.Entities.ToList())
        {
            entity.Storage?.Expire(currentTick, itemCatalog);

            if (entity.Made is { } made
                && itemCatalog.ShelfLifeTicksOf(made) is { } madeShelfLife
                && currentTick - made.MadeTick >= madeShelfLife)
            {
                world.RemoveEntity(entity);
                continue;
            }

            if (entity is { Category: EntityCategory.Pile, Made: null, StaticAmount: > 0, DroppedTick: { } droppedTick }
                && itemCatalog.ShelfLifeFor(new ItemKindId(entity.Kind.Value)) is { } pileShelfLife
                && currentTick - droppedTick >= pileShelfLife)
            {
                entity.StaticAmount = 0;
                world.RemoveEntity(entity);
            }
        }
    }
}
