using System.Text.Json;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Persistence;

public static class SaveGameService
{
    private const int CurrentVersion = 16;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Stryker disable once Boolean: cosmetic formatting only, doesn't affect round-trip behavior
        WriteIndented = true,
    };

    private static SaveData ToSaveData(WorldState world)
    {
        var people = world.People.Select(ToPersonSaveData).ToList();
        var forebears = world.Forebears.Select(ToPersonSaveData).ToList();

        var resourceNodes = world.ResourceNodes
            .Select(node => new ResourceNodeSaveData(
                node.Id.Value,
                node.Kind,
                node.Position.X,
                node.Position.Y,
                node.RemainingAmount,
                node.MaxAmount))
            .ToList();

        var buildings = world.Buildings
            .Select(building => new BuildingSaveData(
                building.Id.Value,
                building.Kind,
                building.Position.X,
                building.Position.Y,
                building.Condition,
                building.Inventory.Counts.Select(kv => new ItemStackSaveData(kv.Key, kv.Value)).ToList()))
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
            resourceNodes,
            buildings,
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
        person.Inventory.Counts.Select(kv => new ItemStackSaveData(kv.Key, kv.Value)).ToList(),
        person.BirthTick,
        person.DeathTick,
        person.CauseOfDeath,
        person.IsBuried,
        person.Mother.Id.Value,
        person.Father.Id.Value,
        person.Sex);

    private static WorldState FromSaveData(SaveData data, WorldConfiguration configuration)
    {
        var world = new WorldState(configuration);
        world.Clock.Advance(data.Tick);

        // A person is built around its parents (see Person.Mother), so they have to be back
        // before the child is. Forebears first (nobody's child but Unknown's), then people in
        // save order: a parent always existed before its child, so it was saved before it too.
        var peopleById = new Dictionary<Guid, Person> { [Person.Unknown.Id.Value] = Person.Unknown };
        foreach (var forebearData in data.Forebears)
        {
            world.RestoreForebear(RestorePerson(forebearData, peopleById));
        }

        foreach (var personData in data.People)
        {
            world.RestorePerson(RestorePerson(personData, peopleById));
        }

        foreach (var nodeData in data.ResourceNodes)
        {
            var node = new ResourceNode
            {
                Id = new ResourceNodeId(nodeData.Id),
                Kind = nodeData.Kind,
                Position = new Position(nodeData.PositionX, nodeData.PositionY),
                RemainingAmount = nodeData.RemainingAmount,
                MaxAmount = nodeData.MaxAmount,
            };

            world.RestoreResourceNode(node);
        }

        foreach (var buildingData in data.Buildings)
        {
            var building = new Building
            {
                Id = new BuildingId(buildingData.Id),
                Kind = buildingData.Kind,
                Position = new Position(buildingData.PositionX, buildingData.PositionY),
                Condition = buildingData.Condition,
            };

            foreach (var stack in buildingData.Inventory)
            {
                building.Inventory.Add(stack.Kind, stack.Count);
            }

            world.RestoreBuilding(building);
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

    private static Person RestorePerson(PersonSaveData personData, Dictionary<Guid, Person> peopleById)
    {
        var person = new Person
        {
            Id = new PersonId(personData.Id),
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

        foreach (var stack in personData.Inventory)
        {
            person.Inventory.Add(stack.Kind, stack.Count);
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

    // A save carries no catalogs of its own (see SaveData) - the configuration is what
    // turns the restored ids back into something the world can act on.
    public static WorldState Load(string path, WorldConfiguration configuration)
    {
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<SaveData>(json, JsonOptions)
            ?? throw new InvalidDataException($"Save file '{path}' could not be parsed.");

        return FromSaveData(data, configuration);
    }
}
