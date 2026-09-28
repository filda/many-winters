using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Passing a technique on face to face - the player's teach action or WorldState's autonomous
// pass between neighbours. Teaching is itself a skill: the teacher has to know how to teach, not
// just the thing taught. The "teaching" base technique spreads the same way; there is no
// separate bootstrap.
public sealed record TeachCommand(Person Teacher, Person Student, TechniqueId Technique) : ICommand
{
    // Public: the game's automatic teaching pass skips teachers who cannot teach, and Main.cs
    // grants it the first time the player directs someone to teach.
    public static readonly SkillTypeId TeachingSkill = new("teaching");

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Teacher.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (!Student.IsAlive)
        {
            return ActionBlocker.TargetIsDead;
        }

        if (!Teacher.KnownTechniques.Contains(Technique))
        {
            return ActionBlocker.TeacherDoesNotKnowIt;
        }

        // Find, not Get: a catalog without "teaching" (a minimal test world) means nobody can
        // teach.
        if (world.Configuration.SkillCatalog.Find(TeachingSkill) is not { } teachingDefinition
            || !Teacher.KnownTechniques.Contains(teachingDefinition.BaseTechnique))
        {
            return ActionBlocker.NotLearned;
        }

        return world.IsWithinReach(Teacher.Position, Student.Position, RangeMultiplier(teachingDefinition, world.Configuration.Rules.EfficientTeachingRangeMultiplier))
            ? ActionBlocker.None
            : ActionBlocker.TooFar;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        Student.KnownTechniques.Add(Technique);

        var teachingDefinition = world.Configuration.SkillCatalog.Get(TeachingSkill);
        Teacher.Skills.Increase(TeachingSkill, world.Configuration.Rules.SkillGainPerLesson);
        if (Teacher.Skills.Get(TeachingSkill) >= Skills.LevelAfter(world.Configuration.Rules.PracticesBeforeDiscovery))
        {
            Teacher.KnownTechniques.Add(teachingDefinition.EfficientTechnique);
        }
    }

    private float RangeMultiplier(SkillDefinition teachingDefinition, float efficientTeachingRangeMultiplier) =>
        Teacher.KnownTechniques.Contains(teachingDefinition.EfficientTechnique)
            ? efficientTeachingRangeMultiplier
            : 1f;
}
