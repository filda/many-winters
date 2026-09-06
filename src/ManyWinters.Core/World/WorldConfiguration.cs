using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.World;

public sealed record WorldConfiguration(
    ResourceCatalog ResourceCatalog,
    SkillCatalog SkillCatalog,
    RecipeCatalog RecipeCatalog,
    BuildingCatalog BuildingCatalog,
    ItemCatalog ItemCatalog,
    SeasonParameters SeasonParameters)
{
    // Nothing defined at all, on the default calendar - what `new WorldConfiguration { X = ... }`
    // starts from when a caller only cares about one or two of the catalogs.
    public WorldConfiguration()
        : this(new([]), new([]), new([]), new([]), new([]), SeasonParameters.Default)
    {
    }

    // The shipped content folder, read straight off the filesystem - what the headless
    // SimulationRunner does. The Godot build can't (see JsonDefinitions), so it goes through
    // LoadFromJson below with its own reader instead.
    public static WorldConfiguration LoadFromDirectory(string contentRoot) =>
        LoadFromJson(catalog => JsonDefinitions.ReadDirectory(Path.Combine(contentRoot, catalog)));

    // `readCatalog` is handed the name of one catalog's folder under the content root
    // ("resources", "skills", ...) and returns the JSON documents found in it - so the folder
    // names live here, once, no matter who does the reading.
    public static WorldConfiguration LoadFromJson(Func<string, IEnumerable<(string Source, string Json)>> readCatalog) => new(
        ResourceCatalog.LoadFromJson(readCatalog("resources")),
        SkillCatalog.LoadFromJson(readCatalog("skills")),
        RecipeCatalog.LoadFromJson(readCatalog("recipes")),
        BuildingCatalog.LoadFromJson(readCatalog("buildings")),
        ItemCatalog.LoadFromJson(readCatalog("items")),
        SeasonParameters.Default);
}
