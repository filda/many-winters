using System;
using System.Collections.Generic;
using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

// Low cloud lying on never-explored ground: it rings the explored area and thickens with
// distance from it (GroundCloudCoverage has the curve), so the unknown reads as
// "there's something out there under the cloud" rather than a bare sheet of parchment.
// CloudScatter's sky clouds are untouched and stay where they are; these reuse its
// sprite-plus-mask-proxy pair so fog-of-war leaves them unpainted the same way.
//
// Candidate spots are scattered once, on a jittered grid across the whole map, each with
// its own fixed size, texture and random roll; every fog refresh only decides which of
// them currently show. Sprites are created lazily the first time a spot shows and merely
// hidden when it stops (the explored area only ever grows, so a hidden spot near camp
// never comes back - but a spot far out toggles as the coverage band moves past it, and
// re-creating nodes for that would be needless churn).
public sealed class GroundClouds
{
    // Smaller than the sky's sizes but still big enough that a handful of them read as a
    // bank of cloud, not a row of bushes.
    private const float MinWorldSize = 9f;
    private const float MaxWorldSize = 18f;

    // Where the sprite's centre sits relative to the terrain, as a fraction of its height.
    // The cloud art only occupies roughly the middle 27%-72% of its canvas (the rest is
    // transparent margin - see art/generate_sprites.py's cloud lobe layouts), so a centre
    // right at ground level shows the upper half of the actual puff rising out of the
    // terrain like low fog. Standing the canvas bottom on the ground (+0.5) floated the
    // puff a quarter of its height in the air like a row of pale shrubs, and +0.2 (tried
    // next) left only the transparent top margin above ground - the clouds vanished.
    private const float CenterAboveGroundFraction = 0.05f;

    private const float CandidateSpacingMeters = 14f;

    // Fixed for reproducibility, like every other scatter here; distinct from
    // CloudScatter.Seed so the two layers don't share a pattern.
    private const int Seed = 23;

    private readonly record struct Candidate(float X, float Z, float Size, string TexturePath, float Roll);

    private readonly Node3D _parent;
    private readonly FogOfWarRenderer _fogOfWar;
    private readonly Func<float, float, float> _sampleHeight;
    private readonly List<Candidate> _candidates = new();
    private readonly Dictionary<int, (Sprite3D Sprite, Sprite3D Proxy)> _live = new();

    public GroundClouds(Node3D parent, FogOfWarRenderer fogOfWar, float halfExtentMeters, Func<float, float, float> sampleHeight)
    {
        _parent = parent;
        _fogOfWar = fogOfWar;
        _sampleHeight = sampleHeight;

        var rng = new RandomNumberGenerator { Seed = Seed };
        for (var z = -halfExtentMeters; z < halfExtentMeters; z += CandidateSpacingMeters)
        {
            for (var x = -halfExtentMeters; x < halfExtentMeters; x += CandidateSpacingMeters)
            {
                _candidates.Add(new Candidate(
                    x + rng.RandfRange(0f, CandidateSpacingMeters),
                    z + rng.RandfRange(0f, CandidateSpacingMeters),
                    rng.RandfRange(MinWorldSize, MaxWorldSize),
                    CloudScatter.TexturePaths[rng.RandiRange(0, CloudScatter.TexturePaths.Length - 1)],
                    rng.Randf()));
            }
        }

        Refresh();
    }

    // Call after FogOfWarRenderer.Refresh - this reads the distance field that rebuild
    // just produced.
    public void Refresh()
    {
        for (var i = 0; i < _candidates.Count; i++)
        {
            var candidate = _candidates[i];
            var distance = _fogOfWar.DistanceToExploredMeters(candidate.X, candidate.Z);
            var show = GroundCloudCoverage.ShouldShow(distance, candidate.Roll);

            if (_live.TryGetValue(i, out var pair))
            {
                pair.Sprite.Visible = show;
                pair.Proxy.Visible = show;
            }
            else if (show)
            {
                // Excluded from occlusion fade, unlike the sky clouds: a faded cloud's mask
                // proxy fades with it, so the fog shader stops treating those pixels as
                // cloud and paints its sheet over the half-transparent puff - a ghost cloud
                // in the fog's own colour. These sit at ground level right where the view
                // target usually is, so that happened constantly; never fading them keeps
                // the real sprite and the mask in agreement.
                var y = _sampleHeight(candidate.X, candidate.Z) + (candidate.Size * CenterAboveGroundFraction);
                var position = new Vector3(candidate.X, y, candidate.Z);
                _live[i] = CloudScatter.CreateCloudWithMaskProxy(_parent, candidate.TexturePath, candidate.Size, position, excludeFromOcclusionFade: true);
            }
        }
    }
}
