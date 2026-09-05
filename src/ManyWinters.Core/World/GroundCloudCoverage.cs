namespace ManyWinters.Core.World;

// How thickly the presentation layer should blanket never-explored ground with low cloud,
// as a function of how far a spot is from anything the group has ever explored. Densest
// right at the explored boundary - a bank of cloud pressing in around the party - and
// thinning outward until nothing is placed where the fog's own sheet has faded away. Pure
// so the curve itself is testable; the Godot side only compares a per-spot random roll
// against it (CloudSpotScatter gives that roll a spatial grain so the thinning is clumpy,
// not an even sprinkle).
public static class GroundCloudCoverage
{
    // Never closer than this to explored ground - a cloud straddling the boundary would
    // hide real, already-discovered trees and people.
    public const float HugDistanceMeters = 4f;

    // Beyond this nothing is placed at all. The fog's own sheet fades into the sky colour
    // between 10m and 70m (fog_of_war_screen.gdshader's fade_start/end_meters, squared)
    // and is already half gone around 50m; a cloud's own art reaches up to ~9m past its
    // centre. Stopping the centres here keeps every cloud on visibly pale ground - one
    // standing on the dark, faded-out ground beyond read as a stray white puff on a table.
    public const float MaxDistanceMeters = 42f;

    // Shapes the thinning: above 1 keeps the cover full for a while past the boundary
    // before it falls off, rather than starting to thin immediately.
    private const float FalloffExponent = 1.6f;

    public static float Coverage(float distanceMeters)
    {
        if (distanceMeters < HugDistanceMeters || distanceMeters > MaxDistanceMeters)
        {
            return 0f;
        }

        var t = (distanceMeters - HugDistanceMeters) / (MaxDistanceMeters - HugDistanceMeters);
        return MathF.Pow(1f - t, FalloffExponent);
    }

    // roll: this spot's own fixed number in [0, 1) - fixed per spot so a cloud doesn't
    // flicker on and off between refreshes while nothing around it changed.
    public static bool ShouldShow(float distanceMeters, float roll) => roll < Coverage(distanceMeters);
}
