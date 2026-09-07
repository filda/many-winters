using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
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
public sealed record TeachCommand(Person Teacher, Person Student, TechniqueId Technique) : ICommand
{
    // Public, not private - WorldState.Advance's own autonomous teaching pass
    // (AutoTeachNearbyPeople) needs it too, to skip a teacher who can't teach at all before
    // looping their known techniques looking for something to pass on; so does the Godot layer
    // (Main.cs), to grant it the first time the player directs a person to teach at all.
    public static readonly SkillTypeId TeachingSkill = new("teaching");

    private const float SkillGainPerLesson = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // The practice curve is not linear any more (see Skills.Increase), so the threshold is
    // stated as the number of tries it stands for rather than as a level.
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    // A teacher who's gotten good at teaching (efficient_teaching) can instruct someone a
    // little further off - reads as a lesson to a small nearby group, not a whisper that only
    // works pressed shoulder to shoulder.
    private const float EfficientTeachingRangeMultiplier = 2f;

    public void Execute(WorldState world)
    {
        // Find, not Get - a caller with no "teaching" skill registered at all (a minimal test
        // world, say) just means nobody could possibly teach anything, not a crash.
        if (!Teacher.IsAlive
            || !Student.IsAlive
            || !Teacher.KnownTechniques.Contains(Technique)
            || world.Configuration.SkillCatalog.Find(TeachingSkill) is not { } teachingDefinition
            || !Teacher.KnownTechniques.Contains(teachingDefinition.BaseTechnique))
        {
            return;
        }

        var rangeMultiplier = Teacher.KnownTechniques.Contains(teachingDefinition.EfficientTechnique)
            ? EfficientTeachingRangeMultiplier
            : 1f;
        if (!world.IsWithinReach(Teacher.Position, Student.Position, rangeMultiplier))
        {
            return;
        }

        Student.KnownTechniques.Add(Technique);

        Teacher.Skills.Increase(TeachingSkill, SkillGainPerLesson);
        if (Teacher.Skills.Get(TeachingSkill) >= DiscoveryThreshold)
        {
            Teacher.KnownTechniques.Add(teachingDefinition.EfficientTechnique);
        }
    }
}
