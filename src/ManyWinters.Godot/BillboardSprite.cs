using Godot;

namespace ManyWinters.Godot;

// Sprite art is looked up by convention next to the entity's other content files, so a
// new kind only has to drop a PNG into its own folder to become visible.
public static class BillboardSprite
{
    private const int PlaceholderPixels = 8;

    private static ImageTexture? _placeholder;

    // Creates a billboarded sprite whose on-screen height matches worldHeight. When the
    // texture is missing the sprite falls back to a flat quad tinted with fallbackColor, so
    // a kind without art is still visible and clickable.
    //
    // alphaCut/renderPriority default to the normal single-sprite case (opaque, depth-sorted
    // like any other 3D object). A layer meant to composite on top of another sprite at the
    // same position - e.g. ResourceNodeView's fruit overlay - has no defined draw order
    // against it under OpaquePrepass (both are at the same depth), so it needs standard alpha
    // blending (Disabled) plus a higher renderPriority to reliably draw second/on top, the
    // same technique SpriteOutline already uses to draw behind instead.
    public static Sprite3D Create(
        string texturePath,
        float worldHeight,
        Color fallbackColor,
        SpriteBase3D.AlphaCutMode alphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
        int renderPriority = 0)
    {
        var sprite = new Sprite3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            // LinearMipmap, not Nearest: the art is now illustrated engraving detail (fine
            // crosshatching), not deliberate hard-edged pixel art. Without a mip chain, that
            // fine detail aliases into shimmering noise once a sprite is small on screen -
            // mipmaps let minified sprites sample a properly pre-blurred, smaller version
            // instead of resampling the full-detail texture at a handful of screen pixels.
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            // OpaquePrepass, not Discard: Discard is a hard alpha-test cutoff with no
            // blending at all, which throws away every soft anti-aliased/shadow edge pixel
            // the new art actually has (each edge pixel snaps to either fully opaque or
            // fully invisible). OpaquePrepass keeps Discard's correct depth-sorting behavior
            // for overlapping billboards while still alpha-blending the soft edge on top.
            AlphaCut = alphaCut,
            RenderPriority = renderPriority,
            Shaded = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        Apply(sprite, texturePath, worldHeight, fallbackColor);
        return sprite;
    }

    // Points an existing sprite at a different texture, keeping its world height stable.
    public static void Apply(Sprite3D sprite, string texturePath, float worldHeight, Color fallbackColor)
    {
        var texture = ResourceLoader.Exists(texturePath)
            ? ResourceLoader.Load<Texture2D>(texturePath)
            : null;

        if (texture is null)
        {
            GD.PushWarning($"Sprite texture '{texturePath}' not found; falling back to a flat colour quad.");
        }

        sprite.Texture = texture ?? Placeholder();
        sprite.Modulate = texture is null ? fallbackColor : Colors.White;
        sprite.PixelSize = worldHeight / sprite.Texture.GetHeight();
    }

    private static ImageTexture Placeholder()
    {
        if (_placeholder is not null)
        {
            return _placeholder;
        }

        var image = Image.CreateEmpty(PlaceholderPixels, PlaceholderPixels, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        _placeholder = ImageTexture.CreateFromImage(image);
        return _placeholder;
    }
}
