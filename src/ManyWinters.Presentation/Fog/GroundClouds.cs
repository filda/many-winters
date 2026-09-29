using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Presentation.Fog;

// Low cloud lying on never-explored ground: it rings the explored area and thickens with
// distance from it, so the unknown reads as something under cloud rather than a bare sheet.
// Reuses CloudScatter's sprite-plus-mask-proxy pair so fog-of-war leaves it unpainted the same
// way; the sky clouds are untouched.
//
// Candidate spots are scattered once with Poisson-disc spacing, from the clouds' own sizes so
// they never stack, each with a fixed size, texture and roll; each fog refresh only decides
// which currently show. Sprites are created lazily and then hidden, not freed: a spot far out
// toggles as the coverage band moves past it.
public sealed class GroundClouds
{
    private readonly FogOfWarRenderer _fogOfWar;
    private readonly Func<float, float, float> _sampleHeight;
    private readonly float _minCenterAboveGroundFraction;
    private readonly float _maxCenterAboveGroundFraction;
    private readonly IReadOnlyList<CloudSpot> _candidates;
    private readonly Dictionary<int, (Sprite3D Sprite, Sprite3D Proxy)> _live = new();

    public GroundClouds(
        FogOfWarRenderer fogOfWar,
        float halfExtentMeters,
        Func<float, float, float> sampleHeight,
        float minWorldSize,
        float maxWorldSize,
        float minCenterAboveGroundFraction,
        float maxCenterAboveGroundFraction,
        float meanSpacingMeters,
        int seed,
        float minGapFactor,
        int attemptsPerTargetSpot,
        float clumpScaleMeters,
        float clumpWeight
        )
    {
        _fogOfWar = fogOfWar;
        _sampleHeight = sampleHeight;
        _minCenterAboveGroundFraction = minCenterAboveGroundFraction;
        _maxCenterAboveGroundFraction = maxCenterAboveGroundFraction;
        _candidates = CloudSpotScatter.Generate(
            halfExtentMeters,
            meanSpacingMeters,
            minWorldSize,
            maxWorldSize,
            CloudScatter.TexturePaths.Length,
            seed,
            minGapFactor,
            attemptsPerTargetSpot,
            clumpScaleMeters,
            clumpWeight
            );

        Refresh();
    }

    // Everything this type adds goes beneath its own root, never beneath Main directly:
    // composition code attaches this once and never reaches into it again.
    public Node3D Root { get; } = new() { Name = "GroundClouds" };

    // Call after the fog-of-war renderer refreshes - this reads the distance field that rebuild
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
                // Excluded from occlusion fade, unlike the sky clouds: a faded cloud's proxy fades
                // with it, so the fog shader stops treating those pixels as cloud and paints its
                // sheet over the half-transparent puff. These sit at ground level where the view
                // target usually is, so it happened constantly.
                var aboveGround = Mathf.Lerp(_minCenterAboveGroundFraction, _maxCenterAboveGroundFraction, candidate.Lift);
                var y = _sampleHeight(candidate.X, candidate.Z) + (candidate.Size * aboveGround);
                var position = new Vector3(candidate.X, y, candidate.Z);
                var texturePath = CloudScatter.TexturePaths[candidate.TextureIndex];
                _live[i] = CloudScatter.CreateCloudWithMaskProxy(Root, texturePath, candidate.Size, position, excludeFromOcclusionFade: true);
            }
        }
    }
}
