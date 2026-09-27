namespace ManyWinters.Audio;

// Both 0..1. Strength scales gain and how far the filter's centre sweeps; gustiness scales how
// fast it sweeps and how much of the octave-up whistle rides on top.
public readonly record struct WindParameters(float Strength, float Gustiness);
