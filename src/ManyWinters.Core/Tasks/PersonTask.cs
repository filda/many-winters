using ManyWinters.Core.Population;

namespace ManyWinters.Core.Tasks;

public abstract class PersonTask
{
    public abstract bool IsComplete { get; }

    public abstract void Advance(Person person);
}
