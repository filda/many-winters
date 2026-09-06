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
        PersonId? motherId = null,
        PersonId? fatherId = null)
    {
        var person = new Person
        {
            Id = world.NextPersonId,
            Name = name,
            Position = position,
            BirthTick = world.Clock.CurrentTick - initialAgeTicks,
            MotherId = motherId,
            FatherId = fatherId,
        };

        world.AddPerson(person);
        return person;
    }

    public static ResourceNode SpawnResourceNode(this WorldState world, ResourceKindId kind, Position position, float amount)
    {
        var node = new ResourceNode
        {
            Id = world.NextResourceNodeId,
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
            Id = world.NextBuildingId,
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
            Id = world.NextGraveId,
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
