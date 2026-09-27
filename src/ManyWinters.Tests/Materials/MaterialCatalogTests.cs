using ManyWinters.Core.Materials;

namespace ManyWinters.Tests.Materials;

public class MaterialCatalogTests
{
    private static readonly MaterialId Hide = new("hide");
    private static readonly MaterialId Stone = new("stone");

    [Fact]
    public void FindReturnsTheDefinitionForAKnownId()
    {
        var catalog = new MaterialCatalog([new MaterialDefinition(Hide, "Hide", Density: 0.75f, Insulation: 1f)]);

        var definition = catalog.Find(Hide);

        Assert.Equal("Hide", definition?.DisplayName);
        Assert.Equal(0.75f, definition?.Density);
        Assert.Equal(1f, definition?.Insulation);
    }

    [Fact]
    public void FindReturnsNullForAnUnknownIdRatherThanThrowing()
    {
        var catalog = new MaterialCatalog([]);

        Assert.Null(catalog.Find(Stone));
    }

    [Fact]
    public void AMaterialDescribedWithNoPropertiesIsWeightlessAndInsulatesNothing()
    {
        var catalog = new MaterialCatalog([new MaterialDefinition(Stone, "Stone")]);

        var definition = catalog.Find(Stone);

        Assert.Equal(0f, definition?.Density);
        Assert.Equal(0f, definition?.Insulation);
    }

    [Fact]
    public void LoadFromDirectoryReadsOneDefinitionPerSubdirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-materialcatalog-{Guid.NewGuid():N}");
        WriteDefinition(root, "hide", """{ "id": "hide", "displayName": "Hide", "density": 0.75, "insulation": 1 }""");

        try
        {
            var catalog = MaterialCatalog.LoadFromDirectory(root);

            var definition = catalog.Find(Hide);
            Assert.Equal("Hide", definition?.DisplayName);
            Assert.Equal(0.75f, definition?.Density);
            Assert.Equal(1f, definition?.Insulation);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryIgnoresNonJsonFilesInAMaterialFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-materialcatalog-{Guid.NewGuid():N}");
        var materialDir = WriteDefinition(root, "hide", """{ "id": "hide", "displayName": "Hide", "density": 0.75 }""");
        File.WriteAllText(Path.Combine(materialDir, "notes.txt"), "this is not json and would blow up if read as such");

        try
        {
            var catalog = MaterialCatalog.LoadFromDirectory(root);

            Assert.Equal(0.75f, catalog.Find(Hide)?.Density);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryThrowsInvalidDataExceptionForAMalformedDefinition()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-materialcatalog-{Guid.NewGuid():N}");
        var materialDir = Path.Combine(root, "hide");
        Directory.CreateDirectory(materialDir);
        var filePath = Path.Combine(materialDir, "hide.json");
        File.WriteAllText(filePath, "null");

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => MaterialCatalog.LoadFromDirectory(root));

            Assert.Contains(filePath, ex.Message, StringComparison.Ordinal);
            Assert.StartsWith("Material definition", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // The exact content shipped in Content/materials/sinew/sinew.json (docs/todo/fauna-plan.md,
    // phase 3): fibrous and flexible enough to twist, elastic enough to hold tension - the
    // bow/snare material the crafting doc names.
    [Fact]
    public void LoadFromDirectoryReadsASinewDefinitionThatHoldsTensionAndCanTwist()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-materialcatalog-{Guid.NewGuid():N}");
        WriteDefinition(root, "sinew", """{ "id": "sinew", "displayName": "Sinew", "density": 0.9, "toughness": 0.5, "flexibility": 0.6, "elasticity": 0.7, "fibrousness": 0.85 }""");

        try
        {
            var catalog = MaterialCatalog.LoadFromDirectory(root);

            var sinew = catalog.Find(new MaterialId("sinew"));
            Assert.NotNull(sinew);
            Assert.True(MaterialAffordances.HoldsTension(sinew));
            Assert.True(MaterialAffordances.CanTwist(sinew));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // The exact content shipped in Content/materials/bone/bone.json (docs/todo/fauna-plan.md,
    // phase 3): hard and tough like stone but lighter, and - unlike stone - too tough to
    // fracture into an edge, so it comes out neither knappable nor sharpenable from the
    // properties alone.
    [Fact]
    public void LoadFromDirectoryReadsABoneDefinitionThatIsNotKnappable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-materialcatalog-{Guid.NewGuid():N}");
        WriteDefinition(root, "bone", """{ "id": "bone", "displayName": "Bone", "density": 1.3, "hardness": 0.75, "toughness": 0.6, "flexibility": 0.1, "elasticity": 0.05, "fibrousness": 0.1 }""");

        try
        {
            var catalog = MaterialCatalog.LoadFromDirectory(root);

            var bone = catalog.Find(new MaterialId("bone"));
            Assert.NotNull(bone);
            // Stone's own shipped density (Content/materials/stone/stone.json) is 2 - bone is
            // meant to read as noticeably lighter than that, not just numerically less.
            Assert.True(bone.Density < 2f, "Bone should be lighter than stone.");
            Assert.False(MaterialAffordances.CanKnap(bone));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string WriteDefinition(string root, string id, string json)
    {
        var materialDir = Path.Combine(root, id);
        Directory.CreateDirectory(materialDir);
        File.WriteAllText(Path.Combine(materialDir, $"{id}.json"), json);

        return materialDir;
    }
}
