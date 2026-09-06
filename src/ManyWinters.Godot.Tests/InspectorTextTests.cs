using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

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
        Assert.Equal("Idle", Main.TaskText(NewPerson()));
    }

    [Fact]
    public void AWalkerNamesWhereTheyAreWalkingTo()
    {
        var person = NewPerson();
        person.Tasks.Interrupt(new MoveTask(new Position(3, 4), speedPerTick: 0.5f));

        Assert.StartsWith("Walking to", Main.TaskText(person), StringComparison.Ordinal);
    }

    [Fact]
    public void AGathererNamesWhatTheyAreGathering()
    {
        var person = NewPerson();
        var node = new ResourceNode { Kind = new ResourceKindId("apple"), Position = new Position(3, 4) };
        person.Tasks.Interrupt(new GatherTask(node, reachDistance: 2f));

        Assert.Equal("Gathering apple", Main.TaskText(person));
    }

    [Fact]
    public void AnUnmarkedGraveRecordsNothingAboutWhoLiesThere()
    {
        // A burial done without the practiced technique preserves no identity (see Grave) - the
        // text must not leak the name it still happens to be carrying.
        var text = Main.GraveText(NewGrave(isMarked: false));

        Assert.Contains("Unmarked grave - no record survives.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ava", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Orla", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AMarkedGraveNamesTheDeadTheirAgeCauseParentsAndKnowledge()
    {
        var text = Main.GraveText(NewGrave());

        Assert.Contains("Ava, died at age 7 winters of old age", text, StringComparison.Ordinal);
        Assert.Contains("Child of Orla and Hesk", text, StringComparison.Ordinal);
        Assert.Contains("Known techniques: basic_foraging", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OneWinterIsSingular()
    {
        Assert.Contains("died at age 1 winter of", Main.GraveText(NewGrave(ageAtDeath: 1)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(70)]
    public void EveryOtherAgeIsPlural(int age)
    {
        Assert.Contains($"died at age {age} winters", Main.GraveText(NewGrave(ageAtDeath: age)), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnrecordedCauseOfDeathIsSimplyLeftUnsaid()
    {
        var text = Main.GraveText(NewGrave(causeOfDeath: null));

        // The age line ends right after "winters" - no cause clause is appended. Checked that
        // way rather than by searching for " of ", which the parent line legitimately contains.
        Assert.Contains("died at age 7 winters\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("of hunger", text, StringComparison.Ordinal);
        Assert.DoesNotContain("of old age", text, StringComparison.Ordinal);
    }

    [Fact]
    public void HungerAndOldAgeReadDifferently()
    {
        Assert.Contains("of hunger", Main.GraveText(NewGrave(causeOfDeath: DeathCause.Hunger)), StringComparison.Ordinal);
        Assert.Contains("of old age", Main.GraveText(NewGrave(causeOfDeath: DeathCause.OldAge)), StringComparison.Ordinal);
    }

    [Fact]
    public void AGraveWithNoKnowledgeSaysSoRatherThanTrailingOff()
    {
        Assert.Contains("Known techniques: none", Main.GraveText(NewGrave(techniques: [])), StringComparison.Ordinal);
    }

    [Fact]
    public void SomeoneWhoseParentsAreBothUnknownGetsNoParentLineAtAll()
    {
        Assert.Equal(string.Empty, Main.ParentsText(null, null));
    }

    [Theory]
    [InlineData("Orla", null, "Child of Orla\n")]
    [InlineData(null, "Hesk", "Child of Hesk\n")]
    [InlineData("Orla", "Hesk", "Child of Orla and Hesk\n")]
    public void OneRememberedParentIsNamedWithoutADanglingAnd(string? mother, string? father, string expected)
    {
        Assert.Equal(expected, Main.ParentsText(mother, father));
    }
}
