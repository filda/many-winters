using ManyWinters.Core.Maps;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// Wall time is too noisy to assert on, but what a tick allocates on this thread is not, and on
// the shipped map it tracks the per-entity work done for every creature: an idle search that
// priced every one of the map's thousands of decorations before checking whether it was even in
// reach allocated a quarter of a gigabyte a tick and stalled the game for half a second.
public class TickCostTests
{
    private const long MaxBytesPerTick = 16L * 1024 * 1024;
    private const int MeasuredTicks = 5;

    [Fact]
    public void ATickOnTheShippedMapAllocatesLittle()
    {
        var world = MapLoader.LoadDefault(TestCatalogs.CreateConfiguration()).World;
        world.Advance(world.Configuration.Rules.MaxPauseTicks + 1);

        var before = GC.GetAllocatedBytesForCurrentThread();
        world.Advance(MeasuredTicks);
        var perTick = (GC.GetAllocatedBytesForCurrentThread() - before) / MeasuredTicks;

        Assert.True(perTick < MaxBytesPerTick, $"a tick allocated {perTick / (1024 * 1024)} MB");
    }
}
