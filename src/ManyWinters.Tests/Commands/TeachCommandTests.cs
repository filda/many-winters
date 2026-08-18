using ManyWinters.Core.Commands;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Commands;

public class TeachCommandTests
{
    [Fact]
    public void TeachingATechniqueTheTeacherKnowsGivesItToTheStudent()
    {
        var world = new WorldState();
        var teacher = world.AddPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(Technique.EfficientForaging);
        var student = world.AddPerson("Bran", new Position(0, 0));

        world.Execute(new TeachCommand(teacher.Id, student.Id, Technique.EfficientForaging));

        Assert.Contains(Technique.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void TeachingATechniqueTheTeacherDoesNotKnowDoesNothing()
    {
        var world = new WorldState();
        var teacher = world.AddPerson("Ava", new Position(0, 0));
        var student = world.AddPerson("Bran", new Position(0, 0));

        world.Execute(new TeachCommand(teacher.Id, student.Id, Technique.EfficientForaging));

        Assert.DoesNotContain(Technique.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void ADeadTeacherCannotTeach()
    {
        var world = new WorldState();
        var teacher = world.AddPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(Technique.EfficientForaging);
        teacher.IsAlive = false;
        var student = world.AddPerson("Bran", new Position(0, 0));

        world.Execute(new TeachCommand(teacher.Id, student.Id, Technique.EfficientForaging));

        Assert.DoesNotContain(Technique.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void ADeadStudentCannotLearn()
    {
        var world = new WorldState();
        var teacher = world.AddPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(Technique.EfficientForaging);
        var student = world.AddPerson("Bran", new Position(0, 0));
        student.IsAlive = false;

        world.Execute(new TeachCommand(teacher.Id, student.Id, Technique.EfficientForaging));

        Assert.DoesNotContain(Technique.EfficientForaging, student.KnownTechniques);
    }

    [Fact]
    public void TeachingWithAnUnknownTeacherOrStudentDoesNothing()
    {
        var world = new WorldState();
        var teacher = world.AddPerson("Ava", new Position(0, 0));
        teacher.KnownTechniques.Add(Technique.EfficientForaging);
        var student = world.AddPerson("Bran", new Position(0, 0));

        world.Execute(new TeachCommand(new PersonId(999), student.Id, Technique.EfficientForaging));
        world.Execute(new TeachCommand(teacher.Id, new PersonId(999), Technique.EfficientForaging));

        Assert.DoesNotContain(Technique.EfficientForaging, student.KnownTechniques);
    }
}
