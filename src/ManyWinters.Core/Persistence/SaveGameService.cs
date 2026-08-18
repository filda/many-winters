using System.Text.Json;
using System.Text.Json.Serialization;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Persistence;

public static class SaveGameService
{
    private const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Stryker disable once Boolean: cosmetic formatting only, doesn't affect round-trip behavior
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
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
                person.Needs.Fatigue))
            .ToList();

        var resourceNodes = world.ResourceNodes
            .Select(node => new ResourceNodeSaveData(
                node.Id.Value,
                node.Kind,
                node.Position.X,
                node.Position.Y,
                node.RemainingAmount))
            .ToList();

        return new SaveData(
            CurrentVersion,
            world.Clock.CurrentTick,
            world.NextPersonId,
            people,
            world.NextResourceNodeId,
            resourceNodes);
    }

    public static WorldState FromSaveData(SaveData data)
    {
        var world = new WorldState();
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
        return world;
    }

    public static void Save(WorldState world, string path)
    {
        var json = JsonSerializer.Serialize(ToSaveData(world), JsonOptions);
        File.WriteAllText(path, json);
    }

    public static WorldState Load(string path)
    {
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<SaveData>(json, JsonOptions)
            ?? throw new InvalidDataException($"Save file '{path}' could not be parsed.");

        return FromSaveData(data);
    }
}
