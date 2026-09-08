using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Views;

// The one place a sprite layer's colour is actually written, shared by every view that has a
// fog fade to show (ResourceNodeView, PersonView, GraveView, BuildingView). What goes into it
// is layered: the layer's own base colour in full sight, dimmed by however far the group's
// memory has taken over (RememberedFade), brightened while the cursor is on it
// (HoverHighlight). Each view knows its own base colour and its own hover state; none of them
// should be composing the order of the other two by hand.
internal static class SpriteLayerTint
{
    public static void Apply(Sprite3D sprite, Color baseModulate, RememberedFade fade, bool hovered)
    {
        var color = fade.Applied(baseModulate);
        if (hovered)
        {
            color = HoverHighlight.TintFor(color);
        }

        // Alpha is left exactly as it is on the sprite right now, rather than taken from the
        // base colour: that channel belongs to Main's occlusion fade, which re-applies it
        // every frame (see BillboardSprite.OcclusionFadedSprites). A fade running on a
        // ghosted sprite would otherwise write full alpha back once a frame and blink it
        // solid for the second the fade lasts.
        color.A = sprite.Modulate.A;
        sprite.Modulate = color;
    }
}
