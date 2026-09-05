namespace ManyWinters.Core.World;

// How thickly the presentation layer should blanket never-explored ground with low cloud,
// as a function of how far a spot is from anything the group has ever explored. The clouds
// ring the explored area (a fair share of them already right at its boundary) and thicken
// with distance until the cover is unbroken, then stop altogether far out where the fog's
// own parchment has long since faded to darkness and nothing is left to hide. Pure so the
// curve itself is testable; the Godot side only rolls a per-cloud random number against it.
public static class GroundCloudCoverage
{
    // Never closer than this to explored ground - a cloud straddling the boundary would
    // hide real, already-discovered trees and people.
    public const float HugDistanceMeters = 4f;

    // Fraction of candidate spots that carry a cloud right at the boundary.
    public const float BoundaryCoverage = 0.45f;

    // Distance past the boundary at which every candidate spot carries a cloud.
    public const float FullCoverageDistanceMeters = 30f;

    // Beyond this nothing is placed at all. The fog's own sheet fades into the sky colour
    // between 10m and 70m (fog_of_war_screen.gdshader's fade_start/end_meters, squared)
    // and is already half gone around 50m; a cloud's own art reaches up to ~9m past its
    // centre. Stopping the centres here keeps every cloud on visibly pale ground - one
    // standing on the dark, faded-out ground beyond read as a stray white puff on a table.
    public const float MaxDistanceMeters = 42f;

    public static float Coverage(float distanceMeters)
    {
        if (distanceMeters < HugDistanceMeters || distanceMeters > MaxDistanceMeters)
        {
            return 0f;
        }

        var t = Math.Clamp((distanceMeters - HugDistanceMeters) / (FullCoverageDistanceMeters - HugDistanceMeters), 0f, 1f);
        var eased = t * t * (3f - (2f * t));
        return BoundaryCoverage + ((1f - BoundaryCoverage) * eased);
    }

    // roll: this spot's own fixed random number in [0, 1) - fixed per spot so a cloud
    // doesn't flicker on and off between refreshes while nothing around it changed.
    public static bool ShouldShow(float distanceMeters, float roll) => roll < Coverage(distanceMeters);
}
