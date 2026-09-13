using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class GrantTechniqueCommandTests
{
    [Fact]
    public void TeachesTheNamedPersonAndNobodyElse()
    {
        var world = TestCatalogs.CreateWorld();
        var ava = world.SpawnPerson("Ava", new Position(0, 0));
        var bran = world.SpawnPerson("Bran", new Position(1, 0));

        world.Execute(new GrantTechniqueCommand(bran, TestCatalogs.BasicForaging));

        Assert.Contains(TestCatalogs.BasicForaging, bran.KnownTechniques);
        Assert.DoesNotContain(TestCatalogs.BasicForaging, ava.KnownTechniques);
    }

    [Fact]
    public void GrantsUnconditionally()
    {
        // The player is the sole initial source of every technique, so this is deliberately not
        // bound by the rules for one person teaching another (proximity, knowing "teaching").
        var world = TestCatalogs.CreateWorld();
        var ava = world.SpawnPerson("Ava", new Position(0, 0));

        world.Execute(new GrantTechniqueCommand(ava, TestCatalogs.BasicTeaching));
        world.Execute(new GrantTechniqueCommand(ava, TestCatalogs.EfficientWoodcutting));

        Assert.Contains(TestCatalogs.BasicTeaching, ava.KnownTechniques);
        Assert.Contains(TestCatalogs.EfficientWoodcutting, ava.KnownTechniques);
    }

    [Fact]
    public void GrantingTheSameTechniqueTwiceIsANoOp()
    {
        var world = TestCatalogs.CreateWorld();
        var ava = world.SpawnPerson("Ava", new Position(0, 0));

        world.Execute(new GrantTechniqueCommand(ava, TestCatalogs.BasicForaging));
        world.Execute(new GrantTechniqueCommand(ava, TestCatalogs.BasicForaging));

        Assert.Single(ava.KnownTechniques);
    }

    [Fact]
    public void DoesNothingForADeadPerson()
    {
        var world = TestCatalogs.CreateWorld();
        var ava = world.SpawnPerson("Ava", new Position(0, 0));
        var bran = world.SpawnPerson("Bran", new Position(1, 0));
        bran.IsAlive = false;

        world.Execute(new GrantTechniqueCommand(bran, TestCatalogs.BasicForaging));

        Assert.Empty(bran.KnownTechniques);
        Assert.Empty(ava.KnownTechniques);
    }

    [Fact]
    public void NothingBlocksThePlayerShowingALivingPersonHow()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        Assert.Equal(ActionBlocker.None, new GrantTechniqueCommand(person, TestCatalogs.BasicForaging).Blocker(world));
    }

    [Fact]
    public void ADeadPersonIsBlockedFromBeingShownHow()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;

        Assert.Equal(ActionBlocker.ActorIsDead, new GrantTechniqueCommand(person, TestCatalogs.BasicForaging).Blocker(world));
    }
}
