namespace ManyWinters.Audio;

// Fibres tearing under load. Size lowers everything; Strain is how far through the cut the tree
// is, which drives the slip rate up as it goes. Both 0..1.
public readonly record struct Creak(float Size, float Strain);
