using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Fog;

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
    // Smaller than the sky clouds but big enough to read as a bank of cloud, not a row of
    // bushes. The wide size spread keeps the layout from looking stamped out.
    private const float MinWorldSize = 7f;
    private const float MaxWorldSize = 20f;

    // Where the sprite's centre sits relative to the terrain, as a fraction of its height, picked
    // per cloud. The cloud art occupies roughly the middle 27%-72% of its canvas (see
    // art/generate_sprites.py), so a centre at ground level shows the upper half of
    // the puff rising out of the terrain; the top of the range lifts it clear. Standing the canvas
    // bottom on the ground (+0.5) floated the puff like a shrub, and one shared height read as a
    // row of puffs stuck into the terrain.
    private const float MinCenterAboveGroundFraction = -0.05f;
    private const float MaxCenterAboveGroundFraction = 0.3f;

    // Mean centre-to-centre spacing the scatter aims for; actual gaps vary (CloudSpotScatter).
    private const float MeanSpacingMeters = 5f;

    // Fixed for reproducibility; distinct from CloudScatter.Seed so the two layers do not share
    // a pattern.
    private const int Seed = 23;

    // Everything this type adds goes beneath its own root, never beneath Main directly:
    // composition code attaches this once and never reaches into it again.
    public Node3D Root { get; } = new() { Name = "GroundClouds" };

    private readonly FogOfWarRenderer _fogOfWar;
    private readonly Func<float, float, float> _sampleHeight;
    private readonly IReadOnlyList<CloudSpot> _candidates;
    private readonly Dictionary<int, (Sprite3D Sprite, Sprite3D Proxy)> _live = new();

    public GroundClouds(FogOfWarRenderer fogOfWar, float halfExtentMeters, Func<float, float, float> sampleHeight)
    {
        _fogOfWar = fogOfWar;
        _sampleHeight = sampleHeight;
        _candidates = CloudSpotScatter.Generate(halfExtentMeters, MeanSpacingMeters, MinWorldSize, MaxWorldSize, CloudScatter.TexturePaths.Length, Seed);

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
                // Excluded from occlusion fade, unlike the sky clouds: a faded cloud's proxy fades
                // with it, so the fog shader stops treating those pixels as cloud and paints its
                // sheet over the half-transparent puff. These sit at ground level where the view
                // target usually is, so it happened constantly.
                var aboveGround = Mathf.Lerp(MinCenterAboveGroundFraction, MaxCenterAboveGroundFraction, candidate.Lift);
                var y = _sampleHeight(candidate.X, candidate.Z) + (candidate.Size * aboveGround);
                var position = new Vector3(candidate.X, y, candidate.Z);
                var texturePath = CloudScatter.TexturePaths[candidate.TextureIndex];
                _live[i] = CloudScatter.CreateCloudWithMaskProxy(Root, texturePath, candidate.Size, position, excludeFromOcclusionFade: true);
            }
        }
    }
}
