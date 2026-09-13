using Godot;

namespace ManyWinters.Godot.Logic;

// How a thing looks while the group can no longer see where it stands - fog of war's
// "remembered" tier, explored but out of sight (see ExplorationState) - and how long the
// transition takes each way. One instance per view (ResourceNodeView, PersonView, GraveView,
// BuildingView) so all four dim by the same numbers on the same curve. It only says what to
// multiply a layer's own base modulate by; the layer's colour stays the view's business.
//
// Losing sight is memory gradually taking over, so it eases out over about a second; regaining
// it is an event, so it snaps back in a fraction of that. Equal durations read as the world
// lagging behind the group.
internal sealed class RememberedFade
{
    // Sepia memory of the place: a componentwise multiply that also darkens slightly, so the
    // tone stays warm rather than merely faded. Deliberately not FogOfWarRenderer's
    // RememberedTint - that one paints bare ground, this one multiplies into inked art.
    public static readonly Color Tint = new(0.78f, 0.68f, 0.52f);

    public const float ToRememberedSeconds = 1.2f;
    public const float ToVisibleSeconds = 0.35f;

    private bool _isRemembered;
    private float _progress;

    public bool IsRemembered => _isRemembered;

    // 0 while in sight, 1 once fully faded into memory.
    public float Progress => _progress;

    // Whether the fade still has somewhere to go - a view keeps processing frames while true.
    public bool IsFading => _isRemembered ? _progress < 1f : _progress > 0f;

    // Straight to the end state: a view created for a place already walked away from was
    // never in sight to fade out of. Safe before the view enters the scene tree.
    public void Snap(bool remembered)
    {
        _isRemembered = remembered;
        _progress = remembered ? 1f : 0f;
    }

    // Points the fade at a new end state and reports whether that changed anything - it is
    // called every tick for every view, and almost every call repeats the last answer.
    public bool Retarget(bool remembered)
    {
        if (remembered == _isRemembered)
        {
            return false;
        }

        _isRemembered = remembered;
        return true;
    }

    // Moves toward the end state and reports whether there is further to go. Reversing
    // mid-fade continues from the current progress; restarting from the far end would flicker
    // as the group wanders along the edge of sight.
    public bool Advance(float deltaSeconds)
    {
        var step = deltaSeconds / (_isRemembered ? ToRememberedSeconds : ToVisibleSeconds);
        _progress = _isRemembered
            ? Math.Min(_progress + step, 1f)
            : Math.Max(_progress - step, 0f);

        return IsFading;
    }

    // The colour a layer shows now, given its full-sight colour. Alpha is left to Main's
    // occlusion fade (see BillboardSprite.OcclusionFadedSprites): whoever writes this back to a
    // sprite must keep the sprite's live alpha, not the base's.
    public Color Applied(Color baseModulate) => baseModulate * Colors.White.Lerp(Tint, _progress);
}
