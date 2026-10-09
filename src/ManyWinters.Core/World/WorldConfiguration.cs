using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Maps;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.World;

// Everything a WorldState is built from but never changes while it runs: catalogs, the
// calendar-to-climate mapping and the tuning numbers (Rules). A save file stores none of it.
public sealed record WorldConfiguration(
    SpeciesCatalog SpeciesCatalog,
    ResourceCatalog ResourceCatalog,
    SkillCatalog SkillCatalog,
    RecipeCatalog RecipeCatalog,
    MaterialCatalog MaterialCatalog,
    FormCatalog FormCatalog,
    ItemCatalog ItemCatalog,
    SeasonParameters SeasonParameters,
    SimulationRules Rules)
{
    // Nothing defined, default calendar and rules - what `new WorldConfiguration { X = ... }`
    // starts from when a caller cares about one or two catalogs. The item catalog gets its own
    // empty material and form catalogs; with no items there is nothing whose weight or edge
    // could differ.
    public WorldConfiguration()
        : this(new([]), new([]), new([]), new([]), new([]), new([]), new([], new([]), new([])), SeasonParameters.Default, SimulationRules.Default)
    {
    }

    // Where water and rock lie; nothing anywhere unless the content says so, so a configuration
    // built in code needs no map data.
    public TerrainFeatures Terrain { get; init; } = TerrainFeatures.None;

    // The shipped content folder off the filesystem, for a headless runner. The Godot build
    // cannot read the filesystem this way and goes through LoadFromJson with its own reader.
    public static WorldConfiguration LoadFromDirectory(string contentRoot) =>
        LoadFromJson(catalog => JsonDefinitions.ReadDirectory(Path.Combine(contentRoot, catalog)));

    // `readCatalog` gets one catalog folder name under the content root ("resources", "skills",
    // ...) and returns its JSON documents, so the folder names live here once.
    public static WorldConfiguration LoadFromJson(Func<string, IEnumerable<(string Source, string Json)>> readCatalog)
    {
        var species = SpeciesCatalog.LoadFromJson(readCatalog("species"));

        // Materials and forms first and named: items derive weight, insulation and edge from the
        // two of them, so the item catalog needs the same instances rather than a second reading.
        var materials = MaterialCatalog.LoadFromJson(readCatalog("materials"));
        var forms = FormCatalog.LoadFromJson(readCatalog("forms"));

        return new(
            species,
            ResourceCatalog.LoadFromJson(readCatalog("resources")),
            SkillCatalog.LoadFromJson(readCatalog("skills")),
            RecipeCatalog.LoadFromJson(readCatalog("recipes")),
            materials,
            forms,
            ItemCatalog.LoadFromJson(readCatalog("items"), materials, forms),
            SeasonParameters.Default,
            SimulationRules.Default)
        {
            Terrain = TerrainFeatures.LoadFromJson(readCatalog("terrain")),
        };
    }
}
