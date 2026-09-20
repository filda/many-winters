using System.Text.Json;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Persistence;

public static class SaveGameService
{
    private const int CurrentVersion = 22;

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
                entity.Storage?.Counts.Select(kv => new ItemStackSaveData(kv.Key, kv.Value)).ToList()))
            .ToList();

        var graves = world.Graves
            .Select(grave => new GraveSaveData(
                grave.Id.Value,
                grave.Position.X,
                grave.Position.Y,
                grave.IsMarked,
                grave.Name,
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

        return new SaveData(
            CurrentVersion,
            world.Clock.CurrentTick,
            people,
            forebears,
            entities,
            graves,
            exploredCells,
            affections);
    }

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
        person.Inventory.Counts.Select(kv => new ItemStackSaveData(kv.Key, kv.Value)).ToList(),
        person.Inventory.Assemblies.Select(ToAssemblySaveData).ToList(),
        person.BirthTick,
        person.DeathTick,
        person.CauseOfDeath,
        person.IsBuried,
        person.Mother.Id.Value,
        person.Father.Id.Value,
        person.Sex,
        person.Curiosity);

    // Recursive both ways, because an assembly is: a bound thing holds two more of them, to any
    // depth (see Assembly).
    private static AssemblySaveData ToAssemblySaveData(Assembly assembly) => assembly switch
    {
        Assembly.Part part => new AssemblySaveData(new PartSaveData(part.Material, part.Form, part.Quality, part.Volume), null),
        Assembly.Joined joined => new AssemblySaveData(
            null,
            new JointSaveData(
                joined.JointStrength,
                joined.JointWeight,
                ToAssemblySaveData(joined.Left),
                ToAssemblySaveData(joined.Right))),
        _ => throw new ArgumentOutOfRangeException(nameof(assembly), assembly, "Unknown kind of worked thing."),
    };

    private static Assembly FromAssemblySaveData(AssemblySaveData data) => data switch
    {
        { Part: { } part } => new Assembly.Part(part.Material, part.Form, part.Quality, part.Volume),
        { Joint: { } joint } => new Assembly.Joined(
            joint.Strength,
            joint.Weight,
            FromAssemblySaveData(joint.Left),
            FromAssemblySaveData(joint.Right)),
        _ => throw new InvalidDataException("A worked thing in the save states neither a part nor a joint."),
    };

    private static WorldState FromSaveData(SaveData data, WorldConfiguration configuration)
    {
        var world = new WorldState(configuration);
        world.Clock.Advance(data.Tick);

        // Parents have to exist before their children (see Person.Mother): forebears first
        // (children of Unknown only), then people in save order, a parent always having been
        // added before its child.
        var peopleById = new Dictionary<Guid, Person> { [Person.Unknown.Id.Value] = Person.Unknown };
        foreach (var forebearData in data.Forebears)
        {
            world.RestoreForebear(RestorePerson(forebearData, peopleById, configuration.Rules));
        }

        foreach (var personData in data.People)
        {
            world.RestorePerson(RestorePerson(personData, peopleById, configuration.Rules));
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
            };

            if (entityData.Storage is { } storage)
            {
                foreach (var stack in storage)
                {
                    entity.Storage!.Add(stack.Kind, stack.Count);
                }
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
                AgeAtDeath = graveData.AgeAtDeath,
                CauseOfDeath = graveData.CauseOfDeath,
                MotherName = graveData.MotherName,
                FatherName = graveData.FatherName,
                KnownTechniques = graveData.KnownTechniques,
            };

            world.RestoreGrave(grave);
        }

        world.Exploration.RestoreExplored(data.ExploredCells.Select(cell => new ExplorationCell(cell.X, cell.Y)));

        foreach (var bond in data.Affections)
        {
            world.Affections.Set(new PersonId(bond.PersonA), new PersonId(bond.PersonB), bond.Value);
        }

        return world;
    }

    private static Person RestorePerson(PersonSaveData personData, Dictionary<Guid, Person> peopleById, SimulationRules rules)
    {
        var id = new PersonId(personData.Id);
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

            // Not saved: it is redrawn from the id (see Person.MaxHunger).
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
