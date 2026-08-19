using System.Text.Json;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Persistence;

public static class SaveGameService
{
    private const int CurrentVersion = 7;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Stryker disable once Boolean: cosmetic formatting only, doesn't affect round-trip behavior
        WriteIndented = true,
    };

    public static SaveData ToSaveData(WorldState world)
    {
        var people = world.People
            .Select(person => new PersonSaveData(
                person.Id.Value,
                person.Name,
                person.Position.X,
                person.Position.Y,
                person.IsAlive,
                person.Needs.Hunger,
                person.Needs.Fatigue,
                person.Skills.Levels.Select(kv => new SkillLevelSaveData(kv.Key, kv.Value)).ToList(),
                person.KnownTechniques.ToList(),
                person.Inventory.Counts.Select(kv => new ItemStackSaveData(kv.Key, kv.Value)).ToList()))
            .ToList();

        var resourceNodes = world.ResourceNodes
            .Select(node => new ResourceNodeSaveData(
                node.Id.Value,
                node.Kind,
                node.Position.X,
                node.Position.Y,
                node.RemainingAmount))
            .ToList();

        var buildings = world.Buildings
            .Select(building => new BuildingSaveData(
                building.Id.Value,
                building.Kind,
                building.Position.X,
                building.Position.Y,
                building.Condition))
            .ToList();

        return new SaveData(
            CurrentVersion,
            world.Clock.CurrentTick,
            world.NextPersonId,
            people,
            world.NextResourceNodeId,
            resourceNodes,
            world.NextBuildingId,
            buildings);
    }

    public static WorldState FromSaveData(
        SaveData data,
        ResourceCatalog? resourceCatalog = null,
        SkillCatalog? skillCatalog = null,
        RecipeCatalog? recipeCatalog = null,
        BuildingCatalog? buildingCatalog = null)
    {
        var world = resourceCatalog is not null && skillCatalog is not null && recipeCatalog is not null && buildingCatalog is not null
            ? new WorldState(resourceCatalog, skillCatalog, recipeCatalog, buildingCatalog)
            : new WorldState();
        world.Clock.Advance(data.Tick);

        foreach (var personData in data.People)
        {
            var person = new Person
            {
                Id = new PersonId(personData.Id),
                Name = personData.Name,
                Position = new Position(personData.PositionX, personData.PositionY),
                IsAlive = personData.IsAlive,
            };
            person.Needs.Hunger = personData.Hunger;
            person.Needs.Fatigue = personData.Fatigue;
            foreach (var skillData in personData.Skills)
            {
                person.Skills.Increase(skillData.Type, skillData.Level);
            }

            foreach (var technique in personData.KnownTechniques)
            {
                person.KnownTechniques.Add(technique);
            }

            foreach (var stack in personData.Inventory)
            {
                person.Inventory.Add(stack.Kind, stack.Count);
            }

            world.RestorePerson(person);
        }

        world.SetNextPersonId(data.NextPersonId);

        foreach (var nodeData in data.ResourceNodes)
        {
            var node = new ResourceNode
            {
                Id = new ResourceNodeId(nodeData.Id),
                Kind = nodeData.Kind,
                Position = new Position(nodeData.PositionX, nodeData.PositionY),
                RemainingAmount = nodeData.RemainingAmount,
            };

            world.RestoreResourceNode(node);
        }

        world.SetNextResourceNodeId(data.NextResourceNodeId);

        foreach (var buildingData in data.Buildings)
        {
            var building = new Building
            {
                Id = new BuildingId(buildingData.Id),
                Kind = buildingData.Kind,
                Position = new Position(buildingData.PositionX, buildingData.PositionY),
                Condition = buildingData.Condition,
            };

            world.RestoreBuilding(building);
        }

        world.SetNextBuildingId(data.NextBuildingId);
        return world;
    }

    public static void Save(WorldState world, string path)
    {
        var json = JsonSerializer.Serialize(ToSaveData(world), JsonOptions);
        File.WriteAllText(path, json);
    }

    public static WorldState Load(
        string path,
        ResourceCatalog? resourceCatalog = null,
        SkillCatalog? skillCatalog = null,
        RecipeCatalog? recipeCatalog = null,
        BuildingCatalog? buildingCatalog = null)
    {
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<SaveData>(json, JsonOptions)
            ?? throw new InvalidDataException($"Save file '{path}' could not be parsed.");

        return FromSaveData(data, resourceCatalog, skillCatalog, recipeCatalog, buildingCatalog);
    }
}
