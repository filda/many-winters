using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

// The first verb whose subject is something already made. What it is worth is the trade it
// makes - keener and lighter - and the bound it works under: an edge comes up to what the hand
// holding it could have made, and no further.
public class SharpenCommandTests
{
    private static readonly MaterialId Stone = new("stone");
    private const float PoorlyMade = 0.3f;

    private static Assembly.Part Wedge(float quality = PoorlyMade, float volume = 1f) =>
        new(Stone, TestCatalogs.Wedge, quality, volume);

    private static Person Sharpener(WorldState world, Assembly carrying, int practice = 50)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicSharpening);
        person.Inventory.AddAssembly(carrying);
        for (var i = 0; i < practice; i++)
        {
            person.Skills.Increase(SharpenCommand.Skill, 1f);
        }

        return person;
    }

    private static Assembly.Part EdgeIn(Person person) =>
        Assert.IsType<Assembly.Part>(Assert.Single(person.Inventory.Assemblies));

    [Fact]
    public void SharpeningLeavesTheEdgeKeenerThanItWas()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Sharpener(world, Wedge());

        world.Execute(new SharpenCommand(person, Wedge()));

        Assert.True(EdgeIn(person).Quality > PoorlyMade);
    }

    // The trade, and the only thing keeping it from being free: an edge is renewed by taking
    // material off it, and mass is in the same score as keenness.
    [Fact]
    public void SharpeningTakesMaterialOffWhateverElseItDoes()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Sharpener(world, Wedge());

        world.Execute(new SharpenCommand(person, Wedge()));

        Assert.True(EdgeIn(person).Volume < 1f);
    }

    // Sharpening something that is already as good as this hand can make it is pure loss - no
    // rule says so, the two halves of the trade say it between them.
    [Fact]
    public void GrindingAwayAtAnAlreadyGoodEdgeMakesAWorseTool()
    {
        var world = TestCatalogs.CreateWorld();
        var good = Wedge(quality: 1f);
        var person = Sharpener(world, good);
        var items = world.Configuration.ItemCatalog;
        var before = person.Inventory.BestChoppingScore(items);

        world.Execute(new SharpenCommand(person, good));

        Assert.True(person.Inventory.BestChoppingScore(items) < before);
    }

    // The bound: an edge comes up to what this person could have struck themselves. A beginner
    // cannot improve on a master's work by rubbing at it.
    [Fact]
    public void ABeginnerCannotImproveOnBetterWorkThanTheirOwn()
    {
        var world = TestCatalogs.CreateWorld();
        var fine = Wedge(quality: 0.9f);
        var person = Sharpener(world, fine, practice: 0);

        world.Execute(new SharpenCommand(person, fine));

        Assert.Equal(0.9f, EdgeIn(person).Quality, 5);
    }

    // What makes it worth going back to a tool made in a first winter, once the hands have
    // learned something.
    [Fact]
    public void APractisedHandRescuesAPoorWedgeSomebodyElseStruck()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Sharpener(world, Wedge(quality: 0.2f));

        world.Execute(new SharpenCommand(person, Wedge(quality: 0.2f)));

        Assert.Equal(1f, EdgeIn(person).Quality, 5);
    }

    // The edge may sit anywhere inside the thing: the point of the verb is sharpening the axe
    // somebody is actually carrying, not a bare flake they have not hafted yet.
    [Fact]
    public void TheEdgeInsideAHaftedThingIsTheOneThatGetsWorked()
    {
        var world = TestCatalogs.CreateWorld();
        var haft = new Assembly.Part(new MaterialId("wood"), new FormId("stick"), 1f, 2f);
        var axe = new Assembly.Joined(0.8f, 0.5f, Wedge(), haft);
        var person = Sharpener(world, axe);

        world.Execute(new SharpenCommand(person, axe));

        var joined = Assert.IsType<Assembly.Joined>(Assert.Single(person.Inventory.Assemblies));
        Assert.True(Assert.IsType<Assembly.Part>(joined.Left).Quality > PoorlyMade);
        Assert.Equal(2f, Assert.IsType<Assembly.Part>(joined.Right).Volume, 5);
    }

    // One attempt renews one edge, even where two pieces of the thing are alike in every
    // particular.
    [Fact]
    public void OneAttemptSharpensOneEdge()
    {
        var world = TestCatalogs.CreateWorld();
        var twin = new Assembly.Joined(0.8f, 0.5f, Wedge(), Wedge());
        var person = Sharpener(world, twin);

        world.Execute(new SharpenCommand(person, twin));

        var joined = Assert.IsType<Assembly.Joined>(Assert.Single(person.Inventory.Assemblies));
        var qualities = new[]
        {
            Assert.IsType<Assembly.Part>(joined.Left).Quality,
            Assert.IsType<Assembly.Part>(joined.Right).Quality,
        };

        Assert.Contains(qualities, quality => quality > PoorlyMade);
        Assert.Contains(qualities, quality => Math.Abs(quality - PoorlyMade) < 0.0001f);
    }

    [Fact]
    public void SomethingWithNoEdgeOnItIsBlockedAsNothingToSharpen()
    {
        var world = TestCatalogs.CreateWorld();
        var cord = new Assembly.Part(new MaterialId("plant_fibre"), TestCatalogs.Cord, 1f, 1f);
        var person = Sharpener(world, cord);
        var command = new SharpenCommand(person, cord);

        Assert.Equal(ActionBlocker.NothingToSharpen, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(1f, EdgeIn(person).Volume, 5);
    }

    // An edge of something that gives rather than fractures cannot be struck back into shape -
    // the same question knapping one in the first place asks (MaterialAffordances.CanKnap). Wood
    // is hard enough to hold a point, so the thing does present an edge; it is too tough to renew
    // one that way, which is a different refusal from having no edge at all.
    [Fact]
    public void AnEdgeOfSomethingTooToughToFractureIsBlockedAsNotKnappable()
    {
        var world = TestCatalogs.CreateWorld();
        var woodenPoint = new Assembly.Part(new MaterialId("wood"), TestCatalogs.Wedge, 0.5f, 1f);
        var person = Sharpener(world, woodenPoint);

        Assert.Equal(ActionBlocker.NotKnappable, new SharpenCommand(person, woodenPoint).Blocker(world));
    }

    // A shape with an edge but a substance too soft to hold one is not an edge at all, so it is
    // the emptier refusal of the two.
    [Fact]
    public void AWedgeOfSomethingTooSoftToHoldAnEdgeHasNoEdgeToRenew()
    {
        var world = TestCatalogs.CreateWorld();
        var hideWedge = new Assembly.Part(new MaterialId("hide"), TestCatalogs.Wedge, 0.5f, 1f);
        var person = Sharpener(world, hideWedge);

        Assert.Equal(ActionBlocker.NothingToSharpen, new SharpenCommand(person, hideWedge).Blocker(world));
    }

    [Fact]
    public void SharpeningSomethingTheyAreNotCarryingDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Sharpener(world, Wedge());
        var elsewhere = Wedge(quality: 0.9f);

        Assert.Equal(ActionBlocker.MissingMaterials, new SharpenCommand(person, elsewhere).Blocker(world));
    }

    [Fact]
    public void SharpeningWithoutHavingLearnedItDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var wedge = Wedge();
        person.Inventory.AddAssembly(wedge);
        var command = new SharpenCommand(person, wedge);

        Assert.Equal(ActionBlocker.NotLearned, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(PoorlyMade, EdgeIn(person).Quality, 5);
    }

    [Fact]
    public void SharpeningByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Sharpener(world, Wedge());
        person.IsAlive = false;

        Assert.Equal(ActionBlocker.ActorIsDead, new SharpenCommand(person, Wedge()).Blocker(world));
    }

    // Working a thing over teaches what it is made of, as working one down does.
    [Fact]
    public void SharpeningTeachesWhatTheEdgeIsMadeOf()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Sharpener(world, Wedge());

        Assert.False(person.Beliefs.HoldsAnythingAbout(Stone));

        world.Execute(new SharpenCommand(person, Wedge()));

        Assert.True(person.Beliefs.IsFirm(Stone, MaterialProperty.Hardness));
    }

    [Fact]
    public void SharpeningIsPractice()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Sharpener(world, Wedge(), practice: 0);

        world.Execute(new SharpenCommand(person, Wedge()));

        Assert.True(person.Skills.Get(SharpenCommand.Skill) > 0f);
    }
}
