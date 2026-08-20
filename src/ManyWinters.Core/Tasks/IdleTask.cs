using ManyWinters.Core.Population;

namespace ManyWinters.Core.Tasks;

public sealed class IdleTask : PersonTask
{
    public override bool IsComplete => false;

    public override void Advance(Person person)
    {
    }
}
