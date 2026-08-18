using ManyWinters.Core.Knowledge;

namespace ManyWinters.Core.Population;

public sealed class Skills
{
    private readonly Dictionary<SkillType, float> _levels = new();

    public IReadOnlyDictionary<SkillType, float> Levels => _levels;

    public float Get(SkillType type) => _levels.GetValueOrDefault(type);

    public void Increase(SkillType type, float amount) => _levels[type] = Get(type) + amount;
}
