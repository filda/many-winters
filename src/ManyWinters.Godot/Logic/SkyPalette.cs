using Godot;

namespace ManyWinters.Godot.Logic;

// What the sky is painted with (Content/effects/sky.gdshader, set up by SkySetup) - and, from
// the same numbers, what the fog-of-war sheet fades to at its far edge, since the two meet
// along the skyline and any difference between them shows up there as a line drawn across the
// whole map. Kept in one place precisely so they cannot drift apart.
internal static class SkyPalette
{
    public static readonly Color Zenith = new(0.26f, 0.45f, 0.70f);

    // The colour the sky has right at the skyline.
    public static readonly Color Horizon = new(0.64f, 0.76f, 0.85f);

    public static readonly Color Streak = new(0.90f, 0.94f, 0.98f);

    // What the camera finds if it tilts under the terrain's edge - the flat clear colour the
    // whole background used to be (project.godot's own default), since that is what everything
    // below the skyline has always looked like.
    public static readonly Color BelowHorizon = new(0.32f, 0.29f, 0.26f);

    // How much of the horizon's blue the fog sheet gives back. Matching the sky exactly is what
    // the seam wants, but the sheet is *ground* - an unexplored map sheet, parchment - and one
    // tinted as blue as the sky above it turned the whole unexplored world into a milky sea
    // rather than land nobody has walked yet. Keeping the horizon's brightness while dropping
    // most of its colour satisfies both: the same value across the seam, so there is no step in
    // it, but a sheet that still reads as paper.
    private const float FogBlueGivenBack = 0.75f;

    public static readonly Color FogFar = Desaturated(Horizon, FogBlueGivenBack);

    // Toward the grey of its own average channel, so brightness survives and only the colour
    // goes. 0 leaves the colour alone; 1 takes all of it.
    public static Color Desaturated(Color color, float amount)
    {
        var grey = (color.R + color.G + color.B) / 3f;

        // The grey carries the original alpha, because Color.Lerp interpolates that channel
        // too: greying toward an implicitly opaque colour would quietly make a translucent
        // one solid, which has nothing to do with taking its colour out.
        return color.Lerp(new Color(grey, grey, grey, color.A), amount);
    }
}
