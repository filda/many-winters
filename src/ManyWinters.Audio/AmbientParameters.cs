namespace ManyWinters.Audio;

// All 0..1. Season maps to these in the game, not here: this library is a leaf and must not
// learn about seasons, the same rule ImpactMaterial follows.
public readonly record struct AmbientParameters(
    float BirdDensity,
    float InsectDensity,
    float CorvidDensity,
    float RustleDensity,
    float Hush);
