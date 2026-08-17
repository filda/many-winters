namespace OfFolk.Core.Time;

public sealed class SimulationClock
{
    public long CurrentTick { get; private set; }

    public void Advance(long ticks = 1)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "Ticks must not be negative.");
        }

        CurrentTick += ticks;
    }
}
