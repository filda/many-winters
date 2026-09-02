using ManyWinters.Core.Items;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Items;

public class RecipeCatalogTests
{
    [Fact]
    public void GetReturnsTheDefinitionForAKnownOutput()
    {
        var catalog = new RecipeCatalog([
            new RecipeDefinition(TestCatalogs.Axe, TestCatalogs.WoodItem, 5),
        ]);

        var recipe = catalog.Get(TestCatalogs.Axe);

        Assert.Equal(TestCatalogs.WoodItem, recipe.InputItem);
        Assert.Equal(5, recipe.InputAmount);
    }

    [Fact]
    public void GetThrowsForAnUnknownOutput()
    {
        var catalog = new RecipeCatalog([]);

        Assert.Throws<KeyNotFoundException>(() => catalog.Get(TestCatalogs.Axe));
    }

    [Fact]
    public void LoadFromDirectoryReadsOneDefinitionPerSubdirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-recipecatalog-{Guid.NewGuid():N}");
        var axeDir = Path.Combine(root, "axe");
        Directory.CreateDirectory(axeDir);
        File.WriteAllText(
            Path.Combine(axeDir, "axe.json"),
            """{ "output": "axe", "inputItem": "wood", "inputAmount": 5 }""");

        try
        {
            var catalog = RecipeCatalog.LoadFromDirectory(root);

            var recipe = catalog.Get(new ItemKindId("axe"));
            Assert.Equal(new ItemKindId("wood"), recipe.InputItem);
            Assert.Equal(5, recipe.InputAmount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryIgnoresNonJsonFilesInARecipeFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-recipecatalog-{Guid.NewGuid():N}");
        var axeDir = Path.Combine(root, "axe");
        Directory.CreateDirectory(axeDir);
        File.WriteAllText(
            Path.Combine(axeDir, "axe.json"),
            """{ "output": "axe", "inputItem": "wood", "inputAmount": 5 }""");
        File.WriteAllText(Path.Combine(axeDir, "notes.txt"), "this is not json and would blow up if read as such");

        try
        {
            var catalog = RecipeCatalog.LoadFromDirectory(root);

            var recipe = catalog.Get(new ItemKindId("axe"));
            Assert.Equal(5, recipe.InputAmount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryThrowsInvalidDataExceptionForAMalformedDefinition()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-recipecatalog-{Guid.NewGuid():N}");
        var axeDir = Path.Combine(root, "axe");
        Directory.CreateDirectory(axeDir);
        var filePath = Path.Combine(axeDir, "axe.json");
        File.WriteAllText(filePath, "null");

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => RecipeCatalog.LoadFromDirectory(root));

            Assert.Contains(filePath, ex.Message, StringComparison.Ordinal);
            Assert.StartsWith("Recipe definition", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
