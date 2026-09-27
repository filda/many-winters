namespace ManyWinters.Audio;

// What is underfoot, as opposed to what was done to it. A footstep, a spade going in and a
// handful of grass being pulled are the same physics at different rates: many tiny impacts, close
// together, exciting whatever body lies under them.
public readonly record struct GranularSurface(
    float GrainsPerSecond,
    float GrainHardness, // 0..1 - each grain's brightness and shortness
    float ResonanceHz, // the body the grains excite
    float ResonanceDamping, // 0..1 - 1 is a dry scatter with no body at all, 0 a live ring
    float NoiseWash); // 0..1 - broadband bed mixed under the grains
