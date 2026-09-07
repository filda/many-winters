using Godot;
using ManyWinters.Godot.Logic;
using GodotEnvironment = Godot.Environment;

namespace ManyWinters.Godot.Terrain;

// The sky behind everything - the other half of the world's backdrop, alongside the terrain
// this folder builds. There was no sky at all before: the scene had no WorldEnvironment, so
// the background was the project's flat clear colour, and a single muted brown-grey behind a
// green landscape read as an overcast wall rather than as air.
//
// Painted, not simulated - see Content/effects/sky.gdshader for what it actually draws and
// why it is a hand-written shader rather than Godot's own ProceduralSkyMaterial (which puts a
// sun disk in the sky and commits to a time of day nothing else in the art does). The colours
// themselves are SkyPalette's, shared with the fog-of-war sheet that has to meet this along
// the skyline.
public static class SkySetup
{
    private const string SkyShaderPath = "res://Content/effects/sky.gdshader";

    public static void Create(Node3D parent)
    {
        var material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(SkyShaderPath) };
        material.SetShaderParameter("zenith_color", SkyPalette.Zenith);
        material.SetShaderParameter("horizon_color", SkyPalette.Horizon);
        material.SetShaderParameter("streak_color", SkyPalette.Streak);
        material.SetShaderParameter("below_horizon_color", SkyPalette.BelowHorizon);

        parent.AddChild(new WorldEnvironment
        {
            Environment = new GodotEnvironment
            {
                BackgroundMode = GodotEnvironment.BGMode.Sky,
                // Small, because nothing reads it: with ambient and reflections off (below),
                // the radiance cubemap this would otherwise build at full size is never
                // sampled by anything.
                Sky = new Sky { SkyMaterial = material, RadianceSize = Sky.RadianceSizeEnum.Size32 },
                // A sky that also lit the scene would quietly re-light every shaded surface in
                // the game (the terrain mesh is a lit StandardMaterial3D; the sprites are
                // unshaded and wouldn't move at all), so the ground would brighten and shift
                // blue while the people standing on it stayed exactly as they were. Lighting
                // stays the one DirectionalLight it has always been - putting the sky's own
                // light into the world is a separate, deliberate change to make with the art
                // in front of you, not a side effect of painting the background.
                AmbientLightSource = GodotEnvironment.AmbientSource.Disabled,
                ReflectedLightSource = GodotEnvironment.ReflectionSource.Disabled,
            },
        });
    }
}
