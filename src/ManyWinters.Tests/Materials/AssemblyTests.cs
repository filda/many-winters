using ManyWinters.Core.Materials;

namespace ManyWinters.Tests.Materials;

public class AssemblyTests
{
    private static readonly MaterialId Wood = new("wood");
    private static readonly MaterialId Stone = new("stone");
    private static readonly MaterialId Grass = new("grass");
    private static readonly MaterialId Unknown = new("unobtainium");

    private static readonly FormId Wedge = new("wedge");
    private static readonly FormId Shaft = new("shaft");
    private static readonly FormId Cord = new("cord");

    // These tests are about the parts, so the lashings themselves weigh nothing; what a
    // binding weighs has its own test below.
    private const float NoBinding = 0f;

    private const float WoodDensity = 0.5f;
    private const float WoodToughness = 0.6f;
    private const float StoneDensity = 2f;
    private const float StoneToughness = 0.2f;
    private const float GrassDensity = 0.1f;
    private const float GrassToughness = 0.4f;

    private static readonly MaterialCatalog Materials = new([
        new MaterialDefinition(Wood, "Wood", WoodDensity, Toughness: WoodToughness),
        new MaterialDefinition(Stone, "Stone", StoneDensity, Toughness: StoneToughness),
        new MaterialDefinition(Grass, "Grass", GrassDensity, Toughness: GrassToughness),
    ]);

    [Fact]
    public void APartWeighsItsMaterialsDensityTimesItsVolume()
    {
        var head = new Assembly.Part(Stone, Wedge, Quality: 0.5f, Volume: 3f);

        Assert.Equal(6f, head.Weight(Materials));
    }

    [Fact]
    public void APartOfAnUndescribedMaterialWeighsNothingRatherThanThrowing()
    {
        var part = new Assembly.Part(Unknown, Wedge, Quality: 1f, Volume: 10f);

        Assert.Equal(0f, part.Weight(Materials));
    }

    [Fact]
    public void AJoinedAssemblyWeighsBothOfItsSidesTogether()
    {
        var head = new Assembly.Part(Stone, Wedge, Volume: 3f);
        var haft = new Assembly.Part(Wood, Shaft, Volume: 4f);

        // 2 * 3 for the head, 0.5 * 4 for the haft.
        Assert.Equal(8f, new Assembly.Joined(0.9f, NoBinding, head, haft).Weight(Materials));
    }

    // The binding is negligible beside what it holds together, so an assembly weighs the same
    // however strong the lashings between its parts are.
    [Fact]
    public void AJointAddsNoWeightOfItsOwn()
    {
        var head = new Assembly.Part(Stone, Wedge, Volume: 3f);
        var haft = new Assembly.Part(Wood, Shaft, Volume: 4f);

        var axe = new Assembly.Joined(0.9f, NoBinding, head, haft);

        Assert.Equal(head.Weight(Materials) + haft.Weight(Materials), axe.Weight(Materials));
    }

    [Fact]
    public void WeightAccumulatesThroughEveryLevelOfNesting()
    {
        var axe = new Assembly.Joined(0.9f, NoBinding, new Assembly.Part(Stone, Wedge, Volume: 3f), new Assembly.Part(Wood, Shaft, Volume: 4f));

        // A second head lashed onto the finished axe: 6 + 2 and 6 again.
        var absurd = new Assembly.Joined(0.9f, NoBinding, axe, new Assembly.Part(Stone, Wedge, Volume: 3f));

        Assert.Equal(14f, absurd.Weight(Materials));
    }

    [Fact]
    public void APartsDurabilityIsItsMaterialsToughnessTimesHowWellItWasWorked()
    {
        var haft = new Assembly.Part(Wood, Shaft, Quality: 0.5f, Volume: 4f);

        Assert.Equal(0.3f, haft.Durability(Materials), 5);
    }

    // Brittle stuff well worked and tough stuff botched both come out poor, which is the point of
    // multiplying rather than taking either number alone.
    [Fact]
    public void BotchedWorkIsAsPoorAsBrittleSubstance()
    {
        var botchedWood = new Assembly.Part(Wood, Shaft, Quality: 0.2f, Volume: 1f);
        var wellWorkedStone = new Assembly.Part(Stone, Wedge, Quality: 0.6f, Volume: 1f);

        Assert.Equal(botchedWood.Durability(Materials), wellWorkedStone.Durability(Materials), 5);
    }

    [Fact]
    public void APerfectlyWorkedPartIsStillOnlyAsSoundAsItsSubstance()
    {
        var part = new Assembly.Part(Stone, Wedge, Quality: 1f, Volume: 1f);

        Assert.Equal(StoneToughness, part.Durability(Materials), 5);
    }

