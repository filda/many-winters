using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// How one page of the game's paper aged. Every panel is cut from the same sheet
// (PanelChrome.Parchment); this is what makes each of them dirty in its own way, so several open
// at once read as pages out of one chronicle rather than as one texture stamped three times.
//
// All of it follows from the panel's name, so a page looks the same in every session: weathering
// that reshuffled on reload would read as a bug rather than as paper.
internal readonly record struct PaperWeathering
{
    // What the stains are seeded with, and how broad and how dark they come out.
    internal int Seed { get; private init; }

    internal float BlotchFrequency { get; private init; }

    internal float BlotchStrength { get; private init; }

    // The printer's hatching under the stains: how far apart the strokes lie, and which way they
    // lean.
    internal int HatchSpacing { get; private init; }

    internal bool HatchRising { get; private init; }

    internal static PaperWeathering Of(string panel)
    {
        var seed = SeedOf(panel);

        return new PaperWeathering
        {
            Seed = seed,
            // Stains the size of a thumb either way; the octaves put the sand back on top of them
            // (see PanelChrome.Blotches).
            BlotchFrequency = 0.009f + (Fraction(seed, salt: 1) * 0.006f),
            // Faint on every page: past a certain strength this stops being paper and becomes
            // wallpaper, and the ink has to fight it.
            BlotchStrength = 0.16f + (Fraction(seed, salt: 2) * 0.12f),
            // Three to five pixels apart. The tile is cut to a multiple of this, so whatever the
            // spacing the strokes still repeat seamlessly.
            HatchSpacing = 3 + Pick(seed, salt: 3, count: 3),
            HatchRising = Pick(seed, salt: 4, count: 2) == 0,
        };
    }

    // FNV-1a rather than string.GetHashCode, which .NET salts per process - the same page would
    // then be stained differently every launch.
    private static int SeedOf(string panel)
    {
        var hash = 2166136261u;
        foreach (var letter in panel)
        {
            hash = unchecked((hash ^ letter) * 16777619u);
        }

        return SeedHash.Avalanche(hash);
    }

    // Each aspect is drawn off its own salt rather than off successive bits of one number, so two
    // names that came out close still differ in every way (see EntityVisualVariation.RangeFor).
    private static float Fraction(int seed, int salt) => (uint)Mixed(seed, salt) / (float)uint.MaxValue;

    private static int Pick(int seed, int salt, int count) => (int)((uint)Mixed(seed, salt) % (uint)count);

    private static int Mixed(int seed, int salt) => SeedHash.Avalanche(unchecked(((uint)seed * 0x9E3779B1u) + (uint)salt));
}
