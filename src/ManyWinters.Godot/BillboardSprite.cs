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
    public static Sprite3D Create(string texturePath, float worldHeight, Color fallbackColor)
    {
        var sprite = new Sprite3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
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
