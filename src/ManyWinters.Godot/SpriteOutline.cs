using Godot;

namespace ManyWinters.Godot;

// A solid-color silhouette of the same texture, scaled up slightly and rendered behind the
// real sprite so its edges peek out as a rim - the standard way to fake an outline around a
// textured billboard without per-pixel edge detection.
public static class SpriteOutline
{
    private const float ScaleFactor = 1.18f;

    // Drawn before (so visually behind) the default-priority sprite it's outlining, so only
    // the rim that sticks out past the real sprite's edges is ever visible.
    private const int BehindRenderPriority = -1;

    private const string ShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_disabled;

        uniform sampler2D outline_texture : source_color, filter_linear_mipmap;
        uniform vec4 outline_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

        void fragment() {
            float shape_alpha = texture(outline_texture, UV).a;
            ALBEDO = outline_color.rgb;
            ALPHA = shape_alpha * outline_color.a;
        }
        """;

    public static Sprite3D Create(string texturePath, float worldHeight, Color outlineColor)
    {
        var texture = ResourceLoader.Load<Texture2D>(texturePath);
        var material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        material.SetShaderParameter("outline_texture", texture);
        material.SetShaderParameter("outline_color", outlineColor);

        var sprite = new Sprite3D
        {
            Texture = texture,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            MaterialOverride = material,
            RenderPriority = BehindRenderPriority,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        sprite.PixelSize = (worldHeight * ScaleFactor) / sprite.Texture.GetHeight();
        return sprite;
    }
}
