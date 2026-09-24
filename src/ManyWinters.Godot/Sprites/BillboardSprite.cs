using Godot;

namespace ManyWinters.Godot.Sprites;

// Sprite art is looked up by convention next to the entity's other content files, so a
// new kind only has to drop a PNG into its own folder to become visible.
public static class BillboardSprite
{
    private const int PlaceholderPixels = 8;

    private static ImageTexture? _placeholder;

    // Every live billboard, so the occlusion fade scan iterates a flat list each frame instead of
    // scanning the whole scene tree. Self-cleaning via TreeExited: a sprite only leaves the tree
    // when its owning view is freed.
    private static readonly HashSet<Sprite3D> _liveSprites = new();

    // Tree trunk and branch layers (ResourceNodeView) and people (PersonView) never fade under occlusion yet remain in
    // LiveSprites: its contract is "every billboard that exists", not "every fadeable one".
    private static readonly HashSet<Sprite3D> _excludedFromOcclusionFade = new();

    // Billboards Main's occlusion fade is currently ghosting. A set rather than a read of
    // Modulate.A: SpritePixelHit needs it (what the player sees through, they click through),
    // and Modulate is written by the fade and the views' tinting alike, so it is not a
    // reliable record.
    private static readonly HashSet<Sprite3D> _occlusionFaded = new();

    public static IReadOnlyCollection<Sprite3D> LiveSprites => _liveSprites;

    public static IReadOnlyCollection<Sprite3D> OcclusionFadedSprites => _occlusionFaded;

    public static bool IsExcludedFromOcclusionFade(Sprite3D sprite) => _excludedFromOcclusionFade.Contains(sprite);

    public static bool IsOcclusionFaded(Sprite3D sprite) => _occlusionFaded.Contains(sprite);

    public static void SetOcclusionFaded(Sprite3D sprite, bool faded)
    {
        if (faded)
        {
            _occlusionFaded.Add(sprite);
        }
        else
        {
            _occlusionFaded.Remove(sprite);
        }
    }

    // Creates a billboarded sprite whose on-screen height matches worldHeight; a missing
    // texture falls back to a flat quad tinted fallbackColor so the kind stays visible and
    // clickable.
    //
    // A layer composited over another sprite at the same position (ResourceNodeView's fruit
    // overlay, PersonView's clothing and hair) has no defined draw order under OpaquePrepass;
    // it needs alphaCut Disabled plus a higher renderPriority to draw on top.
    //
    // FixedY, not Enabled: full billboarding aligns local up with the camera's up, so at this
    // game's pitch a sprite renders shorter than its nominal height and its base floats above
    // the ground. FixedY pins local up to world up. SpritePixelHit's plane math assumes it.
    public static Sprite3D Create(
        string texturePath,
        float worldHeight,
        Color fallbackColor,
        SpriteBase3D.AlphaCutMode alphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
        int renderPriority = 0,
        bool excludeFromOcclusionFade = false,
        bool useMipmaps = true)
    {
        var sprite = new Sprite3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            // Mipmaps: the art is fine engraving hatching, which aliases into shimmer when
            // minified without a mip chain. useMipmaps=false is for kinds where the blur costs
            // more than it saves - CloudScatter's few sparse clouds lose their hatching to one
            // flat tone at even a moderate mip level.
            TextureFilter = useMipmaps ? BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps : BaseMaterial3D.TextureFilterEnum.Linear,
            // OpaquePrepass keeps Discard's depth sorting between overlapping billboards but
            // still blends the art's soft anti-aliased edges instead of snapping them.
            AlphaCut = alphaCut,
            RenderPriority = renderPriority,
            Shaded = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        Apply(sprite, texturePath, worldHeight, fallbackColor);

        _liveSprites.Add(sprite);
        if (excludeFromOcclusionFade)
        {
            _excludedFromOcclusionFade.Add(sprite);
        }

        sprite.TreeExited += () =>
        {
            _liveSprites.Remove(sprite);
            _excludedFromOcclusionFade.Remove(sprite);
            _occlusionFaded.Remove(sprite);
        };

        return sprite;
    }

    // Points an existing sprite at a different texture, keeping its world height stable.
    public static void Apply(Sprite3D sprite, string texturePath, float worldHeight, Color fallbackColor)
    {
        var texture = TextureCache.TryGet(texturePath);

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
