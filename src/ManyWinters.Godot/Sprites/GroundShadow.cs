using Godot;

namespace ManyWinters.Godot.Sprites;

// A flat, ground-parallel shadow decal, deliberately not a billboard: a shadow that turned to
// face the camera would visibly tilt as the camera orbits.
public static class GroundShadow
{
    private const string TexturePath = "res://Content/effects/ground_shadow.png";

    // Just above the terrain surface so it never z-fights with the ground mesh.
    public const float GroundOffset = 0.02f;

    // Centred under the caster with no light-direction offset: a fixed world-space offset next
    // to a caster that always faces the camera swings around it as the view orbits. A
    // camera-relative offset recomputed per frame would fix that but is not worth the cost
    // across tens of thousands of decoration shadows.
    public static Sprite3D Create(float diameter)
    {
        var sprite = new Sprite3D
        {
            Texture = TextureCache.Get(TexturePath),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            Shaded = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        sprite.PixelSize = diameter / sprite.Texture.GetWidth();
        return sprite;
    }
}
