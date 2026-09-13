using Godot;

namespace ManyWinters.Godot.Logic;

// What the sky is painted with (Content/effects/sky.gdshader, set up by SkySetup) and, from the
// same numbers, what the fog-of-war sheet fades to at its far edge: the two meet along the
// skyline, and any difference shows there as a line across the whole map.
internal static class SkyPalette
{
    public static readonly Color Zenith = new(0.26f, 0.45f, 0.70f);

    // The colour the sky has right at the skyline.
    public static readonly Color Horizon = new(0.64f, 0.76f, 0.85f);

    public static readonly Color Streak = new(0.90f, 0.94f, 0.98f);

    // What the camera finds if it tilts under the terrain's edge; matches project.godot's
    // default_clear_color.
    public static readonly Color BelowHorizon = new(0.32f, 0.29f, 0.26f);

    // How much of the horizon's colour the fog sheet gives up. The seam wants an exact match,
    // but the sheet is parchment ground, and one as blue as the sky reads as a milky sea.
    // Keeping the horizon's brightness while dropping most of its colour leaves no step in
    // value across the seam and a sheet that still reads as paper.
    private const float FogBlueGivenBack = 0.75f;

    public static readonly Color FogFar = Desaturated(Horizon, FogBlueGivenBack);

    // Toward the grey of its own average channel, so brightness survives and only colour goes.
    // 0 leaves the colour alone; 1 takes all of it.
    public static Color Desaturated(Color color, float amount)
    {
        var grey = (color.R + color.G + color.B) / 3f;

        // The grey carries the original alpha: Color.Lerp interpolates that channel too, and
        // greying toward an opaque colour would make a translucent one solid.
        return color.Lerp(new Color(grey, grey, grey, color.A), amount);
    }
}
