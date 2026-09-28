using System.Text.Json;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Persistence;

public static class SaveGameService
{
    private const int CurrentVersion = 28;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Stryker disable once Boolean: cosmetic formatting only, doesn't affect round-trip behavior
        WriteIndented = true,
    };

    private static SaveData ToSaveData(WorldState world)
    {
        var people = world.People.Select(ToPersonSaveData).ToList();
        var forebears = world.Forebears.Select(ToPersonSaveData).ToList();

        var entities = world.Entities
            .Select(entity => new EntitySaveData(
                entity.Id.Value,
                entity.Kind,
                entity.Category,
                entity.Position.X,
                entity.Position.Y,
                entity.Growth is { } growth
                    ? new GrowthSaveData(growth.RemainingAmount, growth.MaxAmount, growth.IsAlive, growth.DeathTick, growth.CauseOfDeath, growth.ColdStress)
                    : null,
                entity.StaticAmount,
                entity.Condition,
                entity.Storage?.Counts.Select(kv => new ItemStackSaveData(kv.Key, kv.Value, ToAgedEntries(entity.Storage, kv.Key))).ToList(),
                entity.Made is { } made ? ToAssemblySaveData(made) : null,
                entity.Storage?.Assemblies.Select(ToAssemblySaveData).ToList(),
                entity.DroppedTick))
            .ToList();

        var graves = world.Graves
            .Select(grave => new GraveSaveData(
                grave.Id.Value,
                grave.Position.X,
                grave.Position.Y,
                grave.IsMarked,
                grave.Name,
                grave.Sex,
                grave.AgeAtDeath,
                grave.CauseOfDeath,
                grave.MotherName,
                grave.FatherName,
                grave.KnownTechniques))
            .ToList();

        var exploredCells = world.Exploration.Explored
            .Select(cell => new ExplorationCellSaveData(cell.X, cell.Y))
            .ToList();

        var affections = world.Affections.All
            .Select(bond => new AffectionSaveData(bond.A.Value, bond.B.Value, bond.Value))
            .ToList();

        var vocabulary = world.Vocabulary.Words
            .Select(word => new WordSaveData(word.Key, word.Value))
            .ToList();

        var homeRanges = world.HomeRanges
            .Select(home => new HomeRangeSaveData(home.Id.Value, home.Anchor.X, home.Anchor.Y, home.Radius, home.DriftMetresPerSeason))
            .ToList();

        var animals = world.Animals.Select(ToAnimalSaveData).ToList();

        return new SaveData(
            CurrentVersion,
            world.Clock.CurrentTick,
            people,
            forebears,
            entities,
            graves,
            exploredCells,
            affections,
            vocabulary,
            animals,
            homeRanges);
    }

    private static AnimalSaveData ToAnimalSaveData(Animal animal) => new(
        animal.Id.Value,
        animal.Species,
        animal.Position.X,
        animal.Position.Y,
        animal.IsAlive,
        animal.Needs.Hunger,
        animal.Needs.Fatigue,
        animal.Skills.Levels.Select(kv => new SkillLevelSaveData(kv.Key, kv.Value)).ToList(),
        animal.KnownTechniques.ToList(),
        animal.BirthTick,
        animal.DeathTick,
        animal.CauseOfDeath,
        animal.Sex,
        animal.Home.Id.Value,
        animal.Mother?.Id.Value,
        animal.PregnantSinceTick,
        animal.Inventory.Counts.Select(kv => new ItemStackSaveData(kv.Key, kv.Value, ToAgedEntries(animal.Inventory, kv.Key))).ToList());

    private static PersonSaveData ToPersonSaveData(Person person) => new(
        person.Id.Value,
        person.Name,
        person.Position.X,
        person.Position.Y,
        person.IsAlive,
        person.Needs.Hunger,
        person.Needs.Fatigue,
        person.Skills.Levels.Select(kv => new SkillLevelSaveData(kv.Key, kv.Value)).ToList(),
        person.KnownTechniques.ToList(),
        person.Beliefs.Held
            .Select(held => new BeliefSaveData(held.Key.Material, held.Key.Property, held.Value.Value, held.Value.Confidence))
            .ToList(),
        person.Inventory.Counts.Select(kv => new ItemStackSaveData(kv.Key, kv.Value, ToAgedEntries(person.Inventory, kv.Key))).ToList(),
        person.Inventory.Assemblies.Select(ToAssemblySaveData).ToList(),
        person.BirthTick,
        person.DeathTick,
        person.CauseOfDeath,
        person.IsBuried,
        person.Mother.Id.Value,
        person.Father.Id.Value,
        person.Sex,
        person.Curiosity,
        person.Home.Id.Value);

    // Recursive both ways, because an assembly is: a bound thing holds two more of them, to any
    // depth.
    private static AssemblySaveData ToAssemblySaveData(Assembly assembly) => assembly switch
    {
        Assembly.Part part => new AssemblySaveData(new PartSaveData(part.Material, part.Form, part.Quality, part.Volume, part.MadeTick), null),
        Assembly.Joined joined => new AssemblySaveData(
            null,
            new JointSaveData(
                joined.JointStrength,
                joined.JointWeight,
                ToAssemblySaveData(joined.Left),
                ToAssemblySaveData(joined.Right),
                joined.MadeTick)),
        _ => throw new ArgumentOutOfRangeException(nameof(assembly), assembly, "Unknown kind of worked thing."),
    };

    private static Assembly FromAssemblySaveData(AssemblySaveData data) => data switch
    {
        { Part: { } part } => new Assembly.Part(part.Material, part.Form, part.Quality, part.Volume) { MadeTick = part.MadeTick },
        { Joint: { } joint } => new Assembly.Joined(
            joint.Strength,
            joint.Weight,
            FromAssemblySaveData(joint.Left),
            FromAssemblySaveData(joint.Right))
        {
            MadeTick = joint.MadeTick,
        },
        _ => throw new InvalidDataException("A worked thing in the save states neither a part nor a joint."),
    };

    // The FIFO age ledger for one kind - null if that kind carries no age at all (non-perishable,
    // or added untimed), so an unremarkable stack does not grow a pointless empty list in every
    // save.
    private static List<AgedEntrySaveData>? ToAgedEntries(Inventory? inventory, ItemKindId kind) =>
        inventory is not null && inventory.Ages.TryGetValue(kind, out var entries) && entries.Count > 0
            ? entries.Select(entry => new AgedEntrySaveData(entry.Tick, entry.Count)).ToList()
            : null;

    private static WorldState FromSaveData(SaveData data, WorldConfiguration configuration)
    {
        var world = new WorldState(configuration);
        world.Clock.Advance(data.Tick);

        // Built before any person, unlike Animal's own home ranges below: a person's Home is
        // resolved while restoring the person, not in a second pass, so it has to exist first.
        var homeRangesById = new Dictionary<Guid, HomeRange>();
        foreach (var homeRangeData in data.HomeRanges)
        {
            var homeRange = new HomeRange(new Position(homeRangeData.AnchorX, homeRangeData.AnchorY))
            {
                Id = new HomeRangeId(homeRangeData.Id),
                Radius = homeRangeData.Radius,
                DriftMetresPerSeason = homeRangeData.DriftMetresPerSeason,
            };
            world.RestoreHomeRange(homeRange);
            homeRangesById[homeRangeData.Id] = homeRange;
        }

        // Parents have to exist before their children: forebears first (children of Unknown
        // only), then people in save order, a parent always having been added before its child.
        var peopleById = new Dictionary<Guid, Person> { [Person.Unknown.Id.Value] = Person.Unknown };
        foreach (var forebearData in data.Forebears)
        {
            world.RestoreForebear(RestorePerson(forebearData, peopleById, homeRangesById, configuration.Rules));
        }

        foreach (var personData in data.People)
        {
            world.RestorePerson(RestorePerson(personData, peopleById, homeRangesById, configuration.Rules));
        }

        foreach (var entityData in data.Entities)
        {
            var entity = new Entity
            {
                Id = new EntityId(entityData.Id),
                Kind = entityData.Kind,
                Category = entityData.Category,
                Position = new Position(entityData.PositionX, entityData.PositionY),
                Growth = entityData.Growth is { } growthData
                    ? new GrowthState
                    {
                        RemainingAmount = growthData.RemainingAmount,
                        MaxAmount = growthData.MaxAmount,
                        IsAlive = growthData.IsAlive,
                        DeathTick = growthData.DeathTick,
                        CauseOfDeath = growthData.CauseOfDeath,
                        ColdStress = growthData.ColdStress,
                    }
                    : null,
                StaticAmount = entityData.StaticAmount,
                Condition = entityData.Condition,
                Storage = entityData.Storage is not null ? new Inventory() : null,
                Made = entityData.Made is { } made ? FromAssemblySaveData(made) : null,
                DroppedTick = entityData.DroppedTick,
            };

            if (entityData.Storage is { } storage)
            {
                foreach (var stack in storage)
                {
                    entity.Storage!.Add(stack.Kind, stack.Count);
                    foreach (var age in stack.Ages ?? [])
                    {
                        entity.Storage!.RestoreAgedEntry(stack.Kind, age.Tick, age.Count);
                    }
                }
            }

            foreach (var thing in entityData.StorageWorkedThings ?? [])
            {
                entity.Storage!.AddAssembly(FromAssemblySaveData(thing));
            }

            world.RestoreEntity(entity);
        }

        foreach (var graveData in data.Graves)
        {
            var grave = new Grave
            {
                Id = new GraveId(graveData.Id),
                Position = new Position(graveData.PositionX, graveData.PositionY),
                IsMarked = graveData.IsMarked,
                Name = graveData.Name,
                Sex = graveData.Sex,
                AgeAtDeath = graveData.AgeAtDeath,
                CauseOfDeath = graveData.CauseOfDeath,
                MotherName = graveData.MotherName,
                FatherName = graveData.FatherName,
                KnownTechniques = graveData.KnownTechniques,
            };

            world.RestoreGrave(grave);
        }

        world.Exploration.RestoreExplored(data.ExploredCells.Select(cell => new ExplorationCell(cell.X, cell.Y)));

        foreach (var word in data.Vocabulary)
        {
            world.Vocabulary.Restore(word.PatternSignature, word.Word);
        }

        foreach (var bond in data.Affections)
        {
            world.Affections.Set(new CreatureId(bond.PersonA), new CreatureId(bond.PersonB), bond.Value);
        }

        // A mother always precedes her young in save order (world.Animals is insertion order),
        // the same parents-before-children guarantee relied on above.
        var animalsById = new Dictionary<Guid, Animal>();
        foreach (var animalData in data.Animals)
        {
            world.RestoreAnimal(RestoreAnimal(animalData, homeRangesById, animalsById, configuration.Rules));
        }

        return world;
    }

    private static Animal RestoreAnimal(
        AnimalSaveData animalData,
        Dictionary<Guid, HomeRange> homeRangesById,
        Dictionary<Guid, Animal> animalsById,
        SimulationRules rules)
    {
        var id = new CreatureId(animalData.Id);
        var animal = new Animal(animalData.Species, homeRangesById[animalData.HomeRangeId])
        {
            Id = id,
            Position = new Position(animalData.PositionX, animalData.PositionY),
            IsAlive = animalData.IsAlive,
            BirthTick = animalData.BirthTick,
            DeathTick = animalData.DeathTick,
            CauseOfDeath = animalData.CauseOfDeath,
            Sex = animalData.Sex,
            MaxHunger = rules.MaxHungerFor(id),
            Mother = animalData.MotherId is { } motherId ? animalsById[motherId] : null,
            PregnantSinceTick = animalData.PregnantSinceTick,
        };
        animal.Needs.Hunger = animalData.Hunger;
        animal.Needs.Fatigue = animalData.Fatigue;

        foreach (var skillData in animalData.Skills)
        {
            animal.Skills.Restore(skillData.Type, skillData.Level);
        }

        foreach (var technique in animalData.KnownTechniques)
        {
            animal.KnownTechniques.Add(technique);
        }

        foreach (var stack in animalData.Inventory ?? [])
        {
            animal.Inventory.Add(stack.Kind, stack.Count);
            foreach (var age in stack.Ages ?? [])
            {
                animal.Inventory.RestoreAgedEntry(stack.Kind, age.Tick, age.Count);
            }
        }

        animalsById[animalData.Id] = animal;
        return animal;
    }

    private static Person RestorePerson(
        PersonSaveData personData,
        Dictionary<Guid, Person> peopleById,
        Dictionary<Guid, HomeRange> homeRangesById,
        SimulationRules rules)
    {
        var id = new CreatureId(personData.Id);
        var person = new Person
        {
            Id = id,
            Name = personData.Name,
            Position = new Position(personData.PositionX, personData.PositionY),
            IsAlive = personData.IsAlive,
            BirthTick = personData.BirthTick,
            DeathTick = personData.DeathTick,
            CauseOfDeath = personData.CauseOfDeath,
            IsBuried = personData.IsBuried,
            Mother = ParentById(personData.MotherId, peopleById),
            Father = ParentById(personData.FatherId, peopleById),
            Sex = personData.Sex,
            Curiosity = personData.Curiosity,
            Home = homeRangesById[personData.HomeRangeId],

            // Not saved: it is redrawn from the id.
            MaxHunger = rules.MaxHungerFor(id),
        };
        person.Needs.Hunger = personData.Hunger;
        person.Needs.Fatigue = personData.Fatigue;
        foreach (var skillData in personData.Skills)
        {
            person.Skills.Restore(skillData.Type, skillData.Level);
        }

        foreach (var technique in personData.KnownTechniques)
        {
            person.KnownTechniques.Add(technique);
        }

        foreach (var belief in personData.Beliefs)
        {
            person.Beliefs.Restore(belief.Material, belief.Property, belief.Value, belief.Confidence);
        }

        foreach (var stack in personData.Inventory)
        {
            person.Inventory.Add(stack.Kind, stack.Count);
            foreach (var age in stack.Ages ?? [])
            {
                person.Inventory.RestoreAgedEntry(stack.Kind, age.Tick, age.Count);
            }
        }

        foreach (var worked in personData.WorkedThings)
        {
            person.Inventory.AddAssembly(FromAssemblySaveData(worked));
        }

        peopleById[personData.Id] = person;
        return person;
    }

    private static Person ParentById(Guid id, Dictionary<Guid, Person> peopleById) =>
        peopleById.TryGetValue(id, out var parent)
            ? parent
            : throw new InvalidDataException($"Save refers to person {id} as a parent before (or without) saving that person.");

    public static void Save(WorldState world, string path)
    {
        var json = JsonSerializer.Serialize(ToSaveData(world), JsonOptions);
        File.WriteAllText(path, json);
    }

    // A save carries no catalogs; the configuration turns the restored ids back into definitions.
    public static WorldState Load(string path, WorldConfiguration configuration)
    {
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<SaveData>(json, JsonOptions)
            ?? throw new InvalidDataException($"Save file '{path}' could not be parsed.");

        return FromSaveData(data, configuration);
    }
}
