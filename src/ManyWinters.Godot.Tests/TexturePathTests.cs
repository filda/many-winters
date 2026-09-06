using ManyWinters.Core.Construction;

namespace ManyWinters.Godot.Tests;

// Path building only - deliberately not BaseTexturePathFor, which probes the filesystem
// through ResourceLoader and would abort the run (see this project's README).
public class TexturePathTests
{
    [Fact]
    public void AVariantSuffixGoesBeforeTheExtensionNotAfterIt()
    {
        // apple_tree_trunk_v1.png, not apple_tree_trunk.png_v1 - the file would simply not
        // exist, and a missing texture surfaces as a blank sprite rather than an error.
        Assert.Equal(
            "res://Content/resources/apple/apple_tree_trunk_v1.png",
            TexturePaths.VariantSuffixed("res://Content/resources/apple/apple_tree_trunk.png", 1));
    }

    [Fact]
    public void TheFirstVariantIsTheUnsuffixedAssetItself()
    {
        // Variant 0 is the original hand-authored file; suffixing it would look for a _v0 that
        // was never drawn.
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
        // How a split tree's variant assets are actually named: base, then the layer, then the
        // variant.
        var trunk = TexturePaths.InsertBeforeExtension("res://Content/resources/apple/apple_tree.png", "_trunk");

        Assert.Equal("res://Content/resources/apple/apple_tree_trunk_v2.png", TexturePaths.VariantSuffixed(trunk, 2));
    }

    [Fact]
    public void ABuildingsTextureLivesUnderItsOwnKindsFolder()
    {
        Assert.Equal(
            "res://Content/buildings/storage_hut/storage_hut.png",
            TexturePaths.ForBuilding(new BuildingKindId("storage_hut")));
    }
}
