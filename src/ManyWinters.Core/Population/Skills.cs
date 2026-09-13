using ManyWinters.Core.Knowledge;

namespace ManyWinters.Core.Population;

// How much practice a person has at each thing they do. A level counts lessons the body has
// taken, not actions: the same act teaches less the more often it has been done.
public sealed class Skills
{
    private readonly Dictionary<SkillTypeId, float> _levels = new();

    public IReadOnlyDictionary<SkillTypeId, float> Levels => _levels;

    public float Get(SkillTypeId type) => _levels.GetValueOrDefault(type);

    // Diminishing returns applied here, not in each command, so every kind of practice obeys the
    // same curve. Dividing the gain by the level held makes the level grow as the square root of
    // the practice behind it, so a technique's discovery threshold means "practiced this
    // properly" rather than "stood at one apple tree for a year".
    public void Increase(SkillTypeId type, float amount)
    {
        var level = Get(type);
        _levels[type] = level + (amount / (1f + level));
    }

    // The level reached by practicing this many times from nothing, so a rule can say "after
    // about five tries" instead of quoting a number that goes stale when the curve is retuned.
    // Walks the same additions in the same order as Increase, so the threshold is hit exactly.
    public static float LevelAfter(int practices)
    {
        var level = 0f;
        for (var practice = 0; practice < practices; practice++)
        {
            level += 1f / (1f + level);
        }

        return level;
    }

    // Loading a saved game is not practice: the level comes back exactly as it was rather than
    // being run through the curve again.
    public void Restore(SkillTypeId type, float level) => _levels[type] = level;
}
