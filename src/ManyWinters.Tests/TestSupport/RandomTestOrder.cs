using ManyWinters.Tests.TestSupport;
using Xunit.Sdk;
using Xunit.v3;

// xunit v3 already shuffles test collections (one per class) on every run, but orders the
// classes inside a collection, the methods inside a class and the cases inside a theory by a
// hash that never changes, so a test leaning on the one before it keeps passing. This shuffles
// all three from xunit's own seeded Randomizer: the run prints its seed, and passing it back
// (`-seed`, or "seed" in xunit.runner.json) replays a failing order exactly. Linked into
// ManyWinters.Godot.Tests as well.
[assembly: TestClassOrderer(typeof(RandomTestOrder))]
[assembly: TestMethodOrderer(typeof(RandomTestOrder))]
[assembly: TestCaseOrderer(typeof(RandomTestOrder))]

namespace ManyWinters.Tests.TestSupport;

public sealed class RandomTestOrder : ITestClassOrderer, ITestMethodOrderer, ITestCaseOrderer
{
    public IReadOnlyCollection<TTestClass?> OrderTestClasses<TTestClass>(IReadOnlyCollection<TTestClass?> testClasses)
        where TTestClass : ITestClass => Shuffle(testClasses, Randomizer.Current);

    public IReadOnlyCollection<TTestMethod?> OrderTestMethods<TTestMethod>(IReadOnlyCollection<TTestMethod?> testMethods)
        where TTestMethod : ITestMethod => Shuffle(testMethods, Randomizer.Current);

    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
        where TTestCase : ITestCase => Shuffle(testCases, Randomizer.Current);

    /// <summary>Fisher–Yates over a copy, so the collection xunit handed in is left as it was.</summary>
    public static IReadOnlyCollection<T> Shuffle<T>(IReadOnlyCollection<T> items, Random random)
    {
        var shuffled = items.ToArray();
        for (var i = shuffled.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }
}
