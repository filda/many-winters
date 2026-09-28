using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Population.Naming;

public class NamingCultureTests
{
    [Fact]
    public void TheSameParentsAndTickAlwaysGiveTheSameName()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = world.SpawnPerson("Mother", new Position(0, 0), TestCatalogs.AdultAgeTicks, sex: Sex.Female);
        var father = world.SpawnPerson("Father", new Position(0, 0), TestCatalogs.AdultAgeTicks, sex: Sex.Male);

        var first = world.Naming.NameForNewborn(mother, father, tick: 100);
        var second = world.Naming.NameForNewborn(mother, father, tick: 100);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ANewlyAddedPersonsNameIsThenTreatedAsAlreadyExisting()
    {
        var world = TestCatalogs.CreateWorld();
        var name = world.Naming.GenerateUnrelatedName(new Random(42));

        // Somebody is now called that; the cache (keyed on how many people and forebears exist)
        // must rebuild so the next draw - even from the same seed - avoids repeating it.
        world.SpawnPerson(name, new Position(0, 0));

        var afterAdding = world.Naming.GenerateUnrelatedName(new Random(42));

        Assert.NotEqual(name, afterAdding);
    }
}
