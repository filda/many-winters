using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record TeachCommand(PersonId TeacherId, PersonId StudentId, TechniqueId Technique) : ICommand
{
    public void Execute(WorldState world)
    {
        var teacher = world.People.FirstOrDefault(p => p.Id == TeacherId && p.IsAlive);
        var student = world.People.FirstOrDefault(p => p.Id == StudentId && p.IsAlive);
        if (teacher is null || student is null || !teacher.KnownTechniques.Contains(Technique))
        {
            return;
        }

        student.KnownTechniques.Add(Technique);
    }
}
