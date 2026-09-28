using ManyWinters.Core.Commands;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Knowledge;

public static class CasualTeaching
{
    // Once somebody knows a technique and how to teach, anyone nearby may pick it up without a
    // player action, via a per-tick chance roll. Every living pair every tick: O(n^2) is
    // negligible at tens of people.
    public static void Advance(WorldState world, long currentTick)
    {
        var skillCatalog = world.Configuration.SkillCatalog;
        var rules = world.Configuration.Rules;

        // Find, not Get: a catalog without "teaching" (most unit tests) means nobody can teach,
        // not a crash.
        if (skillCatalog.Find(TeachCommand.TeachingSkill) is not { } teachingDefinition)
        {
            return;
        }

        var teachingBaseTechnique = teachingDefinition.BaseTechnique;
        var efficientTechniques = skillCatalog.Definitions.Select(d => d.EfficientTechnique).ToHashSet();
        var criticalTechniques = new HashSet<TechniqueId> { teachingBaseTechnique };
        if (skillCatalog.Find(EatCommand.Skill) is { } eatingDefinition)
        {
            criticalTechniques.Add(eatingDefinition.BaseTechnique);
        }

        foreach (var teacher in world.People)
        {
            if (!teacher.IsAlive || !teacher.KnownTechniques.Contains(teachingBaseTechnique))
            {
                continue;
            }

            foreach (var student in world.People)
            {
                if (student == teacher || !student.IsAlive)
                {
                    continue;
                }

                TechniqueId? teachableTechnique = null;
                foreach (var technique in teacher.KnownTechniques)
                {
                    var chance = criticalTechniques.Contains(technique) ? rules.CasualTeachingChancePerTickForCriticalSkills : rules.CasualTeachingChancePerTick;
                    if (student.KnownTechniques.Contains(technique)
                        || efficientTechniques.Contains(technique)
                        || !PassesCasualTeachingRoll(teacher.Id, student.Id, technique, currentTick, chance))
                    {
                        continue;
                    }

                    teachableTechnique = technique;
                    break;
                }

                if (teachableTechnique is { } techniqueToTeach)
                {
                    new TeachCommand(teacher, student, techniqueToTeach).Execute(world);
                }
            }
        }
    }

    // Deterministic from the ids' seeds and the tick, rather than a shared Random: reproducible
    // and independent of call order between people.
    private static bool PassesCasualTeachingRoll(CreatureId teacherId, CreatureId studentId, TechniqueId technique, long tick, float chance)
    {
        var seed = CasualTeachingSeed(teacherId.Seed, studentId.Seed, technique.Value, tick);

        // Stryker disable once Equality: NextDouble() returning exactly `chance` has
        // probability zero, so < and <= are the same roll
        return new Random(seed).NextDouble() < chance;
    }

    private static int CasualTeachingSeed(int teacherSeed, int studentSeed, string technique, long tick)
    {
        // Spread by SeedHash so adjacent ids and consecutive ticks do not roll alike.
        var mixed = unchecked((uint)(teacherSeed * 73856093) ^ (uint)(studentSeed * 19349663) ^ (uint)(SeedHash.StableStringHash(technique) * 83492791) ^ ((uint)tick * 2654435761u));

        return SeedHash.Avalanche(mixed);
    }
}
