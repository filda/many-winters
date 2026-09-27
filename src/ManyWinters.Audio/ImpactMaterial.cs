namespace ManyWinters.Audio;

// Three numbers, not a MaterialDefinition: this library is a leaf that the game, the tool and the
// tests all reference, and it must not drag the simulation in. Hardness, Toughness in [0, 1];
// Density relative to water (1.0).
public readonly record struct ImpactMaterial(float Hardness, float Toughness, float Density);