    [Fact]
    public void AnUnworkedPartHasNoDurabilityAtAll()
    {
        var part = new Assembly.Part(Wood, Shaft, Quality: 0f, Volume: 1f);

        Assert.Equal(0f, part.Durability(Materials));
    }

    [Fact]
    public void APartOfAnUndescribedMaterialHasNoDurabilityRatherThanThrowing()
    {
        var part = new Assembly.Part(Unknown, Wedge, Quality: 1f, Volume: 1f);

        Assert.Equal(0f, part.Durability(Materials));
    }

    // The three places a joined assembly can be weakest, one test each, so that dropping any one
    // of them out of the reckoning shows up.
    [Fact]
    public void AWeakJointCapsAnAssemblyOfTwoSoundParts()
    {
        var sound = new Assembly.Part(Wood, Shaft, Quality: 1f, Volume: 1f);

        Assert.Equal(0.1f, new Assembly.Joined(0.1f, NoBinding, sound, sound).Durability(Materials), 5);
    }

    [Fact]
    public void AWeakLeftPartCapsAnAssemblyWithASoundJoint()
    {
        var weak = new Assembly.Part(Wood, Shaft, Quality: 0.1f, Volume: 1f);
        var sound = new Assembly.Part(Wood, Shaft, Quality: 1f, Volume: 1f);

        Assert.Equal(WoodToughness * 0.1f, new Assembly.Joined(1f, NoBinding, weak, sound).Durability(Materials), 5);
    }

    [Fact]
    public void AWeakRightPartCapsAnAssemblyWithASoundJoint()
    {
        var sound = new Assembly.Part(Wood, Shaft, Quality: 1f, Volume: 1f);
        var weak = new Assembly.Part(Wood, Shaft, Quality: 0.1f, Volume: 1f);

        Assert.Equal(WoodToughness * 0.1f, new Assembly.Joined(1f, NoBinding, sound, weak).Durability(Materials), 5);
    }

    // "A rope lashed to a rope lashed to a rope is constructible and useless" - the model refuses
    // nothing, and the weakest link anywhere in the depth still governs the whole.
    [Fact]
    public void TheWeakestLinkAnywhereInTheDepthGovernsTheWholeAssembly()
    {
        var rope = new Assembly.Part(Grass, Cord, Quality: 0.5f, Volume: 1f);
        var ropeOnRope = new Assembly.Joined(0.8f, NoBinding, rope, rope);
        var ropeOnRopeOnRope = new Assembly.Joined(0.9f, NoBinding, ropeOnRope, rope);

        // Every part is 0.4 * 0.5 = 0.2, which is under both lashings.
        Assert.Equal(0.2f, ropeOnRopeOnRope.Durability(Materials), 5);
    }

    [Fact]
    public void ABadJointBuriedDeepStillGovernsTheWholeAssembly()
    {
        var sound = new Assembly.Part(Wood, Shaft, Quality: 1f, Volume: 1f);
        var buriedBadJoint = new Assembly.Joined(0.05f, NoBinding, sound, sound);

        Assert.Equal(0.05f, new Assembly.Joined(1f, NoBinding, buriedBadJoint, sound).Durability(Materials), 5);
    }

    // No MaxParts constant: nothing refuses depth, however silly it gets.
    [Fact]
    public void NothingCapsHowManyPartsAnAssemblyMayHave()
    {
        var part = new Assembly.Part(Wood, Shaft, Quality: 1f, Volume: 1f);
        Assembly grown = part;

        for (var i = 0; i < 50; i++)
        {
            grown = new Assembly.Joined(1f, NoBinding, grown, part);
        }

        Assert.Equal(51 * WoodDensity, grown.Weight(Materials), 5);
        Assert.Equal(WoodToughness, grown.Durability(Materials), 5);
    }

    // A lashing is made of something, and that something does not vanish on being tied: what
    // went into the binding is carried by the object it now holds together.
    [Fact]
    public void ABindingWeighsWhateverWentIntoIt()
    {
        var head = new Assembly.Part(Stone, Wedge, Volume: 3f);
        var haft = new Assembly.Part(Wood, Shaft, Volume: 4f);

        var lashed = new Assembly.Joined(0.9f, 1.5f, head, haft);

        Assert.Equal(6f + 2f + 1.5f, lashed.Weight(Materials));
    }

    [Fact]
    public void EveryBindingInTheDepthAddsItsOwnWeight()
    {
        var part = new Assembly.Part(Wood, Shaft, Volume: 2f);
        var inner = new Assembly.Joined(1f, 0.5f, part, part);
        var outer = new Assembly.Joined(1f, 0.5f, inner, part);

        // Three sticks at 1 each, two lashings at 0.5.
        Assert.Equal(4f, outer.Weight(Materials));
    }
}
