namespace ManyWinters.Audio;

// The articulation VocalTract is driven by: not a formant triple any more, but the two numbers
// that actually move in a mouth. TongueIndex is where along the tract's tongue-shaped span
// (roughly cells 12-36 of the tract's 44 total) the hump of the tongue sits - low towards the
// throat, high towards the lips. TongueDiameter is how far that hump narrows the tract: about 3.5
// is a wide-open vowel, about 0.5 a close one, and 0 or below is a full closure - a stop
// consonant, not a vowel nobody can reach. LipDiameter is the same idea at the very front of the
// tract (from the lip boundary onward): 1.5 is a neutral open mouth, 0 rounds it all the way to a
// labial closure. Both closures rely on the same physics: VocalTract reads a zeroed diameter as
// an obstruction and fires its own release transient when the shape moves off it again, which is
// what makes a stop a stop rather than a separately-authored noise burst.
public readonly record struct TractShape(float TongueIndex, float TongueDiameter, float LipDiameter);
