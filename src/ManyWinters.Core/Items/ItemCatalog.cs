using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Items;

public sealed class ItemCatalog
{
    private readonly Dictionary<ItemKindId, ItemDefinition> _definitions;
    private readonly MaterialCatalog _materials;
    private readonly FormCatalog _forms;

    public ItemCatalog(IEnumerable<ItemDefinition> definitions, MaterialCatalog materials, FormCatalog forms)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
        _materials = materials;
        _forms = forms;
    }

    public ItemDefinition Get(ItemKindId id) => _definitions[id];

    // Insulation is the material's, whatever it was made into. An undescribed item or material
    // insulates nothing.
    public float InsulationFor(ItemKindId id) =>
        _definitions.TryGetValue(id, out var definition)
            ? _materials.Find(definition.Material)?.Insulation ?? 0f
            : 0f;

    // Density from the material, bulk from the item. Same missing-definition fallback as
    // InsulationFor: an undescribed item is weightless rather than unusable.
    public float WeightFor(ItemKindId id) =>
        _definitions.TryGetValue(id, out var definition) ? WeightOf(definition) : 0f;

    // Edge times hardness times the square root of weight (see
    // docs/materials-and-crafting-architecture.md section 4): the shape has to present an edge,
    // the substance has to be hard enough to hold it, and mass behind the blow does the rest.
    // A raw lump of the very same stone the axe head is made of therefore scores nothing, which
    // is the whole point of keeping geometry and substance apart.
    //
    // Raw stock is never hafted - a thing held as a count is a thing held in the bare hand - so
    // the HaftLeverage term is only ever 1 here; a made object is scored by ChoppingScoreOf.
    //
    // GatherCommand and FellCommand read this instead of a per-skill authored "tool" item kind.
    public float ChoppingScoreFor(ItemKindId id) =>
        _definitions.TryGetValue(id, out var definition)
            ? (_forms.Find(definition.Form)?.EdgeSharpness ?? 0f)
              * (_materials.Find(definition.Material)?.Hardness ?? 0f)
              * MathF.Sqrt(WeightOf(definition))
            : 0f;

    // The same question of a made thing, and section 4's formula in full at last:
    //
    //     EdgeSharpness * Hardness * sqrt(Mass) * HaftLeverage
    //
    // A single piece is a thing held in the hand, so it is scored exactly as raw stock is, times
    // how well it was made - a wedge knapped by a beginner is a poor edge, and that is where
    // practice shows in use rather than only in a number on a card.
    //
    // A bound thing is scored both ways round, because which part is the head and which the haft
    // is not declared anywhere: the object simply is whichever reading serves it better. What the
    // head gains from the haft is the haft's own leverage narrowed by the joint holding them
    // together - a head that wobbles is a head swung by hand, however long the shaft.
    public float ChoppingScoreOf(Assembly assembly) => assembly switch
    {
        Assembly.Part part => ChoppingScoreOf(part),
        Assembly.Joined joined => Math.Max(
            ChoppingScoreOf(joined.Left) * (1f + (LeverageOf(joined.Right) * joined.JointStrength)),
            ChoppingScoreOf(joined.Right) * (1f + (LeverageOf(joined.Left) * joined.JointStrength))),
        _ => 0f,
    };

    private float ChoppingScoreOf(Assembly.Part part) =>
        (_forms.Find(part.Form)?.EdgeSharpness ?? 0f)
        * (_materials.Find(part.Material)?.Hardness ?? 0f)
        * MathF.Sqrt(part.Weight(_materials))
        * part.Quality;

    // What this lends to something lashed to it. A made thing lends the best its parts do, so a
    // shaft stays a shaft after something else has been tied to its other end.
    private float LeverageOf(Assembly assembly) => assembly switch
    {
        Assembly.Part part => _forms.Find(part.Form)?.HaftLeverage ?? 0f,
        Assembly.Joined joined => Math.Max(LeverageOf(joined.Left), LeverageOf(joined.Right)),
        _ => 0f,
    };

    // An assembly weighs itself, but only this catalog knows the substances behind its parts -
    // so the weighing of both tiers is asked for in one place, and Inventory.TotalWeight needs
    // no second catalog to add a worked thing to a stack of raw ones.
    public float WeightOf(Assembly assembly) => assembly.Weight(_materials);

    // What this kind turns into when worked with the given verb, or null if it answers to no
    // such verb (see FormTransition).
    public FormTransition? TransitionFor(ItemKindId id, TechniqueId verb) =>
        _definitions.TryGetValue(id, out var definition)
            ? definition.Transitions?.FirstOrDefault(transition => transition.Verb == verb)
            : null;

    private float WeightOf(ItemDefinition definition) => (_materials.Find(definition.Material)?.Density ?? 0f) * definition.Volume;

    public float HungerRestoredPerUnitFor(ItemKindId id) => _definitions.TryGetValue(id, out var definition) ? definition.HungerRestoredPerUnit : 0f;

    public float CarryCapacityBonusFor(ItemKindId id) => _definitions.TryGetValue(id, out var definition) ? definition.CarryCapacityBonus : 0f;

    public static ItemCatalog LoadFromDirectory(string rootPath, MaterialCatalog materials, FormCatalog forms)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath), materials, forms);

    // Takes documents, not a path: in an exported Godot build only Godot's file access reaches
    // the content inside the .pck.
    public static ItemCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents, MaterialCatalog materials, FormCatalog forms)
        => new(JsonDefinitions.Parse<ItemDefinition>(documents, "Item"), materials, forms);
}
