using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

// Path building only - not the texture lookup that probes the filesystem through ResourceLoader
// and would abort the run (see this project's README).
public class TexturePathTests
{
    [Fact]
    public void AVariantSuffixGoesBeforeTheExtensionNotAfterIt()
    {
        // apple_tree_trunk_v1.png, not apple_tree_trunk.png_v1; a missing texture is a blank
        // sprite, not an error.
        Assert.Equal(
            "res://Content/resources/apple/apple_tree_trunk_v1.png",
            TexturePaths.VariantSuffixed("res://Content/resources/apple/apple_tree_trunk.png", 1));
    }

    [Fact]
    public void TheFirstVariantIsTheUnsuffixedAssetItself()
    {
        // Variant 0 is the original file; suffixing it would look for a _v0 that was never drawn.
        const string path = "res://Content/resources/apple/apple_tree_trunk.png";

        Assert.Equal(path, TexturePaths.VariantSuffixed(path, 0));
    }

    [Fact]
    public void InsertBeforeExtensionSplitsAtTheLastDotSoDirectoriesWithDotsSurvive()
    {
        Assert.Equal(
            "res://Content/a.b/apple_trunk.png",
            TexturePaths.InsertBeforeExtension("res://Content/a.b/apple.png", "_trunk"));
    }

    [Fact]
    public void SuffixesCompose()
    {
        // How a split tree's variant assets are named: base, then layer, then variant.
        var trunk = TexturePaths.InsertBeforeExtension("res://Content/resources/apple/apple_tree.png", "_trunk");

        Assert.Equal("res://Content/resources/apple/apple_tree_trunk_v2.png", TexturePaths.VariantSuffixed(trunk, 2));
    }

    [Fact]
    public void ABuildingsTextureLivesUnderItsOwnKindsFolder()
    {
        Assert.Equal(
            "res://Content/buildings/storage_hut/storage_hut.png",
            TexturePaths.ForBuilding(new EntityKindId("storage_hut")));
    }

    [Fact]
    public void ASpeciesIsDrawnFromItsOwnFolderUnderItsOwnName()
    {
        Assert.Equal("res://Content/species/deer/deer.png", TexturePaths.ForSpecies(new SpeciesId("deer")));
    }

    [Fact]
    public void TheYoungPictureSitsBesideTheGrownOneWithASuffix()
    {
        Assert.Equal("res://Content/species/deer/deer_fawn.png", TexturePaths.ForYoungSpecies(new SpeciesId("deer")));
    }

    [Theory]
    [InlineData(LifeStage.Infant, true)]
    [InlineData(LifeStage.Child, true)]
    [InlineData(LifeStage.Adult, false)]
    [InlineData(LifeStage.Elder, false)]
    public void OnlyInfantAndChildAreDrawnYoung(LifeStage stage, bool expected)
    {
        Assert.Equal(expected, TexturePaths.IsYoung(stage));
    }
}
