using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.World;

// Everything a WorldState is built from but never changes while it runs: the content catalogs
// (what exists), the calendar-to-climate mapping, and the simulation's tuning numbers (Rules).
// A save file stores none of this - it's handed back in on load.
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
    // Nothing defined at all, on the default calendar and rules - what
    // `new WorldConfiguration { X = ... }` starts from when a caller only cares about one or two
    // of the catalogs. The item catalog gets an empty material catalog of its own: with no items
    // defined either, there is nothing whose weight could differ between the two.
    public WorldConfiguration()
        : this(new([]), new([]), new([]), new([]), new([]), new([], new([])), SeasonParameters.Default, SimulationRules.Default)
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
    public static WorldConfiguration LoadFromJson(Func<string, IEnumerable<(string Source, string Json)>> readCatalog)
    {
        // Materials first, and named rather than inlined: items derive their weight and
        // insulation from them, so the item catalog needs the same instance rather than a
        // second reading of the same folder.
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
