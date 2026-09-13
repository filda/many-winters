using ManyWinters.Core.Commands;
using ManyWinters.Tests.TestSupport;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Commands;

public class TeachCommandTests
{
    [Fact]
    public void TeachingATechniqueTheTeacherKnowsGivesItToTheStudent()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        var student = world.SpawnPerson("Bran", new Position(0, 0));

        world.Execute(new TeachCommand(teacher, student, TestCatalogs.EfficientForaging));

        Assert.Contains(TestCatalogs.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void TeachingATechniqueTheTeacherDoesNotKnowDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        var student = world.SpawnPerson("Bran", new Position(0, 0));

        world.Execute(new TeachCommand(teacher, student, TestCatalogs.EfficientForaging));

        Assert.DoesNotContain(TestCatalogs.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void TeacherNotKnowingHowToTeachDoesNothingEvenIfTheyKnowTheTechnique()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        var student = world.SpawnPerson("Bran", new Position(0, 0));

        world.Execute(new TeachCommand(teacher, student, TestCatalogs.EfficientForaging));

        Assert.DoesNotContain(TestCatalogs.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void ADeadTeacherCannotTeach()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        teacher.IsAlive = false;
        var student = world.SpawnPerson("Bran", new Position(0, 0));

        world.Execute(new TeachCommand(teacher, student, TestCatalogs.EfficientForaging));

        Assert.DoesNotContain(TestCatalogs.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void ADeadStudentCannotLearn()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        var student = world.SpawnPerson("Bran", new Position(0, 0));
        student.IsAlive = false;

        world.Execute(new TeachCommand(teacher, student, TestCatalogs.EfficientForaging));

        Assert.DoesNotContain(TestCatalogs.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void TeachingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        var student = world.SpawnPerson("Bran", new Position(world.Configuration.Rules.MaxInteractionDistance, 0));

        world.Execute(new TeachCommand(teacher, student, TestCatalogs.EfficientForaging));

        Assert.Contains(TestCatalogs.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void TeachingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        var student = world.SpawnPerson("Bran", new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));

        world.Execute(new TeachCommand(teacher, student, TestCatalogs.EfficientForaging));

        Assert.DoesNotContain(TestCatalogs.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void KnowingEfficientTeachingReachesFurther()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.EfficientTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        var student = world.SpawnPerson("Bran", new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));

        world.Execute(new TeachCommand(teacher, student, TestCatalogs.EfficientForaging));

        Assert.Contains(TestCatalogs.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void NothingBlocksALessonBetweenTwoPeopleStandingTogether()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var student = world.SpawnPerson("Bran", new Position(0, 0));

        var command = new TeachCommand(teacher, student, TestCatalogs.BasicForaging);

        Assert.Equal(ActionBlocker.None, command.Blocker(world));
    }

    [Fact]
    public void ADeadTeacherIsBlockedFromTeaching()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.BasicForaging);
        teacher.IsAlive = false;
        var student = world.SpawnPerson("Bran", new Position(0, 0));

        var command = new TeachCommand(teacher, student, TestCatalogs.BasicForaging);

        Assert.Equal(ActionBlocker.ActorIsDead, command.Blocker(world));
    }

    [Fact]
    public void ADeadStudentBlocksTheLesson()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var student = world.SpawnPerson("Bran", new Position(0, 0));
        student.IsAlive = false;

        var command = new TeachCommand(teacher, student, TestCatalogs.BasicForaging);

        Assert.Equal(ActionBlocker.TargetIsDead, command.Blocker(world));
    }

    // Not the same refusal as not knowing how to teach: this teacher can hold a lesson, just not
    // about this.
    [Fact]
    public void ATeacherWhoDoesNotKnowTheTechniqueSaysSoRatherThanNotLearned()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        var student = world.SpawnPerson("Bran", new Position(0, 0));

        var command = new TeachCommand(teacher, student, TestCatalogs.BasicForaging);

        Assert.Equal(ActionBlocker.TeacherDoesNotKnowIt, command.Blocker(world));
    }

    [Fact]
    public void ATeacherWhoNeverLearnedToTeachIsBlockedAsNotLearned()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var student = world.SpawnPerson("Bran", new Position(0, 0));

        var command = new TeachCommand(teacher, student, TestCatalogs.BasicForaging);

        Assert.Equal(ActionBlocker.NotLearned, command.Blocker(world));
    }

    [Fact]
    public void AStudentOutOfEarshotBlocksTheLessonAsTooFar()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var student = world.SpawnPerson("Bran", new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));

        var command = new TeachCommand(teacher, student, TestCatalogs.BasicForaging);

        Assert.Equal(ActionBlocker.TooFar, command.Blocker(world));
    }

    // The wider reach of an efficient teacher is part of the question the UI asks, not something
    // discovered only once the lesson is attempted.
    [Fact]
    public void NothingBlocksAnEfficientTeacherAtADistanceThatWouldOtherwiseBeTooFar()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.EfficientTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var student = world.SpawnPerson("Bran", new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));

        var command = new TeachCommand(teacher, student, TestCatalogs.BasicForaging);

        Assert.Equal(ActionBlocker.None, command.Blocker(world));
    }
}
