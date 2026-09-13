using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Passing a technique on face to face - the player's teach action (Main.cs) or WorldState's
// autonomous pass between neighbours. Teaching is itself a skill (see
// SkillDefinition.BaseTechnique): the teacher has to know how to teach, not just the thing
// taught. The "teaching" base technique spreads the same way; there is no separate bootstrap.
public sealed record TeachCommand(Person Teacher, Person Student, TechniqueId Technique) : ICommand
{
    // Public: WorldState.AutoTeachNearbyPeople skips teachers who cannot teach, and Main.cs
    // grants it the first time the player directs someone to teach.
    public static readonly SkillTypeId TeachingSkill = new("teaching");

    private const float SkillGainPerLesson = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // Stated in tries, not as a level: the practice curve is not linear (see Skills.Increase).
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    // A teacher who knows the efficient technique reaches a little further - a lesson to a
    // small group, not a whisper.
    private const float EfficientTeachingRangeMultiplier = 2f;

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

        return world.IsWithinReach(Teacher.Position, Student.Position, RangeMultiplier(teachingDefinition))
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
        Teacher.Skills.Increase(TeachingSkill, SkillGainPerLesson);
        if (Teacher.Skills.Get(TeachingSkill) >= DiscoveryThreshold)
        {
            Teacher.KnownTechniques.Add(teachingDefinition.EfficientTechnique);
        }
    }

    private float RangeMultiplier(SkillDefinition teachingDefinition) =>
        Teacher.KnownTechniques.Contains(teachingDefinition.EfficientTechnique)
            ? EfficientTeachingRangeMultiplier
            : 1f;
}
