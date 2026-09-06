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
}
