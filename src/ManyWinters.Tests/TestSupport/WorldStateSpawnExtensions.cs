using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.TestSupport;

// Test-only shorthand: WorldState.Add* only accepts a finished object, and most tests just need
// a person/node/building/grave to exist.
public static class WorldStateSpawnExtensions
{
    public static Person SpawnPerson(
        this WorldState world,
        string name,
        Position position,
        long initialAgeTicks = 0,
        Person? mother = null,
        Person? father = null,
        Sex? sex = null,
        float curiosity = 1f) =>
        world.SpawnPerson(PersonId.New(), name, position, initialAgeTicks, mother, father, sex, curiosity);

    // With a chosen id - for tests pinning an outcome that runs on the id's seed.
    public static Person SpawnPerson(
        this WorldState world,
        PersonId id,
        string name,
        Position position,
        long initialAgeTicks = 0,
        Person? mother = null,
        Person? father = null,
        // Unset when the test has no stake in it, in which case the id decides. Tests about who
        // can have a child with whom pin it, or they assert against a coin flip.
        Sex? sex = null,
        // One is the rate the shipped band works things out at; tests about idle discovery turn it.
        float curiosity = 1f)
    {
        var person = new Person
        {
            Id = id,
            Name = name,
            Position = position,
            BirthTick = world.Clock.CurrentTick - initialAgeTicks,
            Mother = mother ?? Person.Unknown,
            Father = father ?? Person.Unknown,
            Sex = sex ?? Person.SexOf(id),
            MaxHunger = world.Configuration.Rules.MaxHungerFor(id),
            Curiosity = curiosity,
        };

        world.AddPerson(person);
        return person;
    }

    // A dead-before-the-story parent: born and dead before tick 0.
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
            Sex = TestPeople.AnySex,
        };

        world.AddForebear(forebear);
        return forebear;
    }

    public static Entity SpawnResourceNode(this WorldState world, EntityKindId kind, Position position, float amount)
    {
        var node = new Entity
        {
            Kind = kind,
            Category = EntityCategory.Growable,
            Position = position,
            Growth = new GrowthState { RemainingAmount = amount, MaxAmount = amount },
        };

        world.AddEntity(node);
        return node;
    }

    public static Entity SpawnBuilding(this WorldState world, EntityKindId kind, Position position)
    {
        var building = new Entity
        {
            Kind = kind,
            Category = EntityCategory.Building,
            Position = position,
            Condition = 100f,
            Storage = new Inventory(),
        };

        world.AddEntity(building);
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

    public static Entity SpawnItemPile(this WorldState world, ItemKindId kind, Position position, int amount)
    {
        var pile = new Entity
        {
            Kind = new EntityKindId(kind.Value),
            Category = EntityCategory.Pile,
            Position = position,
            StaticAmount = amount,
        };

        world.AddEntity(pile);
        return pile;
    }
}
