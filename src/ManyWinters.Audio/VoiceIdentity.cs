namespace ManyWinters.Audio;

// One person's voice, fixed for as long as they live and gone with them: the game derives this
// from a CreatureId rather than storing it, the same way EntityVisualVariation derives a sprite's
// look. PitchHz is the glottal fundamental, roughly 80 (a big man) to 260 (a child); Tract in
// [0, 1] is formant scale, a short tract to a long one; Roughness in [0, 1] is jitter on the
// glottal train - age, strain, a worn voice; Breath in [0, 1] is how much of the source is
// aperiodic.
public readonly record struct VoiceIdentity(float PitchHz, float Tract, float Roughness, float Breath);
