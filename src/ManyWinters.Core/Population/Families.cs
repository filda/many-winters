using ManyWinters.Core.Commands;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

public static class Families
{
    // Affections first, then people's own families, then animal breeding - the order Advance
    // itself used before this moved out.
    public static void Advance(WorldState world, long currentTick, Climate climate)
    {
        AdvanceAffections(world);
        StartFamilies(world, currentTick);
        BreedAnimals(world, currentTick, climate);
    }

    // Time together grows a bond, time apart loses it, for every living pair every tick (O(n^2),
    // negligible at tens of people). Pairs involving the dead are skipped rather than decayed,
    // so what someone meant to others is still there to read after they are gone.
    private static void AdvanceAffections(WorldState world)
    {
        var rules = world.Configuration.Rules;
        var people = world.People;
        for (var i = 0; i < people.Count; i++)
        {
            var first = people[i];
            if (!first.IsAlive)
            {
                continue;
            }

            for (var j = i + 1; j < people.Count; j++)
            {
                var second = people[j];
                if (!second.IsAlive)
                {
                    continue;
                }

                var together = WorldState.Distance(first.Position, second.Position) <= rules.TogetherDistance;
                var delta = together ? rules.AffectionGainedPerTickTogether : -rules.AffectionLostPerTickApart;
                world.Affections.Change(first.Id, second.Id, delta, rules.MaxAffection);
            }
        }
    }

    // Where children come from when nobody asks. Whether a birth is possible is BirthCommand's
    // business; this pass adds only the bond threshold. Iterates a snapshot because BirthCommand
    // adds to People: a child must not become a candidate parent on the tick it is born.
    private static void StartFamilies(WorldState world, long currentTick)
    {
        var threshold = world.Configuration.Rules.AffectionNeededToHaveAChild;
        var candidates = world.People.Where(person => person.IsAlive && world.IsOldEnoughForChildren(person)).ToList();

        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                var first = candidates[i];
                var second = candidates[j];
                if (world.Affections.Between(first.Id, second.Id) < threshold)
                {
                    continue;
                }

                var mother = first.Sex == Sex.Female ? first : second;
                var father = ReferenceEquals(mother, first) ? second : first;

                // Alive, grown, one of each sex, not kin, within reach, mother not nursing - all
                // checked inside; it declines silently like any other command.
                new BirthCommand(world.Naming.NameForNewborn(mother, father, currentTick), mother, father).Execute(world);
            }
        }
    }

    // Where fawns come from when nobody asks - the animal counterpart of StartFamilies, but no
    // pair state: a female's own id and the tick decide everything. Iterates a snapshot of
    // Animals because giving birth adds to it, the same "a child must not become a candidate on
    // the tick it is born" guard StartFamilies uses.
    private static void BreedAnimals(WorldState world, long currentTick, Climate climate)
    {
        var mothers = world.Animals.Where(animal => animal.IsAlive && animal.Sex == Sex.Female).ToList();

        foreach (var mother in mothers)
        {
            if (world.Configuration.SpeciesCatalog.Get(mother.Species).Breeding is not { } breeding)
            {
                continue;
            }

            if (mother.PregnantSinceTick is { } pregnantSinceTick)
            {
                if (currentTick - pregnantSinceTick >= breeding.GestationTicks)
                {
                    GiveBirth(world, mother, currentTick);
                }

                continue;
            }

            if (world.LifeStageOf(mother) != LifeStage.Adult
                || world.NursingInfantOf(mother) is not null
                || mother.Needs.Hunger >= breeding.SatietyHungerBelow
                || climate != breeding.Climate
                || !HasAdultMaleOfHerOwnSpeciesAtHome(world, mother))
            {
                continue;
            }

            if (PassesConceptionRoll(mother, currentTick, breeding.ConceptionChancePerTick))
            {
                mother.PregnantSinceTick = currentTick;
            }
        }
    }

    // Same HomeRange reference, not merely nearby - a herd shares one anchor, so "at home" is
    // exactly "in this herd" rather than a distance check.
    private static bool HasAdultMaleOfHerOwnSpeciesAtHome(WorldState world, Animal mother) =>
        world.Animals.Any(candidate =>
            candidate.IsAlive
            && candidate.Sex == Sex.Male
            && candidate.Species == mother.Species
            && ReferenceEquals(candidate.Home, mother.Home)
            && world.LifeStageOf(candidate) == LifeStage.Adult);

    // What spawning does for a fawn born mid-game rather than drawn at map load: same position
    // and Home as its mother, her as Mother, sex off its own freshly drawn id, and whatever the
    // species starts every newborn knowing.
    private static void GiveBirth(WorldState world, Animal mother, long currentTick)
    {
        var id = NewbornIdFor(mother.Id, currentTick);
        world.Execute(new SpawnAnimalCommand(id, mother.Species, mother.Position, mother.Home, Creature.SexOf(id), currentTick, mother));
        mother.PregnantSinceTick = null;
    }

    // Deterministic from the mother's own id and the tick, as a newborn's name is drawn from both
    // parents' ids and the tick - a fawn has only one parent in this rule, so her id alone is the
    // seed.
    private static CreatureId NewbornIdFor(CreatureId motherId, long currentTick)
    {
        var mixed = unchecked((uint)(motherId.Seed * 2654435761u) ^ ((uint)currentTick * 40503u));
        return CreatureId.New(new Random(SeedHash.Avalanche(mixed)));
    }

    // Deterministic from the mother and the tick, as every other roll is.
    private static bool PassesConceptionRoll(Animal mother, long currentTick, float chance)
    {
        var mixed = unchecked((uint)(mother.Id.Seed * 73856093) ^ ((uint)currentTick * 19349663u));

        // Stryker disable once Equality: NextDouble() returning exactly `chance` has probability
        // zero, so < and <= are the same roll
        return new Random(SeedHash.Avalanche(mixed)).NextDouble() < chance;
    }
}
