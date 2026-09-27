namespace ManyWinters.Audio;

// Three numbers, not a MaterialDefinition: matches ImpactMaterial's leaf shape so Audio stays free
// of Core. Grit and Pressure are both in [0, 1].
public readonly record struct FrictionStroke(float Grit, float Pressure, float StrokesPerSecond);
