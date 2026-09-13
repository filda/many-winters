namespace ManyWinters.Core.World;

// One candidate spot for a low cloud; GroundCloudCoverage decides whether it currently shows.
// Roll is the spot's fixed random number in [0, 1) for that decision, TextureIndex picks the
// sprite, Lift in [0, 1) is how high the puff rides (the presentation layer maps it onto its
// own range) - all at one height they read as stuck into the terrain in a row.
public readonly record struct CloudSpot(float X, float Z, float Size, int TextureIndex, float Roll, float Lift);

// Scatters cloud spots with the blue-noise look of a Poisson-disc distribution: random
// candidates are kept only if they clear every placed neighbour by a gap derived from both
// sizes, so puffs never sit glued together and no grid shows through.
public static class CloudSpotScatter
{
    // Two spots must be at least this fraction of their combined size apart. Cloud art spans
    // ~90% of its canvas, so 0.45 would be edge-to-edge; well under that lets neighbours overlap
    // by more than half a width, as puffs in a bank of low cloud do. The gap, not the requested
    // count, is what caps the density.
    private const float MinGapFactor = 0.2f;

    // Random candidates tried per spot wanted: enough headroom for rejection sampling to
    // saturate the space without looping long over a full map.
    private const int AttemptsPerTargetSpot = 6;

    public static IReadOnlyList<CloudSpot> Generate(float halfExtentMeters, float meanSpacingMeters, float minSize, float maxSize, int textureCount, int seed)
    {
        var rng = new Random(seed);
        var extent = 2f * halfExtentMeters;
        var target = (int)((extent * extent) / (meanSpacingMeters * meanSpacingMeters));

        // Spatial hash keyed by a cell at least as wide as the largest possible gap, so a
        // candidate only ever has to check the 3x3 cells around it.
        // Stryker disable once Arithmetic: any cell at least this wide rejects the same candidates - bucket count, not layout
        var cell = MathF.Max(2f * MinGapFactor * maxSize, 1f);
        var index = new SpatialSpacingIndex<CloudSpot>(cell, spot => spot.X, spot => spot.Z);
        var spots = new List<CloudSpot>(target);

        // Stryker disable once Equality: a give-up budget, not a quantity - one more roll against an already saturated map
        for (var attempt = 0; attempt < target * AttemptsPerTargetSpot && spots.Count < target; attempt++)
        {
            var size = minSize + ((float)rng.NextDouble() * (maxSize - minSize));
            var x = ((float)rng.NextDouble() * extent) - halfExtentMeters;
            var z = ((float)rng.NextDouble() * extent) - halfExtentMeters;

            if (index.IsTooClose(x, z, existing => MinGap(size, existing.Size)))
            {
                continue;
            }

            var spot = new CloudSpot(x, z, size, rng.Next(textureCount), ClumpyRoll(x, z, (float)rng.NextDouble(), seed), (float)rng.NextDouble());
            spots.Add(spot);
            index.Add(spot);
        }

        return spots;
    }

    public static float MinGap(float sizeA, float sizeB) => MinGapFactor * (sizeA + sizeB);

    // Wavelength of the spatial grain mixed into each spot's roll, in metres - the size of
    // the clumps and gaps the thinning cover breaks into.
    private const float ClumpScaleMeters = 22f;

    // How much of the roll is spatial grain rather than independent chance. An independent
    // roll thins the cover as an even sprinkle, which still reads as regular; shared grain
    // makes whole patches drop out together, so the cover tears into clumps and openings.
    private const float ClumpWeight = 0.6f;

    // Blends the spot's own independent chance with smooth value noise sampled at its
    // position, staying in [0, 1).
    public static float ClumpyRoll(float x, float z, float independent, int seed)
    {
        var grain = ValueNoise((x / ClumpScaleMeters) + (seed * 0.731f), (z / ClumpScaleMeters) - (seed * 0.377f));
        return Math.Clamp(((1f - ClumpWeight) * independent) + (ClumpWeight * grain), 0f, 0.99999f);
    }

    private static float Hash(int ix, int iz)
    {
        unchecked
        {
            var h = (uint)(ix * 374761393) + (uint)(iz * 668265263);
            // Stryker disable once Bitwise: h is uint, so >> and >>> are the same operation
            h = (h ^ (h >> 13)) * 1274126177u;
            // Stryker disable once Bitwise: as above
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    private static float ValueNoise(float x, float z)
    {
        var ix = (int)MathF.Floor(x);
        var iz = (int)MathF.Floor(z);
        var fx = x - ix;
        var fz = z - iz;
        var ux = fx * fx * (3f - (2f * fx));
        var uz = fz * fz * (3f - (2f * fz));
        var top = Hash(ix, iz) + ((Hash(ix + 1, iz) - Hash(ix, iz)) * ux);
        var bottom = Hash(ix, iz + 1) + ((Hash(ix + 1, iz + 1) - Hash(ix, iz + 1)) * ux);
        return top + ((bottom - top) * uz);
    }
}
