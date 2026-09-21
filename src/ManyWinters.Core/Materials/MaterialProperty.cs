namespace ManyWinters.Core.Materials;

// Which property of a substance a belief is about. An enum rather than the content-extensible
// PropertyId docs/materials-and-crafting-architecture.md section 7 sketches, for the same reason
// a verb is an enum (section 3): these are fields on MaterialDefinition, so the set is closed by
// the code that reads them and content cannot add one anyway. Naming it as if content could
// would be a promise nothing keeps.
public enum MaterialProperty
{
    Density,
    Hardness,
    Toughness,
    Flexibility,
    Elasticity,
    Fibrousness,
}
