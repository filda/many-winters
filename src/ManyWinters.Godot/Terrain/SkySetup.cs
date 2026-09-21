using Godot;
using ManyWinters.Godot.Logic;
using GodotEnvironment = Godot.Environment;

namespace ManyWinters.Godot.Terrain;

// The sky backdrop, painted via a hand-written shader (Content/effects/sky.gdshader) rather
// than ProceduralSkyMaterial. Colours come from SkyPalette, shared with the fog-of-war sheet
// that meets this along the skyline.
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
                // Small: with ambient and reflections off (below) nothing samples the radiance cubemap.
                Sky = new Sky { SkyMaterial = material, RadianceSize = Sky.RadianceSizeEnum.Size32 },
                // Ambient/reflected sky lighting would re-light the terrain mesh while unshaded
                // sprites stayed as they were, so lighting stays the one DirectionalLight.
                AmbientLightSource = GodotEnvironment.AmbientSource.Disabled,
                ReflectedLightSource = GodotEnvironment.ReflectionSource.Disabled,
            },
        });
    }
}
