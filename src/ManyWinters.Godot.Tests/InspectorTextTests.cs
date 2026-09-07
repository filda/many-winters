using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// The strings a player actually reads in the inspector. Worth pinning because they are the
// one place simulation state gets rendered into English, and getting a plural or a missing
// parent wrong is visible in the game and invisible to every other test.
public class InspectorTextTests
{
    private static Person NewPerson() =>
        new() { Name = "Ava", BirthTick = 0, Mother = Person.Unknown, Father = Person.Unknown };

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
        var node = new ResourceNode { Kind = new ResourceKindId("apple"), Position = new Position(3, 4) };
        person.Tasks.Interrupt(new GatherTask(node, reachDistance: 2f));

        Assert.Equal("Gathering apple", InspectorText.ForTask(person));
    }

    [Fact]
    public void AnUnmarkedGraveRecordsNothingAboutWhoLiesThere()
    {
        // A burial done without the practiced technique preserves no identity (see Grave) - the
        // text must not leak the name it still happens to be carrying.
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
        Assert.Contains("Child of Orla and Hesk", text, StringComparison.Ordinal);
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
        // One technique never exercises the separator - the run-together
        // "basic_foragingbasic_woodcutting" only shows up once a grave holds two.
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

        // The age line ends right after "winters" - no cause clause is appended. Checked that
        // way rather than by searching for " of ", which the parent line legitimately contains.
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

    [Fact]
    public void AGraveWithNoKnowledgeSaysSoRatherThanTrailingOff()
    {
        Assert.Contains("Known techniques: none", InspectorText.ForGrave(NewGrave(techniques: [])), StringComparison.Ordinal);
    }

    [Fact]
    public void SomeoneWhoseParentsAreBothUnknownGetsNoParentLineAtAll()
    {
        Assert.Equal(string.Empty, InspectorText.ForParents(null, null));
    }

    [Theory]
    [InlineData("Orla", null, "Child of Orla\n")]
    [InlineData(null, "Hesk", "Child of Hesk\n")]
    [InlineData("Orla", "Hesk", "Child of Orla and Hesk\n")]
    public void OneRememberedParentIsNamedWithoutADanglingAnd(string? mother, string? father, string expected)
    {
        Assert.Equal(expected, InspectorText.ForParents(mother, father));
    }
}
