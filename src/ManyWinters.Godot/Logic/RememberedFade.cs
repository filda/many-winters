using Godot;

namespace ManyWinters.Godot.Logic;

// How a thing looks while the group can no longer see where it stands - fog of war's
// "remembered" tier, explored but outside anyone's current sight (see ExplorationState) - and
// how long it takes to get there and back.
//
// One of these per view (ResourceNodeView, PersonView, GraveView, BuildingView) rather than
// each of them keeping its own bool and tint: the four dim by the same numbers on the same
// curve, which is what stopped a grave staying bright while the trees around it went sepia.
// It only ever says what to *multiply* a layer's own base modulate by, never what the layer's
// colour is - every view has its own idea of that (a dead person's grey, a tree canopy's
// brightness jitter), and none of that is this type's business.
//
// The two durations are deliberately different. Losing sight of something is memory gradually
// taking over from seeing it, so it eases out over about a second; getting it back is an
// event - someone walked up and there it is - so it snaps back in a fraction of that. Equal
// durations read as the world lagging behind the group.
internal sealed class RememberedFade
{
    // Sepia-ish memory of the place rather than what's actually there, same spirit as
    // docs/ZemanConceptArt.png's own "Remembered" panel. A componentwise multiply that also
    // darkens slightly, not a flat grey, so the tone stays warm and aged rather than merely
    // faded. Close to but not the same as FogOfWarRenderer's own remembered tint: that one
    // paints bare ground, this one multiplies into already-inked art.
    public static readonly Color Tint = new(0.78f, 0.68f, 0.52f);

    public const float ToRememberedSeconds = 1.2f;
    public const float ToVisibleSeconds = 0.35f;

    private bool _isRemembered;
    private float _progress;

    public bool IsRemembered => _isRemembered;

    // 0 while in sight, 1 once fully faded into memory.
    public float Progress => _progress;

    // Whether the fade still has somewhere to go - what tells a view to keep processing
    // frames, and to stop once it arrives.
    public bool IsFading => _isRemembered ? _progress < 1f : _progress > 0f;

    // Straight to the end state, no fade: a view being created for a place the group has
    // already walked away from was never in sight to fade out of. Safe before the view is in
    // the scene tree, since it touches nothing but this.
    public void Snap(bool remembered)
    {
        _isRemembered = remembered;
        _progress = remembered ? 1f : 0f;
    }

    // Points the fade at its new end state, and answers whether that was a change at all -
    // the exploration state is re-checked every tick for every view, and all but a handful of
    // those calls say the same thing as last time.
    public bool Retarget(bool remembered)
    {
        if (remembered == _isRemembered)
        {
            return false;
        }

        _isRemembered = remembered;
        return true;
    }

    // Moves toward the current end state and answers whether there is still further to go.
    // Reversing mid-fade continues from wherever the progress got to, rather than from the
    // end it never reached: the group wandering along the edge of sight flips this back and
    // forth, and restarting from 1 each time would show up as a flicker.
    public bool Advance(float deltaSeconds)
    {
        var step = deltaSeconds / (_isRemembered ? ToRememberedSeconds : ToVisibleSeconds);
        _progress = _isRemembered
            ? Math.Min(_progress + step, 1f)
            : Math.Max(_progress - step, 0f);

        return IsFading;
    }

    // The colour a layer should show now, given what it looks like in full sight. Alpha comes
    // through untouched from the base, because that channel belongs to Main's occlusion fade
    // (see BillboardSprite.OcclusionFadedSprites) - whoever writes this back to a sprite has
    // to preserve the sprite's live alpha, not the base's.
    public Color Applied(Color baseModulate) => baseModulate * Colors.White.Lerp(Tint, _progress);
}
