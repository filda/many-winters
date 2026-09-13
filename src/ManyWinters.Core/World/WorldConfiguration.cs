using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.World;

// Everything a WorldState is built from but never changes while it runs: catalogs, the
// calendar-to-climate mapping and the tuning numbers (Rules). A save file stores none of it.
public sealed record WorldConfiguration(
    ResourceCatalog ResourceCatalog,
    SkillCatalog SkillCatalog,
    RecipeCatalog RecipeCatalog,
    BuildingCatalog BuildingCatalog,
    MaterialCatalog MaterialCatalog,
    ItemCatalog ItemCatalog,
    SeasonParameters SeasonParameters,
    SimulationRules Rules)
{
    // Nothing defined, default calendar and rules - what `new WorldConfiguration { X = ... }`
    // starts from when a caller cares about one or two catalogs. The item catalog gets its own
    // empty material catalog; with no items there is nothing whose weight could differ.
    public WorldConfiguration()
        : this(new([]), new([]), new([]), new([]), new([]), new([], new([])), SeasonParameters.Default, SimulationRules.Default)
    {
    }

    // The shipped content folder off the filesystem (the headless SimulationRunner). The Godot
    // build cannot (see JsonDefinitions) and goes through LoadFromJson with its own reader.
    public static WorldConfiguration LoadFromDirectory(string contentRoot) =>
        LoadFromJson(catalog => JsonDefinitions.ReadDirectory(Path.Combine(contentRoot, catalog)));

    // `readCatalog` gets one catalog folder name under the content root ("resources", "skills",
    // ...) and returns its JSON documents, so the folder names live here once.
    public static WorldConfiguration LoadFromJson(Func<string, IEnumerable<(string Source, string Json)>> readCatalog)
    {
        // Materials first and named: items derive weight and insulation from them, so the item
        // catalog needs the same instance rather than a second reading.
        var materials = MaterialCatalog.LoadFromJson(readCatalog("materials"));

        return new(
            ResourceCatalog.LoadFromJson(readCatalog("resources")),
            SkillCatalog.LoadFromJson(readCatalog("skills")),
            RecipeCatalog.LoadFromJson(readCatalog("recipes")),
            BuildingCatalog.LoadFromJson(readCatalog("buildings")),
            materials,
            ItemCatalog.LoadFromJson(readCatalog("items"), materials),
            SeasonParameters.Default,
            SimulationRules.Default);
    }
}
