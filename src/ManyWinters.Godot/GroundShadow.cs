using Godot;

namespace ManyWinters.Godot;

// A flat, ground-parallel shadow decal - deliberately not a billboard. Every other sprite
// here rotates to face the camera; a shadow that did the same would visibly tilt as the
// camera orbits instead of staying flat on the ground the way a real shadow does.
public static class GroundShadow
{
    private const string TexturePath = "res://Content/effects/ground_shadow.png";

    // Sits just above the terrain surface rather than exactly on it, so it never z-fights
    // with the ground mesh.
    public const float GroundOffset = 0.02f;

    // A shadow centered directly under its caster reads as floating, not grounded - there's
    // no visual separation between "the object's own base" and "its shadow." Offsetting it
    // to one side (as if from a consistent, fixed light direction) fixes that. Callers add
    // their own ground-height (and, for world-space callers, XZ) position on top of this via
    // Position +=, so this offset survives rather than being overwritten.
    private const float SideOffsetFraction = 0.35f;

    public static Sprite3D Create(float diameter)
    {
        var sprite = new Sprite3D
        {
            Texture = ResourceLoader.Load<Texture2D>(TexturePath),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            Shaded = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        sprite.PixelSize = diameter / sprite.Texture.GetWidth();
        var sideOffset = diameter * SideOffsetFraction;
        sprite.Position = new Vector3(sideOffset, 0f, sideOffset);
        return sprite;
    }
}
