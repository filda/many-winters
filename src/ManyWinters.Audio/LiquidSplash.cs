namespace ManyWinters.Audio;

// A foot going into standing water. Depth lowers and slows everything; Vigour is how hard it was
// hit, which sets the spray and how many bubbles come off it. Both 0..1.
public readonly record struct LiquidSplash(float Depth, float Vigour);
