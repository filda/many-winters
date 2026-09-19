using ManyWinters.Core.Materials;

namespace ManyWinters.Tests.Materials;

public class FormCatalogTests
{
    private static readonly FormId Wedge = new("wedge");
    private static readonly FormId Lump = new("lump");

    [Fact]
    public void FindReturnsTheDefinitionForAKnownId()
    {
        var catalog = new FormCatalog([new FormDefinition(Wedge, "Wedge", EdgeSharpness: 1f)]);

        var definition = catalog.Find(Wedge);

        Assert.Equal("Wedge", definition?.DisplayName);
        Assert.Equal(1f, definition?.EdgeSharpness);
    }

    [Fact]
    public void FindReturnsNullForAnUnknownIdRatherThanThrowing()
    {
        var catalog = new FormCatalog([]);

        Assert.Null(catalog.Find(Wedge));
    }

    // A shape nobody described presents no edge, rather than being unusable - the same fallback
    // MaterialCatalog gives an undescribed substance.
    [Fact]
    public void AFormDescribedWithNoPropertiesPresentsNoEdge()
    {
        var catalog = new FormCatalog([new FormDefinition(Lump, "Lump")]);

        Assert.Equal(0f, catalog.Find(Lump)?.EdgeSharpness);
    }

    [Fact]
    public void LoadFromDirectoryReadsOneDefinitionPerSubdirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-formcatalog-{Guid.NewGuid():N}");
        WriteDefinition(root, "wedge", """{ "id": "wedge", "displayName": "Wedge", "edgeSharpness": 1 }""");

        try
        {
            var catalog = FormCatalog.LoadFromDirectory(root);

            var definition = catalog.Find(Wedge);
            Assert.Equal("Wedge", definition?.DisplayName);
            Assert.Equal(1f, definition?.EdgeSharpness);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryIgnoresNonJsonFilesInAFormFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-formcatalog-{Guid.NewGuid():N}");
        var formDir = WriteDefinition(root, "wedge", """{ "id": "wedge", "displayName": "Wedge", "edgeSharpness": 1 }""");
        File.WriteAllText(Path.Combine(formDir, "notes.txt"), "this is not json and would blow up if read as such");

        try
        {
            var catalog = FormCatalog.LoadFromDirectory(root);

            Assert.Equal(1f, catalog.Find(Wedge)?.EdgeSharpness);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryThrowsInvalidDataExceptionForAMalformedDefinition()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-formcatalog-{Guid.NewGuid():N}");
        var formDir = Path.Combine(root, "wedge");
        Directory.CreateDirectory(formDir);
        var filePath = Path.Combine(formDir, "wedge.json");
        File.WriteAllText(filePath, "null");

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => FormCatalog.LoadFromDirectory(root));

            Assert.Contains(filePath, ex.Message, StringComparison.Ordinal);

            // Every catalog loads through the same parser, so the message has to say which kind
            // of content it was reading, not just which file.
            Assert.StartsWith("Form definition", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string WriteDefinition(string root, string id, string json)
    {
        var formDir = Path.Combine(root, id);
        Directory.CreateDirectory(formDir);
        File.WriteAllText(Path.Combine(formDir, $"{id}.json"), json);

        return formDir;
    }
}
