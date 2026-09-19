namespace ManyWinters.Core.Materials;

// The worked shape an item has been brought to, as opposed to the substance it is made of
// (MaterialDefinition). Geometry lives here and substance lives there, which is what lets a
// stone lump and a stone wedge behave differently while being the same stone - see
// docs/materials-and-crafting-architecture.md section 1.
//
// Only properties something reads are defined (EdgeSharpness: ItemCatalog.ChoppingScoreFor);
// the rest, HaftLeverage among them, arrive with the readers that ask for them.
public sealed record FormDefinition(
    FormId Id,
    string DisplayName,
    // How much of a cutting edge the shape presents, 0-1. Geometry only: how well that edge
    // then bites is the material's hardness, asked separately (see ItemCatalog.ChoppingScoreFor).
    float EdgeSharpness = 0f);
