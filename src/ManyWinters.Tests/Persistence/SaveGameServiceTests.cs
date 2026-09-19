using ManyWinters.Core.Materials;
using ManyWinters.Core.Persistence;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Persistence;

public class SaveGameServiceTests
{
    [Fact]
    public void RoundTripPreservesTickAndPeople()
    {
        var world = TestCatalogs.CreateWorld();
        world.Clock.Advance(42);
        var ava = world.SpawnPerson("Ava", new Position(1.5f, 2.5f));
        ava.Needs.Hunger = 30;
        ava.Needs.Fatigue = 10;
        ava.Skills.Increase(TestCatalogs.Foraging, 3.5f);
        ava.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        ava.Inventory.Add(TestCatalogs.WoodItem, 7);
        var bran = world.SpawnPerson("Bran", new Position(-3f, 0f));
        bran.IsAlive = false;
        bran.Needs.Hunger = 100;
        bran.DeathTick = 42;
        bran.IsBuried = true;
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(4f, 5f), 42f);
        node.Growth!.RemainingAmount = 10f;
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(-1f, -2f));
        building.Condition = 63f;
        building.Storage!.Add(TestCatalogs.WoodItem, 12);

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path, TestCatalogs.CreateConfiguration());

            Assert.Equal(world.Clock.CurrentTick, restored.Clock.CurrentTick);
            Assert.Equal(world.People.Count, restored.People.Count);

            var restoredAva = restored.People.Single(p => p.Name == "Ava");
            Assert.Equal(ava.Id, restoredAva.Id);
            Assert.Equal(ava.Position, restoredAva.Position);
            Assert.True(restoredAva.IsAlive);
            Assert.Equal(ava.Needs.Hunger, restoredAva.Needs.Hunger);
            Assert.Equal(ava.Needs.Fatigue, restoredAva.Needs.Fatigue);
            Assert.Equal(ava.Skills.Get(TestCatalogs.Foraging), restoredAva.Skills.Get(TestCatalogs.Foraging));
            Assert.Equal(ava.KnownTechniques, restoredAva.KnownTechniques);
            Assert.Equal(ava.Inventory.Get(TestCatalogs.WoodItem), restoredAva.Inventory.Get(TestCatalogs.WoodItem));
            Assert.Equal(ava.BirthTick, restoredAva.BirthTick);
            Assert.Null(restoredAva.DeathTick);
            Assert.False(restoredAva.IsBuried);

            var restoredBran = restored.People.Single(p => p.Name == "Bran");
            Assert.False(restoredBran.IsAlive);
            Assert.Equal(bran.BirthTick, restoredBran.BirthTick);
            Assert.Equal(42, restoredBran.DeathTick);
            Assert.True(restoredBran.IsBuried);

            var resourceNodes = world.Entities.Where(e => e.Category == EntityCategory.Growable).ToList();
            var restoredResourceNodes = restored.Entities.Where(e => e.Category == EntityCategory.Growable).ToList();
            Assert.Equal(resourceNodes.Count, restoredResourceNodes.Count);
            var restoredNode = Assert.Single(restoredResourceNodes);
            Assert.Equal(node.Id, restoredNode.Id);
            Assert.Equal(node.Kind, restoredNode.Kind);
            Assert.Equal(node.Position, restoredNode.Position);
            Assert.Equal(node.Growth!.RemainingAmount, restoredNode.Growth!.RemainingAmount);
            Assert.Equal(node.Growth.MaxAmount, restoredNode.Growth.MaxAmount);

            var buildings = world.Entities.Where(e => e.Category == EntityCategory.Building).ToList();
            var restoredBuildings = restored.Entities.Where(e => e.Category == EntityCategory.Building).ToList();
            Assert.Equal(buildings.Count, restoredBuildings.Count);
            var restoredBuilding = Assert.Single(restoredBuildings);
            Assert.Equal(building.Id, restoredBuilding.Id);
            Assert.Equal(building.Kind, restoredBuilding.Kind);
            Assert.Equal(building.Position, restoredBuilding.Position);
            Assert.Equal(building.Condition, restoredBuilding.Condition);
            Assert.Equal(building.Storage!.Get(TestCatalogs.WoodItem), restoredBuilding.Storage!.Get(TestCatalogs.WoodItem));

            Assert.NotEmpty(world.Exploration.Explored);
            Assert.Equal(world.Exploration.Explored.ToHashSet(), restored.Exploration.Explored.ToHashSet());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RoundTripPreservesFamilyTiesAndCauseOfDeath()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = world.SpawnPerson("Sela", new Position(0, 0));
        var father = world.SpawnPerson("Bran", new Position(0, 0));
        var child = world.SpawnPerson("Ava", new Position(1, 1), mother: mother, father: father);
        child.IsAlive = false;
        child.DeathTick = 10;
        child.CauseOfDeath = DeathCause.Hunger;

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path, TestCatalogs.CreateConfiguration());

            var restoredMother = restored.People.Single(p => p.Name == "Sela");
            var restoredFather = restored.People.Single(p => p.Name == "Bran");
            var restoredChild = restored.People.Single(p => p.Name == "Ava");

            Assert.Same(restoredMother, restoredChild.Mother);
            Assert.Same(restoredFather, restoredChild.Father);
            Assert.Equal(DeathCause.Hunger, restoredChild.CauseOfDeath);
            Assert.Same(Person.Unknown, restoredMother.Mother);
            Assert.Same(Person.Unknown, restoredMother.Father);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RoundTripPreservesSexAndTheBondsBetweenPeople()
    {
        var world = TestCatalogs.CreateWorld();
        var sela = world.SpawnPerson("Sela", new Position(0, 0), sex: Sex.Female);
        var doran = world.SpawnPerson("Doran", new Position(1, 0), sex: Sex.Male);
        var tora = world.SpawnPerson("Tora", new Position(2, 0), sex: Sex.Female);
        world.Affections.Set(sela.Id, doran.Id, 62.5f);
        world.Affections.Set(doran.Id, tora.Id, 11f);

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path, TestCatalogs.CreateConfiguration());

            var restoredSela = restored.People.Single(p => p.Name == "Sela");
            var restoredDoran = restored.People.Single(p => p.Name == "Doran");
            var restoredTora = restored.People.Single(p => p.Name == "Tora");

            Assert.Equal(Sex.Female, restoredSela.Sex);
            Assert.Equal(Sex.Male, restoredDoran.Sex);
            Assert.Equal(62.5f, restored.Affections.Between(restoredSela.Id, restoredDoran.Id));
            Assert.Equal(11f, restored.Affections.Between(restoredDoran.Id, restoredTora.Id));
            Assert.Equal(0f, restored.Affections.Between(restoredSela.Id, restoredTora.Id));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Sex is saved because somebody may have chosen it; MaxHunger is not, being redrawn off the
    // id (Person.MaxHunger). A loaded band must go on starving at the same numbers as before.
    [Fact]
    public void RoundTripLeavesEveryoneStarvingAtTheSameMaxHungerTheyHadBefore()
    {
        var world = TestCatalogs.CreateWorld();
        var band = Enumerable.Range(1, 6)
            .Select(seed => world.SpawnPerson(TestIds.Person(seed), $"Person {seed}", new Position(seed * 100, 0)))
            .ToList();
        world.Advance(40);

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path, TestCatalogs.CreateConfiguration());

            Assert.All(band, person =>
                Assert.Equal(person.MaxHunger, restored.People.Single(p => p.Id == person.Id).MaxHunger));

            // And nobody drops dead the moment the world starts running again.
            restored.Advance(1);
            Assert.All(restored.People, person => Assert.True(person.IsAlive));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RoundTripPreservesForebearsAndTheChildrenWhoPointAtThem()
    {
        var world = TestCatalogs.CreateWorld();
        var forebear = world.SpawnForebear("Orla");
        var child = world.SpawnPerson("Ava", new Position(1, 1), mother: forebear);

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path, TestCatalogs.CreateConfiguration());

            var restoredForebear = Assert.Single(restored.Forebears);
            var restoredChild = Assert.Single(restored.People);
            Assert.Equal(forebear.Id, restoredForebear.Id);
            Assert.Equal("Orla", restoredForebear.Name);
            Assert.False(restoredForebear.IsAlive);
            Assert.Equal(forebear.DeathTick, restoredForebear.DeathTick);
            Assert.Same(restoredForebear, restoredChild.Mother);
            Assert.Same(Person.Unknown, restoredChild.Father);
            Assert.Equal(child.Id, restoredChild.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RoundTripPreservesGraves()
    {
        var world = TestCatalogs.CreateWorld();
        var markedGrave = world.SpawnGrave(
            new Position(1f, 2f),
            isMarked: true,
            name: "Ava",
            ageAtDeath: 5,
            causeOfDeath: DeathCause.OldAge,
            motherName: "Sela",
            fatherName: "Bran",
            knownTechniques: [TestCatalogs.EfficientForaging]);
        var anonymousGrave = world.SpawnGrave(
            new Position(3f, 4f),
            isMarked: false,
            name: null,
            ageAtDeath: null,
            causeOfDeath: null,
            motherName: null,
            fatherName: null,
            knownTechniques: []);

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path, TestCatalogs.CreateConfiguration());

            Assert.Equal(2, restored.Graves.Count);

            var restoredMarked = restored.Graves.Single(g => g.Id == markedGrave.Id);
            Assert.Equal(markedGrave.Position, restoredMarked.Position);
            Assert.True(restoredMarked.IsMarked);
            Assert.Equal("Ava", restoredMarked.Name);
            Assert.Equal(5, restoredMarked.AgeAtDeath);
            Assert.Equal(DeathCause.OldAge, restoredMarked.CauseOfDeath);
            Assert.Equal("Sela", restoredMarked.MotherName);
            Assert.Equal("Bran", restoredMarked.FatherName);
            Assert.Equal(markedGrave.KnownTechniques, restoredMarked.KnownTechniques);

            var restoredAnonymous = restored.Graves.Single(g => g.Id == anonymousGrave.Id);
            Assert.False(restoredAnonymous.IsMarked);
            Assert.Null(restoredAnonymous.Name);
            Assert.Null(restoredAnonymous.AgeAtDeath);
            Assert.Null(restoredAnonymous.CauseOfDeath);
            Assert.Null(restoredAnonymous.MotherName);
            Assert.Null(restoredAnonymous.FatherName);
            Assert.Empty(restoredAnonymous.KnownTechniques);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RoundTripPreservesItemPiles()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.WoodItem, new Position(5f, 6f), 3);

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path, TestCatalogs.CreateConfiguration());

            var restoredPile = Assert.Single(restored.Entities, e => e.Category == EntityCategory.Pile);
            Assert.Equal(pile.Id, restoredPile.Id);
            Assert.Equal(pile.Kind, restoredPile.Kind);
            Assert.Equal(pile.Position, restoredPile.Position);
            Assert.Equal(pile.StaticAmount, restoredPile.StaticAmount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadWithConfigurationProvidedWiresItIntoTheRestoredWorld()
    {
        var world = TestCatalogs.CreateWorld();
        world.SpawnPerson("Ava", new Position(0, 0));

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var configuration = TestCatalogs.CreateConfiguration();
            var restored = SaveGameService.Load(path, configuration);

            Assert.Same(configuration, restored.Configuration);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadThrowsInvalidDataExceptionForNullContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "null");

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SaveGameService.Load(path, TestCatalogs.CreateConfiguration()));

            Assert.Contains(path, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadRefusesASaveThatNamesAParentItHasNotRestoredYet()
    {
        // People are restored in file order and wired to parents by id as they go, so a child
        // stored ahead of its mother has nothing to point at. Refusing beats silently losing the
        // lineage.
        var world = TestCatalogs.CreateWorld();
        var mother = new Person { Name = "Orla", BirthTick = 0, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex };
        var child = new Person { Name = "Ava", BirthTick = 0, Mother = mother, Father = Person.Unknown, Sex = TestPeople.AnySex };
        world.AddPerson(child);
        world.AddPerson(mother);
        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");

        try
        {
            SaveGameService.Save(world, path);

            var ex = Assert.Throws<InvalidDataException>(() => SaveGameService.Load(path, TestCatalogs.CreateConfiguration()));

            Assert.Contains(mother.Id.Value.ToString(), ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("as a parent before", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // The instance tier has to survive a reload as itself: a cord's quality is its maker's work,
    // and re-deriving it on load would hand every cord the same one.
    [Fact]
    public void RoundTripPreservesWorkedThingsWithTheirQuality()
    {
        var world = TestCatalogs.CreateWorld();
        var ava = world.SpawnPerson("Ava", new Position(0f, 0f));
        ava.Inventory.Add(TestCatalogs.WoodItem, 2);
        ava.Inventory.AddAssembly(new Assembly.Part(new MaterialId("plant_fibre"), TestCatalogs.Cord, Quality: 0.42f, Volume: 15f));

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path, TestCatalogs.CreateConfiguration());

            var person = Assert.Single(restored.People);
            var cord = Assert.IsType<Assembly.Part>(Assert.Single(person.Inventory.Assemblies));
            Assert.Equal(new MaterialId("plant_fibre"), cord.Material);
            Assert.Equal(TestCatalogs.Cord, cord.Form);
            Assert.Equal(0.42f, cord.Quality, 5);
            Assert.Equal(15f, cord.Volume, 5);
            Assert.Equal(2, person.Inventory.Get(TestCatalogs.WoodItem));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
