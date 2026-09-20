namespace ManyWinters.Core.Materials;

// The worked shape an item has been brought to, as opposed to the substance it is made of
// (MaterialDefinition). Geometry lives here and substance lives there, which is what lets a
// stone lump and a stone wedge behave differently while being the same stone - see
// docs/materials-and-crafting-architecture.md section 1.
//
// Only properties something reads are defined (EdgeSharpness and HaftLeverage:
// ItemCatalog.ChoppingScoreFor / ChoppingScoreOf; LashingStrength: BindCommand); the rest
// arrive with the readers that ask for them.
public sealed record FormDefinition(
    FormId Id,
    string DisplayName,
    // How much of a cutting edge the shape presents, 0-1. Geometry only: how well that edge
    // then bites is the material's hardness, asked separately (see ItemCatalog.ChoppingScoreFor).
    float EdgeSharpness = 0f,
    // How well the shape serves to lash two things together, 0-1: long and thin binds, a lump
    // does not. Zero means this shape cannot be used as a binding at all, so content decides
    // what counts as cordage rather than the command naming one blessed form.
    float LashingStrength = 0f,
    // How much swing this shape lends to something lashed to it: a shaft turns a held stone into
    // a swung one, a lump lends nothing. Geometry only - how firmly the head is actually
    // attached is the joint's business, asked separately (see ItemCatalog.ChoppingScoreOf), so
    // the two are never rolled into one number.
    float HaftLeverage = 0f);
