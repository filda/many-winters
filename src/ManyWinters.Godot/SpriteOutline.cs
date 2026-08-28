using Godot;

namespace ManyWinters.Godot;

// A solid-color silhouette of the same texture, scaled up slightly and rendered behind the
// real sprite so its edges peek out as a rim - the standard way to fake an outline around a
// textured billboard without per-pixel edge detection.
public static class SpriteOutline
{
    // Shared by every hover outline (resource nodes, people, ...) so "the outline color" stays
    // one thing to change rather than a constant copy-pasted at each call site.
    public static readonly Color HoverColor = new(0.95f, 0.95f, 0.92f, 0.85f);

    private const float ScaleFactor = 1.18f;

    // Drawn before (so visually behind) the default-priority sprite it's outlining, so only
    // the rim that sticks out past the real sprite's edges is ever visible.
    private const int BehindRenderPriority = -1;

    // TODO: this outline still doesn't turn to face the camera quite the same way the real
    // sprite beside it does (reported as "rotated differently"). Adding render_mode billboard
    // here was tried and made it worse (rendered as a solid square) - reverted. Needs
    // diagnosing live rather than guessed at blind; leaving the original behavior in place.
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

    // Points an existing outline at a different texture - for an entity whose appearance
    // changes shape (e.g. PersonView swapping to the sideways "dead" sprite), so the outline's
    // silhouette keeps matching what it's outlining instead of tracing the old shape.
    public static void Apply(Sprite3D outline, string texturePath, float worldHeight)
    {
        var texture = ResourceLoader.Load<Texture2D>(texturePath);
        ((ShaderMaterial)outline.MaterialOverride!).SetShaderParameter("outline_texture", texture);
        outline.Texture = texture;
        outline.PixelSize = (worldHeight * ScaleFactor) / texture.GetHeight();
    }
}
