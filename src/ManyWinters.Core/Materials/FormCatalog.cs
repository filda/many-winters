using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Materials;

public sealed class FormCatalog
{
    private readonly Dictionary<FormId, FormDefinition> _definitions;

    public FormCatalog(IEnumerable<FormDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
    }

    // Deliberately no throwing Get, for the same reason MaterialCatalog has none: a caller
    // derives something for whatever items it was handed, also in a test world with no forms
    // described, and an undescribed shape has to mean "affords nothing" rather than a crash.
    public FormDefinition? Find(FormId id) => _definitions.GetValueOrDefault(id);

    public static FormCatalog LoadFromDirectory(string rootPath)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents, not a path: in an exported Godot build only Godot's file access reaches
    // the content inside the .pck.
    public static FormCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<FormDefinition>(documents, "Form"));
}
