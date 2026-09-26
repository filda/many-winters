using ManyWinters.Core.Population;

namespace ManyWinters.Core.Tasks;

public abstract class CreatureTask
{
    public abstract bool IsComplete { get; }

    public abstract void Advance(Creature creature);
}
