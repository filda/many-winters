using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// The figure in the world and the portrait on the page are drawn from this, so it is what keeps
// the two the same person.
public class PersonLookTests
{
    private static readonly string[] Garments = ["robe", "tunic", "cloak"];
    private static readonly string[] Hairstyles = ["short", "long", "tied"];

    [Fact]
    public void TheSameSeedAlwaysGivesTheSameLook()
    {
        Assert.Equal(PersonLook.For(42, Sex.Female, lyingDown: false), PersonLook.For(42, Sex.Female, lyingDown: false));
    }

    // Whatever the seed, a woman is drawn with a woman's body and a man with a man's - the card
    // beside the portrait says which they are, and the two must agree.
    [Fact]
    public void TheBodyIsOfThePersonsOwnSex()
    {
        foreach (var seed in Enumerable.Range(0, 20))
        {
            Assert.Equal(Path("person_body", "male"), PersonLook.For(seed, Sex.Male, lyingDown: false).Body);
            Assert.Equal(Path("person_body", "female"), PersonLook.For(seed, Sex.Female, lyingDown: false).Body);
        }
    }

    // Sex decides the body and nothing else: the clothes and hair are the seed's, the same picks
    // the people of a saved world already wear.
    [Fact]
    public void ClothesAndHairAreTheSeedsWhateverTheSex()
    {
        var man = PersonLook.For(42, Sex.Male, lyingDown: false);
        var woman = PersonLook.For(42, Sex.Female, lyingDown: false);

        Assert.Equal(Path("clothing", Garments[EntityVisualVariation.IndexFor(42, 5, 3)]), man.Clothing);
        Assert.Equal(Path("hair", Hairstyles[EntityVisualVariation.IndexFor(42, 7, 3)]), man.Hair);
        Assert.Equal(man with { Body = woman.Body }, woman);
    }

    // Death lays the same person down, not somebody else: each layer is its own lying-down
    // counterpart, and the colours do not change.
    [Fact]
    public void LyingDownIsTheSameLookLaidOnItsSide()
    {
        var standing = PersonLook.For(42, Sex.Male, lyingDown: false);
        var lying = PersonLook.For(42, Sex.Male, lyingDown: true);

        Assert.Equal(standing.Body.Replace(".png", "_dead.png"), lying.Body);
        Assert.Equal(standing.Clothing.Replace(".png", "_dead.png"), lying.Clothing);
        Assert.Equal(standing.Hair.Replace(".png", "_dead.png"), lying.Hair);
        Assert.Equal(standing.ClothingColor, lying.ClothingColor);
        Assert.Equal(standing.HairColor, lying.HairColor);
    }

    [Fact]
    public void PeopleDoNotAllLookAlike()
    {
        var looks = Enumerable.Range(0, 50).Select(seed => PersonLook.For(seed, Sex.Female, lyingDown: false)).Distinct();

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

        Assert.Equal(PersonLook.For(person.Id.Seed, person.Sex, lyingDown: false), card.Look);
        Assert.False(card.IsAlive);
    }

    private static string Path(string layer, string variant) => $"res://Content/people/{layer}_{variant}.png";
}
