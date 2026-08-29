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

    // No side offset - a shadow centered directly under its caster was tried with one (as if
    // from a consistent, fixed light direction) to avoid reading as floating, but that offset
    // is fixed in *world* space while the caster itself is a billboard that only ever rotates
    // to face the camera (its actual Transform never turns - see SpritePixelHit's own doc
    // comment on that). Orbiting the camera around a fixed-world-space-offset shadow next to
    // an always-camera-facing sprite made the shadow appear to swing around the object as the
    // view angle changed - worse than the plain-floating look it was meant to fix. A per-frame
    // camera-relative offset (recomputed like the selection marker overlay was) would fix that
    // properly, but doing it for potentially tens of thousands of decoration shadows every
    // frame is exactly the per-frame-cost mistake the density/performance pass just walked
    // back - not worth it for a purely cosmetic offset.
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
        return sprite;
    }
}
