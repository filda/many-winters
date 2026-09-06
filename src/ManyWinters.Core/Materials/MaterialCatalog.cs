using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Materials;

public sealed class MaterialCatalog
{
    private readonly Dictionary<MaterialId, MaterialDefinition> _definitions;

    public MaterialCatalog(IEnumerable<MaterialDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
    }

    // Deliberately the only lookup, where the other catalogs also offer a throwing Get: every
    // caller so far derives something for whatever items it was handed, including in a
    // minimal test world that defined no materials at all, and "made of a substance nobody
    // described" has to mean weightless rather than a crash. A Get can be added the day
    // something genuinely cannot carry on without the material.
    public MaterialDefinition? Find(MaterialId id) => _definitions.GetValueOrDefault(id);

    public static MaterialCatalog LoadFromDirectory(string rootPath)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents rather than a path so an exported Godot build, where these live
    // inside the .pck and only Godot's file access can reach them, can load them too.
    public static MaterialCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<MaterialDefinition>(documents, "Material"));
}
