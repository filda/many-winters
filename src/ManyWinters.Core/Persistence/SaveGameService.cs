using System.Text.Json;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Persistence;

public static class SaveGameService
{
    private const int CurrentVersion = 1;

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
                person.Needs.Fatigue))
            .ToList();

        return new SaveData(CurrentVersion, world.Clock.CurrentTick, world.NextPersonId, people);
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
        var data = JsonSerializer.Deserialize<SaveData>(json)
            ?? throw new InvalidDataException($"Save file '{path}' could not be parsed.");

        return FromSaveData(data);
    }
}
