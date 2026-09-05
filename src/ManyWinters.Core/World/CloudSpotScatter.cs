namespace ManyWinters.Core.World;

// One candidate spot for a low cloud (GroundCloudCoverage decides whether it currently
// shows). Roll is the spot's own fixed random number in [0, 1) for that decision;
// TextureIndex picks which cloud sprite it uses.
public readonly record struct CloudSpot(float X, float Z, float Size, int TextureIndex, float Roll);

// Scatters cloud spots across the map with the irregular, blue-noise look of a Poisson-disc
// distribution: random candidates are kept only if they clear every already-placed
// neighbour by a gap derived from both clouds' own sizes, so two puffs never sit glued
// together yet no grid shows through either. A jittered grid (the first version) kept
// every spot inside its own cell, which still read as rows once enough of them showed.
public static class CloudSpotScatter
{
    // Two spots must be at least this fraction of their combined size apart. A cloud's art
    // spans ~90% of its canvas width, so 0.45 would be edge-to-edge; well under that lets
    // neighbours overlap by more than half a width - the way puffs in a bank of low cloud
    // merge - while still stopping two from sitting on the same spot. 0.4, 0.36 and 0.27
    // all kept the cover a row of separate pillows no matter how many spots were
    // requested, because the gap itself, not the count, was capping the density.
    public const float MinGapFactor = 0.2f;

    // How many random candidates to try per spot ultimately wanted. Rejection sampling
    // needs headroom; this saturates the available space in practice without looping for
    // long over a map that's already full.
    private const int AttemptsPerTargetSpot = 6;

    public static IReadOnlyList<CloudSpot> Generate(float halfExtentMeters, float meanSpacingMeters, float minSize, float maxSize, int textureCount, int seed)
    {
        var rng = new Random(seed);
        var extent = 2f * halfExtentMeters;
        var target = (int)((extent * extent) / (meanSpacingMeters * meanSpacingMeters));

        // Spatial hash keyed by a cell at least as wide as the largest possible gap, so a
        // candidate only ever has to check the 3x3 cells around it.
        var cell = MathF.Max(2f * MinGapFactor * maxSize, 1f);
        var buckets = new Dictionary<(int, int), List<CloudSpot>>();
        var spots = new List<CloudSpot>(target);

        for (var attempt = 0; attempt < target * AttemptsPerTargetSpot && spots.Count < target; attempt++)
        {
            var size = minSize + ((float)rng.NextDouble() * (maxSize - minSize));
            var x = ((float)rng.NextDouble() * extent) - halfExtentMeters;
            var z = ((float)rng.NextDouble() * extent) - halfExtentMeters;

            var cx = (int)MathF.Floor(x / cell);
            var cz = (int)MathF.Floor(z / cell);
            if (IsTooClose(buckets, cx, cz, x, z, size))
            {
                continue;
            }

            var spot = new CloudSpot(x, z, size, rng.Next(textureCount), ClumpyRoll(x, z, (float)rng.NextDouble(), seed));
            spots.Add(spot);
            if (!buckets.TryGetValue((cx, cz), out var bucket))
            {
                bucket = new List<CloudSpot>();
                buckets[(cx, cz)] = bucket;
            }

            bucket.Add(spot);
        }

        return spots;
    }

    public static float MinGap(float sizeA, float sizeB) => MinGapFactor * (sizeA + sizeB);

    // Wavelength of the spatial grain mixed into each spot's roll, in metres - the size of
    // the clumps and gaps the thinning cover breaks into.
    public const float ClumpScaleMeters = 22f;

    // How much of the roll is spatial grain rather than independent chance. A purely
    // independent roll thins the cover as an even sprinkle (every spot equally likely to
    // vanish), which on a Poisson-disc layout still reads as regular; sharing part of the
    // roll between neighbours makes whole patches drop out together, so the cover tears
    // into clumps and openings the way real low cloud does.
    public const float ClumpWeight = 0.6f;

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
            h = (h ^ (h >> 13)) * 1274126177u;
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

    private static bool IsTooClose(Dictionary<(int, int), List<CloudSpot>> buckets, int cx, int cz, float x, float z, float size)
    {
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (!buckets.TryGetValue((cx + dx, cz + dz), out var bucket))
                {
                    continue;
                }

                foreach (var other in bucket)
                {
                    var gap = MinGap(size, other.Size);
                    var ddx = other.X - x;
                    var ddz = other.Z - z;
                    if ((ddx * ddx) + (ddz * ddz) < gap * gap)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
