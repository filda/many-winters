using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Views;

// Puts the hover rim (Content/effects/sprite_highlight.gdshader) on whatever the cursor is
// currently on, and takes it off again.
//
// One rim for the whole entity rather than one per layer, hung on the topmost layer's sprite:
// all of an entity's layers share the same quad and UV mapping, so the shader can trace the
// union of their silhouettes from one pass (see its own doc comment for what per-layer rims did
// to a person). The shader samples up to MaxLayers of them at once.
//
// The materials are pooled rather than owned per sprite: there are thousands of sprites and at
// most one entity is ever hovered - HoverArbiter enforces exactly that - so one material in
// hand is normally enough, and a material per sprite would mean thousands of ShaderMaterial
// instances for a highlight only one of them shows at a time.
internal static class HoverOutline
{
    private const string ShaderPath = "res://Content/effects/sprite_highlight.gdshader";

    // What hover looks like, and the whole of it. Two earlier answers are worth not repeating:
    // a second, scaled-up "rim" sprite behind the original (the removed SpriteOutline) depended
    // on depth-test and billboard-orientation behaviour that two fix attempts could not pin
    // down, and a Modulate tint could not be seen reliably - Modulate is a pure multiply and
    // this art is largely black crosshatch ink, so a large share of every sprite barely changed
    // whatever colour was picked. A size bump stood in for both until this rim existed; it is
    // gone now, because growing the thing under the cursor also moved its own click rectangle.
    private static readonly Color RimColor = new(1f, 0.85f, 0.15f);

    // In pixels of the screen: the same entity is rendered at every distance the camera has, and
    // an ink line that keeps its weight regardless is what the drawing itself does. Kept thin
    // deliberately - the rim reads as the art's own contour picked out, not as a glow over it.
    private const float RimScreenPixels = 2f;

    // As many textures as the shader has slots for. A fruit tree is the widest entity in the
    // game at four layers, and its fruit overlay is not one of the silhouette's own (see
    // SpriteEntityView.ShowHovered), so nothing today comes close to filling this.
    private const int MaxLayers = 4;

    private static Shader? _shader;
    private static readonly LendingPool<ShaderMaterial> _materials = new(NewMaterial);

    // Draws the rim on `host` until Clear takes it off, tracing the union of `textures`. They
    // are handed to the shader explicitly: a second pass has no automatic binding to what the
    // sprite itself draws, and reading them off the layers here keeps the rim in step through a
    // texture swap (PersonView lying down on death).
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
            // An unused slot is cleared rather than left pointing at whatever the last hovered
            // entity had there; the shader's own hint_default_transparent then makes it
            // contribute nothing to the silhouette.
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
        // The texture references go with it, so a pooled material cannot keep textures (and
        // whatever they own) alive while it sits unused.
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
        // (FogOfWarRenderer.OverlayRenderPriority). Hover feedback is the answer to "what is
        // under your cursor" and belongs on top of the world rather than inside it - drawn
        // under the fog, the rim lost contrast as the fog boundary moved past the thing being
        // pointed at, which looks like the line changing thickness.
        var material = new ShaderMaterial { Shader = _shader, RenderPriority = 127 };
        material.SetShaderParameter("rim_color", RimColor);
        material.SetShaderParameter("rim_pixels", RimScreenPixels);

        return material;
    }
}
