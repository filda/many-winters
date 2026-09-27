namespace ManyWinters.Audio;

// One bird's identity: the same voice should be recognisable call after call. PitchHz is the
// centre the phrase's ratios are drawn against; Brightness (0..1) scales how far each note glides
// and how much breath rides under it, so a bright voice chirps harder and hisses a touch more.
public readonly record struct BirdVoice(float PitchHz, float Brightness);
