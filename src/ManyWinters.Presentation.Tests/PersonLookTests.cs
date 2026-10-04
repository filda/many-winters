using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

// The figure in the world and the portrait on the page are drawn from this, so it is what keeps
// the two the same person.
public class PersonLookTests
{
    private static readonly string[] Garments = ["robe", "tunic", "cloak"];
    private static readonly string[] Hairstyles = ["short", "long", "tied"];

    [Fact]
    public void TheSameSeedAlwaysGivesTheSameLook()
    {
        Assert.Equal(PersonLook.For(42, Sex.Female, LifeStage.Adult, lyingDown: false), PersonLook.For(42, Sex.Female, LifeStage.Adult, lyingDown: false));
    }

    // Whatever the seed, a woman is drawn with a woman's body and a man with a man's - the card
    // beside the portrait says which they are, and the two must agree.
    [Fact]
    public void TheBodyIsOfThePersonsOwnSex()
    {
        foreach (var seed in Enumerable.Range(0, 20))
        {
            Assert.Equal(Path("person_body", "male"), PersonLook.For(seed, Sex.Male, LifeStage.Adult, lyingDown: false).Body);
            Assert.Equal(Path("person_body", "female"), PersonLook.For(seed, Sex.Female, LifeStage.Adult, lyingDown: false).Body);
        }
    }

    // Sex decides the body and nothing else: the clothes and hair are the seed's, the same picks
    // the people of a saved world already wear.
    [Fact]
    public void ClothesAndHairAreTheSeedsWhateverTheSex()
    {
        var man = PersonLook.For(42, Sex.Male, LifeStage.Adult, lyingDown: false);
        var woman = PersonLook.For(42, Sex.Female, LifeStage.Adult, lyingDown: false);

        Assert.Equal(Path("clothing", Garments[EntityVisualVariation.IndexFor(42, 5, 3)]), man.Clothing);
        Assert.Equal(Path("hair", Hairstyles[EntityVisualVariation.IndexFor(42, 7, 3)]), man.Hair);
        Assert.Equal(man with { Body = woman.Body }, woman);
    }

    // Death lays the same person down, not somebody else: each layer is its own lying-down
    // counterpart, and the colours do not change.
    [Fact]
    public void LyingDownIsTheSameLookLaidOnItsSide()
    {
        var standing = PersonLook.For(42, Sex.Male, LifeStage.Adult, lyingDown: false);
        var lying = PersonLook.For(42, Sex.Male, LifeStage.Adult, lyingDown: true);

        Assert.Equal(standing.Body.Replace(".png", "_dead.png"), lying.Body);
        Assert.Equal(standing.Clothing.Replace(".png", "_dead.png"), lying.Clothing);
        Assert.Equal(standing.Hair.Replace(".png", "_dead.png"), lying.Hair);
        Assert.Equal(standing.ClothingColor, lying.ClothingColor);
        Assert.Equal(standing.HairColor, lying.HairColor);
    }

    // Only the grown are drawn as they always were; a child's layers are the same names with
    // "_child" before any "_dead".
    [Theory]
    [InlineData(LifeStage.Infant)]
    [InlineData(LifeStage.Child)]
    public void ChildrenAreDrawnWithTheirOwnLayers(LifeStage stage)
    {
        var standing = PersonLook.For(42, Sex.Female, stage, lyingDown: false);

        Assert.Equal(Path("person_body", "female_child"), standing.Body);
        Assert.Equal(Path("clothing", Garments[EntityVisualVariation.IndexFor(42, 5, 3)] + "_child"), standing.Clothing);
        Assert.Equal(Path("hair", Hairstyles[EntityVisualVariation.IndexFor(42, 7, 3)] + "_child"), standing.Hair);
    }

    [Theory]
    [InlineData(LifeStage.Adult)]
    [InlineData(LifeStage.Elder)]
    public void TheGrownAreDrawnWithoutTheChildSuffix(LifeStage stage)
    {
        var look = PersonLook.For(42, Sex.Male, stage, lyingDown: false);

        Assert.Equal(Path("person_body", "male"), look.Body);
        Assert.DoesNotContain("_child", look.Clothing);
        Assert.DoesNotContain("_child", look.Hair);
    }

    // A child grows into their own garment, hair and colours, so who they are survives the
    // change of stage.
    [Fact]
    public void AChildGrowsIntoTheSameGarmentHairAndColours()
    {
        foreach (var seed in Enumerable.Range(0, 30))
        {
            var child = PersonLook.For(seed, Sex.Male, LifeStage.Child, lyingDown: false);
            var adult = PersonLook.For(seed, Sex.Male, LifeStage.Adult, lyingDown: false);

            Assert.Equal(adult.Clothing, child.Clothing.Replace("_child", string.Empty));
            Assert.Equal(adult.Hair, child.Hair.Replace("_child", string.Empty));
            Assert.Equal(adult.Body, child.Body.Replace("_child", string.Empty));
            Assert.Equal(adult.ClothingColor, child.ClothingColor);
            Assert.Equal(adult.HairColor, child.HairColor);
        }
    }

