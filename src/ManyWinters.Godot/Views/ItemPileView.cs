using Godot;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// A small heap of one kind of item, dropped on the ground (DropCommand). Same shape as
// ResourceNodeView's ground icon - one billboard, a shadow - but never a standing tree: a pile
// has no growth to draw. A kind with no dedicated icon yet falls back to a flat tint, the same
// way a resource without art does.
internal partial class ItemPileView : SpriteEntityView
{
    internal const float Size = 0.5f;
    private const float ShadowDiameterRatio = 0.6f / Size;
    private static readonly Color FallbackColor = new(0.55f, 0.45f, 0.3f);

    private readonly Entity _pile;
    private readonly Action<Entity, MouseButton> _onClicked;

    internal ItemPileView(Entity pile, HoverArbiter hover, Action<Entity, MouseButton> onClicked, InputEventEventHandler onMissedClick)
        : base(Size, hover, onMissedClick)
    {
        _pile = pile;
        _onClicked = onClicked;
    }

    protected override void Build()
    {
        SetUpGroundShadow(Size * ShadowDiameterRatio);

        var texturePath = TexturePathFor(_pile.Kind);
        Register(BillboardSprite.Create(texturePath, Size, FallbackColor), texturePath);
    }

    // A dedicated item icon (axe, warm_clothing) lives under Content/items; a gathered material
    // (wood, apple, pear...) never got one of its own - it already has a ground icon under
    // Content/resources, drawn for the resource it comes off, and a dropped pile of it is the
    // same icon lying on the ground rather than growing.
    private static string TexturePathFor(EntityKindId kind)
    {
        var itemsPath = TexturePaths.ForItem(kind.Value);
        return ResourceLoader.Exists(itemsPath) ? itemsPath : TexturePaths.ForResource(kind.Value);
    }

    // Both buttons answer, as a resource does: left picks it up, right asks what else could be
    // done with it (today, the same one thing).
    protected override bool WantsClick(MouseButton button) => true;

    protected override bool OnClicked(MouseButton button)
    {
        _onClicked(_pile, button);
        return true;
    }
}
