using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// The one place simulation state is rendered into English; a wrong plural or a missing parent
// is visible in the game and invisible to every other test.
public class InspectorTextTests
{
    // Sex is required of every Person but none of the inspector's wording reads it. Fixed
    // rather than drawn from the id so it is the same person on every run.
    private static Person NewPerson() =>
        new() { Name = "Ava", BirthTick = 0, Mother = Person.Unknown, Father = Person.Unknown, Sex = Sex.Female };

    private static Grave NewGrave(
        bool isMarked = true,
        int? ageAtDeath = 7,
        DeathCause? causeOfDeath = DeathCause.OldAge,
        string? motherName = "Orla",
        string? fatherName = "Hesk",
        IReadOnlyList<TechniqueId>? techniques = null) =>
        new()
        {
            Position = new Position(1, 2),
            IsMarked = isMarked,
            Name = "Ava",
            Sex = Sex.Female,
            AgeAtDeath = ageAtDeath,
            CauseOfDeath = causeOfDeath,
            MotherName = motherName,
            FatherName = fatherName,
            KnownTechniques = techniques ?? [new TechniqueId("basic_foraging")],
        };

    [Fact]
    public void SomeoneWithNothingToDoIsIdle()
    {
        Assert.Equal("Idle", InspectorText.ForTask(NewPerson()));
    }

    [Fact]
    public void AWalkerNamesWhereTheyAreWalkingTo()
    {
        var person = NewPerson();
        person.Tasks.Interrupt(new MoveTask(new Position(3, 4), speedPerTick: 0.5f));

        Assert.StartsWith("Walking to", InspectorText.ForTask(person), StringComparison.Ordinal);
    }

    [Fact]
    public void AGathererNamesWhatTheyAreGathering()
    {
        var person = NewPerson();
        var node = new Entity { Kind = new EntityKindId("apple"), Category = EntityCategory.Growable, Position = new Position(3, 4) };
        person.Tasks.Interrupt(new GatherTask(node, reachDistance: 2f));

        Assert.Equal("Gathering apple", InspectorText.ForTask(person));
    }

    [Fact]
    public void AnInfantNamesTheMotherItIsKeepingUpWith()
    {
        var person = NewPerson();
        var mother = new Person { Name = "Sela", BirthTick = 0, Mother = Person.Unknown, Father = Person.Unknown, Sex = Sex.Female };
        person.Tasks.Interrupt(new FollowTask(mother, keepWithin: 2f, speedPerTick: 0.25f));

        Assert.Equal("Keeping up with Sela", InspectorText.ForTask(person));
    }

