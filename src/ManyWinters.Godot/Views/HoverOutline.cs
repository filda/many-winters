using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Views;

// Puts the hover rim (Content/effects/sprite_highlight.gdshader) on whatever the cursor is on,
// and takes it off again.
//
// One rim for the whole entity, hung on the topmost layer's sprite: all layers share one quad
// and UV mapping, so the shader traces the union of up to MaxLayers silhouettes in one pass
// (per-layer rims fill a person in solid - see the shader's header).
//
// Materials are pooled: thousands of sprites, at most one hovered (HoverArbiter), so one
// material in hand is normally enough.
internal static class HoverOutline
{
    private const string ShaderPath = "res://Content/effects/sprite_highlight.gdshader";

    // The whole of what hover looks like. A Modulate tint is not an option: it is a pure
    // multiply and this art is largely black crosshatch ink, so most of a sprite barely changes.
    private static readonly Color RimColor = new(1f, 0.85f, 0.15f);

    // Screen pixels, so the line keeps its weight at every camera distance like the drawing's
    // own ink. Kept thin: the art's contour picked out, not a glow over it.
    private const float RimScreenPixels = 2f;

    // As many textures as the shader has slots for. A fruit tree is the widest entity at four
    // layers, and only two of those are traced (SpriteLayer.Outlines), so nothing comes close.
    private const int MaxLayers = 4;

    private static Shader? _shader;
    private static readonly LendingPool<ShaderMaterial> _materials = new(NewMaterial);

    // Draws the rim on `host` until Clear, tracing the union of `textures`. They are handed to
    // the shader explicitly - an overlay pass has no binding to what the sprite draws - and read
    // off the layers, which keeps the rim in step through a texture swap (PersonView on death).
    internal static void Show(Sprite3D host, IReadOnlyList<Texture2D> textures)
    {
        if (textures.Count == 0)
        {
            return;
        }

        var material = _materials.Take();
        var count = Math.Min(textures.Count, MaxLayers);
        for (var slot = 0; slot < MaxLayers; slot++)
        {
            // An unused slot is cleared rather than left pointing at the last hovered entity's
            // texture; hint_default_transparent then contributes nothing to the silhouette.
            material.SetShaderParameter($"layer_{slot}", slot < count ? Variant.From(textures[slot]) : default);
        }

        material.SetShaderParameter("layer_count", count);
        host.MaterialOverlay = material;
    }

    internal static void Clear(Sprite3D host)
    {
        if (host.MaterialOverlay is not ShaderMaterial material)
        {
            return;
        }

        host.MaterialOverlay = null;
        // Texture references go with it, so a pooled material does not keep textures alive
        // while unused.
        for (var slot = 0; slot < MaxLayers; slot++)
        {
            material.SetShaderParameter($"layer_{slot}", default(Variant));
        }

        _materials.Return(material);
    }

    private static ShaderMaterial NewMaterial()
    {
        _shader ??= ResourceLoader.Load<Shader>(ShaderPath);
        // The highest priority there is, one above the fog-of-war sheets
        // (FogOfWarRenderer.OverlayRenderPriority): drawn under the fog, the rim lost contrast
        // where the fog boundary crossed it, which looks like the line changing thickness.
        var material = new ShaderMaterial { Shader = _shader, RenderPriority = 127 };
        material.SetShaderParameter("rim_color", RimColor);
        material.SetShaderParameter("rim_pixels", RimScreenPixels);

        return material;
    }
}
