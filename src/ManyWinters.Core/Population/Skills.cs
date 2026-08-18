using ManyWinters.Core.Knowledge;

namespace ManyWinters.Core.Population;

public sealed class Skills
{
    private readonly Dictionary<SkillTypeId, float> _levels = new();

    public IReadOnlyDictionary<SkillTypeId, float> Levels => _levels;

    public float Get(SkillTypeId type) => _levels.GetValueOrDefault(type);

    public void Increase(SkillTypeId type, float amount) => _levels[type] = Get(type) + amount;
}
