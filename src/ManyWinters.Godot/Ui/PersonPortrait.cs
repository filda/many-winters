using Godot;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Ui;

// Head and shoulders of the person the page is about, cut from the very layers their figure in
// the world is drawn with, so the face on the page is the one the player clicked on. The dead are
// shown standing, as they were, drained of colour the way their body on the ground is.
internal partial class PersonPortrait : Control
{
    // The head and the shoulders under it, out of the 256-pixel square every person layer is
    // drawn on; below this is the rest of the body, which the page has no need of.
    private static readonly Rect2 HeadAndShoulders = new(64, 14, 128, 128);

    private readonly TextureRect _body;
    private readonly TextureRect _clothing;
    private readonly TextureRect _hair;
    private (PersonLook Look, bool IsAlive)? _shown;

    internal PersonPortrait(float size)
    {
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;

        // Garment over body, hair over both - the order the world sprite stacks them in.
        _body = Layer();
        _clothing = Layer();
        _hair = Layer();
    }

    // Redrawn on every tick the page is left open, so it only retextures when it is somebody
    // else, or the same person has died.
    internal void Show(PersonLook look, bool isAlive)
    {
        if (_shown == (look, isAlive))
        {
            return;
        }

        _shown = (look, isAlive);
        Paint(_body, look.Body, isAlive ? Colors.White : PersonLook.DeadTint);
        Paint(_clothing, look.Clothing, isAlive ? SpriteTint.ModulateFor(look.ClothingColor) : PersonLook.DeadTint);
        Paint(_hair, look.Hair, isAlive ? SpriteTint.ModulateFor(look.HairColor) : PersonLook.DeadTint);
    }

    private TextureRect Layer()
    {
        var layer = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(layer);
        return layer;
    }

    private static void Paint(TextureRect layer, string texturePath, Color modulate)
    {
        layer.Texture = new AtlasTexture { Atlas = TextureCache.Get(texturePath), Region = HeadAndShoulders };
        layer.Modulate = modulate;
    }
}