    [Fact]
    public void AnUnmarkedGraveRecordsNothingAboutWhoLiesThere()
    {
        // An unmarked grave preserves no identity; the text must not leak the name it still
        // carries internally.
        var text = InspectorText.ForGrave(NewGrave(isMarked: false));

        Assert.Contains("Unmarked grave - no record survives.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ava", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Orla", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AMarkedGraveNamesTheDeadTheirAgeCauseParentsAndKnowledge()
    {
        var grave = NewGrave();

        var text = InspectorText.ForGrave(grave);

        Assert.StartsWith($"{grave.Id}\nPosition: {grave.Position}\n", text, StringComparison.Ordinal);
        Assert.Contains("Ava, died at age 7 winters of old age", text, StringComparison.Ordinal);
        Assert.Contains("Daughter of Orla and Hesk", text, StringComparison.Ordinal);
        Assert.Contains("Known techniques: basic_foraging", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnmarkedGraveStillSaysWhichGraveAndWhere()
    {
        // All a passer-by can tell without a record: that someone lies here, and where.
        var grave = NewGrave(isMarked: false);

        var text = InspectorText.ForGrave(grave);

        Assert.StartsWith($"{grave.Id}\nPosition: {grave.Position}\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SeveralRememberedTechniquesAreListedSeparately()
    {
        // One technique never exercises the separator; "basic_foragingbasic_woodcutting" needs two.
        var text = InspectorText.ForGrave(NewGrave(techniques:
            [new TechniqueId("basic_foraging"), new TechniqueId("basic_woodcutting")]));

        Assert.Contains("Known techniques: basic_foraging, basic_woodcutting", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OneWinterIsSingular()
    {
        Assert.Contains("died at age 1 winter of", InspectorText.ForGrave(NewGrave(ageAtDeath: 1)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(70)]
    public void EveryOtherAgeIsPlural(int age)
    {
        Assert.Contains($"died at age {age} winters", InspectorText.ForGrave(NewGrave(ageAtDeath: age)), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnrecordedCauseOfDeathIsSimplyLeftUnsaid()
    {
        var text = InspectorText.ForGrave(NewGrave(causeOfDeath: null));

        // The age line ends right after "winters". Not checked via " of ", which the parent line
        // legitimately contains.
        Assert.Contains("died at age 7 winters\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("of hunger", text, StringComparison.Ordinal);
        Assert.DoesNotContain("of old age", text, StringComparison.Ordinal);
    }

    [Fact]
    public void HungerAndOldAgeReadDifferently()
    {
        Assert.Contains("of hunger", InspectorText.ForGrave(NewGrave(causeOfDeath: DeathCause.Hunger)), StringComparison.Ordinal);
        Assert.Contains("of old age", InspectorText.ForGrave(NewGrave(causeOfDeath: DeathCause.OldAge)), StringComparison.Ordinal);
    }

    // ForDeath directly, rather than only through ForGrave/ForGraveRecord which both build the
    // same sentence: an unrecorded cause must add nothing at all, not even a stray word.
    [Fact]
    public void ForDeathWithNoRecordedCauseAddsNothing()
    {
        Assert.Equal("Died at 5 winters.", InspectorText.ForDeath(5, null));
    }

    [Fact]
    public void ForDeathAtOneWinterIsSingular()
    {
        Assert.Equal("Died at 1 winter.", InspectorText.ForDeath(1, null));
    }

    [Fact]
    public void ForDeathNamesTheCauseWhenOneIsRecorded()
    {
        Assert.Equal("Died at 5 winters of old age.", InspectorText.ForDeath(5, DeathCause.OldAge));
    }

    [Fact]
    public void AGraveWithNoKnowledgeSaysSoRatherThanTrailingOff()
    {
        Assert.Contains("Known techniques: none", InspectorText.ForGrave(NewGrave(techniques: [])), StringComparison.Ordinal);
    }

    [Fact]
    public void SomeoneWhoseParentsAreBothUnknownGetsNoParentLineAtAll()
    {
        Assert.Equal(string.Empty, InspectorText.ForParents(null, null, null));
    }

    [Theory]
    [InlineData("Orla", null, "Daughter of Orla\n")]
    [InlineData(null, "Hesk", "Daughter of Hesk\n")]
    [InlineData("Orla", "Hesk", "Daughter of Orla and Hesk\n")]
    public void OneRememberedParentIsNamedWithoutADanglingAnd(string? mother, string? father, string expected)
    {
        Assert.Equal(expected, InspectorText.ForParents(Sex.Female, mother, father));
    }

    [Fact]
    public void ASonReadsSonRatherThanDaughter()
    {
        Assert.Equal("Son of Orla and Hesk\n", InspectorText.ForParents(Sex.Male, "Orla", "Hesk"));
    }

    // The three lists a person's card and the debug dump both show. Each has an empty form, and
    // each is sorted, so the card does not reshuffle itself between refreshes.
    [Fact]
    public void KnowingNothingReadsAsNoneRatherThanAnEmptyLine()
    {
        Assert.Equal("none", InspectorText.ForTechniques([]));
    }

    [Fact]
    public void TechniquesAreListedInAStableOrder()
    {
        var techniques = new[] { new TechniqueId("basic_woodcutting"), new TechniqueId("basic_eating") };

        Assert.Equal("basic_eating, basic_woodcutting", InspectorText.ForTechniques(techniques));
    }

    [Fact]
    public void NoSkillsPractisedYetReadsAsNone()
    {
        Assert.Equal("none", InspectorText.ForSkills(new Skills()));
    }

    [Fact]
    public void SkillsAreListedWithTheirLevels()
    {
        var skills = new Skills();
        skills.Restore(new SkillTypeId("foraging"), 2.5f);

        Assert.Equal("foraging: 2.5", InspectorText.ForSkills(skills));
    }

    // Two skills together, so the alphabetical order, the ", " separator and the one-decimal
    // rounding are all exercised at once rather than trivially true of a single entry.
    [Fact]
    public void SeveralSkillsAreListedInAStableOrderRoundedToOneDecimal()
    {
        var skills = new Skills();
        skills.Restore(new SkillTypeId("woodcutting"), 3.678f);
        skills.Restore(new SkillTypeId("foraging"), 1.234f);

        Assert.Equal("foraging: 1.2, woodcutting: 3.7", InspectorText.ForSkills(skills));
    }

    [Fact]
    public void AnEmptyPackSaysSo()
    {
        Assert.Equal("empty", InspectorText.ForInventory(new Inventory()));
    }

    [Fact]
    public void CarriedItemsAreListedWithTheirCounts()
    {
        var inventory = new Inventory();
        inventory.Add(new ItemKindId("wood"), 3);
        inventory.Add(new ItemKindId("apple"), 1);

        Assert.Equal("apple x1, wood x3", InspectorText.ForInventory(inventory));
    }

    // The player's card says what someone is doing, never where: a destination in raw
    // coordinates is for the debug inspector, not for a card about a person.
    [Fact]
    public void AWalkerIsJustWalkingWithNoCoordinates()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Tasks.Interrupt(new MoveTask(new Position(9.2451, 245.707), 1f));

        var work = InspectorText.ForWork(person, world.Configuration.ResourceCatalog);

        Assert.Equal("Walking", work);
        Assert.DoesNotContain("245", work, StringComparison.Ordinal);
    }

    [Fact]
    public void AGathererNamesWhatTheyArePicking()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        var node = new Entity
        {
            Kind = TestWorld.AppleTree,
            Category = EntityCategory.Growable,
            Position = new Position(0, 0),
            Growth = new GrowthState { RemainingAmount = 10, MaxAmount = 10 },
        };
        world.AddEntity(node);
        person.Tasks.Interrupt(new GatherTask(node, world.Configuration.Rules.MaxInteractionDistance));

        Assert.Equal("Gathering apple", InspectorText.ForWork(person, world.Configuration.ResourceCatalog));
    }

    // "Idle" is a scheduler's word; the player is looking at somebody standing in a field.
    [Fact]
    public void SomebodyWithNothingToDoIsAtRest()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Equal("At rest", InspectorText.ForWork(person, world.Configuration.ResourceCatalog));
    }

    // The player's grave card says what the stone says, and nothing the stone could not - no id,
    // no coordinates. Those stay in ForGrave for the debug inspector.
    [Fact]
    public void AGraveRecordCarriesNoIdAndNoCoordinates()
    {
        var world = TestWorld.Create();
        var grave = NewGrave(ageAtDeath: 30, causeOfDeath: DeathCause.Hunger);

        var record = InspectorText.ForGraveRecord(grave, world.Configuration.SkillCatalog);

        Assert.Contains("Ava. Died at 30 winters of hunger.", record, StringComparison.Ordinal);
        Assert.Contains("Daughter of Orla and Hesk", record, StringComparison.Ordinal);
        Assert.Contains("Knew: Foraging", record, StringComparison.Ordinal);
        Assert.DoesNotContain(grave.Id.ToString(), record, StringComparison.Ordinal);
        Assert.DoesNotContain("Position", record, StringComparison.Ordinal);
    }

    [Fact]
    public void AGraveRecordSaysNothingWasKnownRatherThanTrailingOff()
    {
        var world = TestWorld.Create();

        var record = InspectorText.ForGraveRecord(NewGrave(techniques: []), world.Configuration.SkillCatalog);

        Assert.Contains("Knew: nothing", record, StringComparison.Ordinal);
    }

    // One technique never exercises the ", " separator between the grave record's known
    // things; two does.
    [Fact]
    public void AGraveRecordListsSeveralKnownThingsSeparately()
    {
        var world = TestWorld.Create();
        var grave = NewGrave(techniques: [TestWorld.BasicForaging, TestWorld.BasicTeaching]);

        var record = InspectorText.ForGraveRecord(grave, world.Configuration.SkillCatalog);

        Assert.Contains("Knew: Foraging, Teaching", record, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnmarkedGraveSaysOnlyThatNothingSurvives()
    {
        var world = TestWorld.Create();

        var record = InspectorText.ForGraveRecord(NewGrave(isMarked: false), world.Configuration.SkillCatalog);

        Assert.Equal("An unmarked grave. No record survives of who lies here.", record);
    }

    [Fact]
    public void APractisedSkillIsMarkedAsSuch()
    {
        var world = TestWorld.Create();
        var techniques = new HashSet<TechniqueId> { TestWorld.BasicForaging, new("efficient_foraging") };

        Assert.Equal(["Foraging (practised)"], InspectorText.ForKnowledge(techniques, world.Configuration.SkillCatalog));
    }

    // Two known skills, so the alphabetical order is exercised rather than trivially true of a
    // single entry.
    [Fact]
    public void SeveralKnownSkillsAreListedInAStableOrder()
    {
        var world = TestWorld.Create();
        var techniques = new HashSet<TechniqueId> { TestWorld.BasicTeaching, TestWorld.BasicForaging };

        Assert.Equal(["Foraging", "Teaching"], InspectorText.ForKnowledge(techniques, world.Configuration.SkillCatalog));
    }

    // Empty rather than worded: "nothing yet" of the living and "nothing" of the dead are
    // different things to say, so the caller says them.
    [Fact]
    public void KnowingNothingAtAllComesBackEmpty()
    {
        var world = TestWorld.Create();

        Assert.Empty(InspectorText.ForKnowledge([], world.Configuration.SkillCatalog));
    }

    // A worked thing has no authored name, so one is made of what it is (see
    // docs/materials-and-crafting-architecture.md section 8).
    [Fact]
    public void AWorkedThingIsNamedAfterItsSubstanceAndItsShape()
    {
        var world = TestWorld.Create();
        var cord = new Assembly.Part(new MaterialId("plant_fibre"), TestWorld.Cord, Quality: 0.5f, Volume: 15f);

        Assert.Equal(
            "plant fibre cord",
            InspectorText.ForWorkedThing(cord, world.Configuration.MaterialCatalog, world.Configuration.FormCatalog));
    }

    [Fact]
    public void AWorkedThingOfAnUndescribedSubstanceIsNamedByItsShapeAlone()
    {
        var world = TestWorld.Create();
        var cord = new Assembly.Part(new MaterialId("unobtainium"), TestWorld.Cord, Quality: 0.5f, Volume: 1f);

        Assert.Equal(
            "cord",
            InspectorText.ForWorkedThing(cord, world.Configuration.MaterialCatalog, world.Configuration.FormCatalog));
    }

    [Fact]
    public void AWorkedThingNothingCanBeSaidAboutStillReadsAsAThing()
    {
        var world = TestWorld.Create();
        var nameless = new Assembly.Part(new MaterialId("unobtainium"), new FormId("unheard_of"), Quality: 0.5f, Volume: 1f);

        Assert.Equal(
            "something made",
            InspectorText.ForWorkedThing(nameless, world.Configuration.MaterialCatalog, world.Configuration.FormCatalog));
    }

    // Both tiers on one line, the counted stock first and the worked things after it.
    [Fact]
    public void TheCarriedListNamesWorkedThingsBesideCountedStock()
    {
        var world = TestWorld.Create();
        var inventory = new Inventory();
        inventory.Add(TestWorld.Wood, 3);
        inventory.AddAssembly(new Assembly.Part(new MaterialId("plant_fibre"), TestWorld.Cord, Quality: 0.5f, Volume: 15f));

        Assert.Equal(
            "Wood x3, plant fibre cord",
            InspectorText.ForCarried(inventory, world));
    }

    // Two counted kinds and two worked things together, so both tiers' alphabetical order is
    // exercised rather than trivially true of a single entry each.
    [Fact]
    public void CarriedStockAndWorkedThingsAreEachListedInAStableOrder()
    {
        var world = TestWorld.Create();
        var inventory = new Inventory();
        inventory.Add(TestWorld.Wood, 3);
        inventory.Add(TestWorld.Apple, 1);
        var stick = new Assembly.Part(new MaterialId("wood"), new FormId("stick"), Quality: 1f, Volume: 2f);
        inventory.AddAssembly(new Assembly.Part(new MaterialId("plant_fibre"), TestWorld.Cord, Quality: 0.5f, Volume: 15f));
        inventory.AddAssembly(stick);

        Assert.Equal(
            "Apple x1, Wood x3, plant fibre cord, wood stick",
            InspectorText.ForCarried(inventory, world));
    }

    // Until the band has coined a word for it, a bound thing still has to read as something
    // (section 8's fallback naming).
    [Fact]
    public void ABoundThingIsNamedByWhatWasTiedToWhat()
    {
        var world = TestWorld.Create();
        var cord = new Assembly.Part(new MaterialId("plant_fibre"), TestWorld.Cord, Quality: 0.5f, Volume: 15f);
        var stick = new Assembly.Part(new MaterialId("wood"), new FormId("stick"), Quality: 1f, Volume: 2f);

        Assert.Equal(
            "lashed wood stick and plant fibre cord",
            InspectorText.ForWorkedThing(
                new Assembly.Joined(0.5f, 1f, stick, cord),
                world.Configuration.MaterialCatalog,
                world.Configuration.FormCatalog));
    }

    [Fact]
    public void ABoundThingBoundAgainNamesTheWholeDepth()
    {
        var world = TestWorld.Create();
        var stick = new Assembly.Part(new MaterialId("wood"), new FormId("stick"), Quality: 1f, Volume: 2f);
        var inner = new Assembly.Joined(0.5f, 1f, stick, stick);

        Assert.Equal(
            "lashed lashed wood stick and wood stick and wood stick",
            InspectorText.ForWorkedThing(
                new Assembly.Joined(0.5f, 1f, inner, stick),
                world.Configuration.MaterialCatalog,
                world.Configuration.FormCatalog));
    }
}