    [Fact]
    public void ADeadChildLiesInTheChildLayers()
    {
        var look = PersonLook.For(42, Sex.Male, LifeStage.Child, lyingDown: true);

        Assert.EndsWith("_child_dead.png", look.Body);
        Assert.EndsWith("_child_dead.png", look.Clothing);
        Assert.EndsWith("_child_dead.png", look.Hair);
    }

    // The portrait is the child the world draws, at the age they are, or were when they died.
    [Fact]
    public void TheCardShowsAChildAsAChild()
    {
        var world = TestWorld.Create();
        var mother = TestWorld.AddAdult(world, "Sela", new Position(0, 0));
        var father = TestWorld.AddAdult(world, "Doran", new Position(0, 0), Sex.Male);
        var child = TestWorld.AddChildOf(world, "Bran", mother, father);

        Assert.Equal(PersonLook.For(child.Id.Seed, child.Sex, LifeStage.Infant, lyingDown: false), SelectionCard.For(world, child).Look);
    }

    // Death freezes the age: a child who died long ago is still the child on the card, however
    // many winters have passed since.
    [Fact]
    public void TheCardOfAChildWhoDiedStaysAChild()
    {
        var world = TestWorld.Create();
        var ticksPerYear = world.Configuration.Rules.TicksPerYear;
        var child = new Person
        {
            Name = "Bran",
            Position = new Position(0, 0),
            BirthTick = world.Clock.CurrentTick - (ticksPerYear * 10),
            Mother = Person.Unknown,
            Father = Person.Unknown,
            Sex = Sex.Male,
            Home = TestWorld.AnyHome,
        };
        world.AddPerson(child);
        child.IsAlive = false;
        child.DeathTick = child.BirthTick + (ticksPerYear / 2);

        Assert.Equal(PersonLook.For(child.Id.Seed, child.Sex, LifeStage.Infant, lyingDown: false), SelectionCard.For(world, child).Look);
    }

    [Fact]
    public void PeopleDoNotAllLookAlike()
    {
        var looks = Enumerable.Range(0, 50).Select(seed => PersonLook.For(seed, Sex.Female, LifeStage.Adult, lyingDown: false)).Distinct();

        Assert.True(looks.Count() > 1);
    }

    // A portrait is of who they were, not of the body on the ground.
    [Fact]
    public void TheCardCarriesThePersonsStandingLookEvenOnceTheyAreDead()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.IsAlive = false;

        var card = SelectionCard.For(world, person);

        Assert.Equal(PersonLook.For(person.Id.Seed, person.Sex, LifeStage.Adult, lyingDown: false), card.Look);
        Assert.False(card.IsAlive);
    }

    // Which tint replaces a creature's own colour for its state - null for the living, since
    // their colour is their own rather than something looked up here.
    [Fact]
    public void TheLivingHaveNoTint()
    {
        Assert.Null(PersonLook.TintFor(isAlive: true, isDecayed: false));
    }

    [Fact]
    public void TheDeadAreTintedDead()
    {
        Assert.Equal(PersonLook.DeadTint, PersonLook.TintFor(isAlive: false, isDecayed: false));
    }

    [Fact]
    public void TheDecayedAreTintedFurtherToBones()
    {
        Assert.Equal(PersonLook.BonesTint, PersonLook.TintFor(isAlive: false, isDecayed: true));
    }

    // Paler and greyer than the dead tint, not merely a different colour - a corpse whose record
    // has decayed reads as further gone, not as something else entirely.
    [Fact]
    public void BonesAreAPalerGreyerTintThanDead()
    {
        Assert.True(PersonLook.BonesTint.R > PersonLook.DeadTint.R);
        Assert.True(PersonLook.BonesTint.G > PersonLook.DeadTint.G);
        Assert.True(PersonLook.BonesTint.B > PersonLook.DeadTint.B);

        var deadSpread = Math.Max(Math.Max(PersonLook.DeadTint.R, PersonLook.DeadTint.G), PersonLook.DeadTint.B)
            - Math.Min(Math.Min(PersonLook.DeadTint.R, PersonLook.DeadTint.G), PersonLook.DeadTint.B);
        var bonesSpread = Math.Max(Math.Max(PersonLook.BonesTint.R, PersonLook.BonesTint.G), PersonLook.BonesTint.B)
            - Math.Min(Math.Min(PersonLook.BonesTint.R, PersonLook.BonesTint.G), PersonLook.BonesTint.B);
        Assert.True(bonesSpread <= deadSpread);
    }

    private static string Path(string layer, string variant) => $"res://Content/people/{layer}_{variant}.png";
}
