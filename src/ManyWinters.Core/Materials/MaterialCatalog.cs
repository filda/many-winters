using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Materials;

public sealed class MaterialCatalog
{
    private readonly Dictionary<MaterialId, MaterialDefinition> _definitions;

    public MaterialCatalog(IEnumerable<MaterialDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
    }

    // Deliberately no throwing Get: every caller derives something for whatever items it was
    // handed, also in a test world with no materials, and an undescribed material has to mean
    // weightless rather than a crash.
    public MaterialDefinition? Find(MaterialId id) => _definitions.GetValueOrDefault(id);

    public static MaterialCatalog LoadFromDirectory(string rootPath)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents, not a path: in an exported Godot build only Godot's file access reaches
    // the content inside the .pck.
    public static MaterialCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<MaterialDefinition>(documents, "Material"));
}
