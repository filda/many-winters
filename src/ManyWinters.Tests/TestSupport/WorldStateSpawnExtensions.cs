using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.TestSupport;

// Test-only shorthand for "put one of these in the world and hand it back" - WorldState.Add*
// itself only accepts a finished object (see its own doc comment), and most tests just need
// a person/node/building/grave to exist without caring how it was put together.
public static class WorldStateSpawnExtensions
{
    public static Person SpawnPerson(
        this WorldState world,
        string name,
        Position position,
        long initialAgeTicks = 0,
        Person? mother = null,
        Person? father = null) =>
        world.SpawnPerson(PersonId.New(), name, position, initialAgeTicks, mother, father);

    // With a chosen id - for tests pinning an outcome that runs on the id's seed (see TestIds).
    public static Person SpawnPerson(
        this WorldState world,
        PersonId id,
        string name,
        Position position,
        long initialAgeTicks = 0,
        Person? mother = null,
        Person? father = null)
    {
        var person = new Person
        {
            Id = id,
            Name = name,
            Position = position,
            BirthTick = world.Clock.CurrentTick - initialAgeTicks,
            Mother = mother ?? Person.Unknown,
            Father = father ?? Person.Unknown,
        };

        world.AddPerson(person);
        return person;
    }

    // A dead-before-the-story parent (see WorldState.Forebears) - born and dead before tick 0.
    public static Person SpawnForebear(this WorldState world, string name)
    {
        var forebear = new Person
        {
            Name = name,
            BirthTick = -2000,
            IsAlive = false,
            DeathTick = -1000,
            CauseOfDeath = DeathCause.OldAge,
            IsBuried = true,
            Mother = Person.Unknown,
            Father = Person.Unknown,
        };

        world.AddForebear(forebear);
        return forebear;
    }

    public static ResourceNode SpawnResourceNode(this WorldState world, ResourceKindId kind, Position position, float amount)
    {
        var node = new ResourceNode
        {
            Kind = kind,
            Position = position,
            RemainingAmount = amount,
            MaxAmount = amount,
        };

        world.AddResourceNode(node);
        return node;
    }

    public static Building SpawnBuilding(this WorldState world, BuildingKindId kind, Position position)
    {
        var building = new Building
        {
            Kind = kind,
            Position = position,
        };

        world.AddBuilding(building);
        return building;
    }

    public static Grave SpawnGrave(
        this WorldState world,
        Position position,
        bool isMarked,
        string? name = null,
        int? ageAtDeath = null,
        DeathCause? causeOfDeath = null,
        string? motherName = null,
        string? fatherName = null,
        IReadOnlyList<TechniqueId>? knownTechniques = null)
    {
        var grave = new Grave
        {
            Position = position,
            IsMarked = isMarked,
            Name = name,
            AgeAtDeath = ageAtDeath,
            CauseOfDeath = causeOfDeath,
            MotherName = motherName,
            FatherName = fatherName,
            KnownTechniques = knownTechniques ?? [],
        };

        world.AddGrave(grave);
        return grave;
    }
}
