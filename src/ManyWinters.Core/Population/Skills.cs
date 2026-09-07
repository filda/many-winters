using ManyWinters.Core.Knowledge;

namespace ManyWinters.Core.Population;

// How much practice a person has at each thing they do. A level is a number of *lessons the
// body has taken*, not a count of actions: the same act teaches less the more often it has
// already been done.
public sealed class Skills
{
    private readonly Dictionary<SkillTypeId, float> _levels = new();

    public IReadOnlyDictionary<SkillTypeId, float> Levels => _levels;

    public float Get(SkillTypeId type) => _levels.GetValueOrDefault(type);

    // Diminishing returns, applied here rather than in each command, so every kind of practice
    // (gathering, eating, teaching, burying) obeys the same curve and no caller can forget it.
    // A flat point per action made a level a tally of repetitions, and repetitions are cheap:
    // somebody standing at one apple tree collected a point every tick forever, reaching 193
    // over a single simulated year (docs/todo/todo.md). Dividing the gain by the level already
    // held makes the level grow as the square root of the practice behind it - that same year now
    // reads in the teens - so the first few tries teach nearly all of what there is
    // to learn and the two-hundredth teaches almost nothing, which is also what makes a
    // technique's discovery threshold mean "practiced this properly" rather than "stood here".
    public void Increase(SkillTypeId type, float amount)
    {
        var level = Get(type);
        _levels[type] = level + (amount / (1f + level));
    }

    // The level reached by practicing something this many times from nothing - so a rule that
    // means "after about five tries" can say so, instead of quoting whatever number the curve
    // above happens to produce and going stale the moment the curve is retuned. Every
    // technique's discovery threshold is written this way (see GatherCommand and friends), and
    // walking the same additions in the same order is what keeps "the fifth gather discovers
    // it" exact rather than a hair either side of the threshold.
    public static float LevelAfter(int practices)
    {
        var level = 0f;
        for (var practice = 0; practice < practices; practice++)
        {
            level += 1f / (1f + level);
        }

        return level;
    }

    // Loading a saved game is not practice: the level was earned before the save and has to
    // come back exactly as it was, rather than being re-derived by running the curve above
    // over a number that has already been through it.
    public void Restore(SkillTypeId type, float level) => _levels[type] = level;
}
