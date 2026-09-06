using ManyWinters.Core.Population;

namespace ManyWinters.Tests.Population;

public class PersonNamesTests
{
    public static TheoryData<string> BothPools()
    {
        var data = new TheoryData<string>();
        foreach (var name in PersonNames.Pool.Concat(PersonNames.Forebears))
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(BothPools))]
    public void EveryNameIsSomethingAPersonCouldActuallyBeCalled(string name)
    {
        // A blank entry would surface as a nameless person in the UI and as "child of  and
        // Hesk" on a grave - each entry is content, so each one is asserted rather than only
        // the pools' shapes.
        Assert.False(string.IsNullOrWhiteSpace(name));
    }

    [Fact]
    public void NeitherPoolRepeatsAName()
    {
        Assert.Equal(PersonNames.Pool.Length, PersonNames.Pool.Distinct().Count());
        Assert.Equal(PersonNames.Forebears.Length, PersonNames.Forebears.Distinct().Count());
    }

    [Fact]
    public void ForebearsShareNoNameWithTheLiving()
    {
        // Deliberately disjoint (see PersonNames.Forebears) so "child of Orla and Hesk" on a
        // grave can never be read as a couple still walking around camp.
        Assert.Empty(PersonNames.Pool.Intersect(PersonNames.Forebears));
    }
}
