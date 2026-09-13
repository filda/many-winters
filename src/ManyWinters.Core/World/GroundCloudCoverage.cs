namespace ManyWinters.Core.World;

// How thickly never-explored ground is blanketed with low cloud, as a function of distance
// from explored ground: densest at the boundary, thinning to nothing where the fog sheet has
// faded. Pure so the curve is testable; the Godot side compares a per-spot roll against it
// (CloudSpotScatter gives that roll a spatial grain so the thinning is clumpy).
public static class GroundCloudCoverage
{
    // Never closer than this to explored ground - a cloud straddling the boundary would
    // hide real, already-discovered trees and people.
    public const float HugDistanceMeters = 4f;

    // Beyond this nothing is placed. The fog sheet fades into the sky between 10m and 70m
    // (fog_of_war_screen.gdshader fade_start/end_meters) and is half gone around 50m; cloud art
    // reaches ~9m past its centre, so this keeps every cloud on visibly pale ground.
    public const float MaxDistanceMeters = 42f;

    // Shapes the thinning: above 1 keeps the cover full for a while past the boundary
    // before it falls off, rather than starting to thin immediately.
    private const float FalloffExponent = 1.6f;

    public static float Coverage(float distanceMeters)
    {
        // Stryker disable once Equality: at exactly the maximum the curve below is pow(0, e) = 0 either way
        if (distanceMeters < HugDistanceMeters || distanceMeters > MaxDistanceMeters)
        {
            return 0f;
        }

        var t = (distanceMeters - HugDistanceMeters) / (MaxDistanceMeters - HugDistanceMeters);
        return MathF.Pow(1f - t, FalloffExponent);
    }

    // roll is fixed per spot so a cloud does not flicker between refreshes while nothing changed.
    public static bool ShouldShow(float distanceMeters, float roll) => roll < Coverage(distanceMeters);
}
