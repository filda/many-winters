namespace ManyWinters.Core.Tasks;

public sealed class IdleTask : PersonTask
{
    public override bool IsComplete => false;
}
