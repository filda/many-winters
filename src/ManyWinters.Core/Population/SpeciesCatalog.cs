using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Population;

public sealed class SpeciesCatalog
{
    private readonly Dictionary<SpeciesId, SpeciesDefinition> _definitions;

    public SpeciesCatalog(IEnumerable<SpeciesDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
    }

    public SpeciesDefinition Get(SpeciesId id) => _definitions[id];

    public static SpeciesCatalog LoadFromDirectory(string rootPath)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents, not a path: in an exported Godot build only Godot's file access reaches
    // the content inside the .pck.
    public static SpeciesCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<SpeciesDefinition>(documents, "Species"));
}
