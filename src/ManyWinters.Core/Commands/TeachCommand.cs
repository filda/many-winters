using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Passing a technique on to another person, face to face - either the player invoking this
// directly (Main.cs's right-click "teach") or WorldState.Advance's own autonomous version of
// the same thing between any two people who happen to be near each other. Teaching is itself a
// skill (see SkillDefinition.BaseTechnique) - the teacher has to know how to teach, not just
// know the thing being taught, the same way knowing woodcutting doesn't make someone a clear
// explainer of it. Teaching the "teaching" base technique itself is the one case where those
// two requirements collapse into the same check - there's no separate bootstrap for it, it has
// to spread the same way everything else past the player's own initial lessons does.
public sealed record TeachCommand(PersonId TeacherId, PersonId StudentId, TechniqueId Technique) : ICommand
{
    // Public, not private - WorldState.Advance's own autonomous teaching pass
    // (AutoTeachNearbyPeople) needs it too, to skip a teacher who can't teach at all before
    // looping their known techniques looking for something to pass on; so does the Godot layer
    // (Main.cs), to grant it the first time the player directs a person to teach at all.
    public static readonly SkillTypeId TeachingSkill = new("teaching");

    private const float SkillGainPerLesson = 1f;
    private const float DiscoveryThreshold = 5f;

    // A teacher who's gotten good at teaching (efficient_teaching) can instruct someone a
    // little further off - reads as a lesson to a small nearby group, not a whisper that only
    // works pressed shoulder to shoulder.
    private const float EfficientTeachingRangeMultiplier = 2f;

    public void Execute(WorldState world)
    {
        var teacher = world.People.FirstOrDefault(p => p.Id == TeacherId && p.IsAlive);
        var student = world.People.FirstOrDefault(p => p.Id == StudentId && p.IsAlive);
        // Find, not Get - a caller with no "teaching" skill registered at all (a minimal test
        // world, say) just means nobody could possibly teach anything, not a crash.
        if (teacher is null
            || student is null
            || !teacher.KnownTechniques.Contains(Technique)
            || world.SkillCatalog.Find(TeachingSkill) is not { } teachingDefinition
            || !teacher.KnownTechniques.Contains(teachingDefinition.BaseTechnique))
        {
            return;
        }

        var range = teacher.KnownTechniques.Contains(teachingDefinition.EfficientTechnique)
            ? WorldState.MaxInteractionDistance * EfficientTeachingRangeMultiplier
            : WorldState.MaxInteractionDistance;
        if (WorldState.Distance(teacher.Position, student.Position) > range)
        {
            return;
        }

        student.KnownTechniques.Add(Technique);

        teacher.Skills.Increase(TeachingSkill, SkillGainPerLesson);
        if (teacher.Skills.Get(TeachingSkill) >= DiscoveryThreshold)
        {
            teacher.KnownTechniques.Add(teachingDefinition.EfficientTechnique);
        }
    }
}
