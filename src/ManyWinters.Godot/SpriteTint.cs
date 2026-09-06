using Godot;

namespace ManyWinters.Godot;

// Recolouring a sprite whose art is drawn in a neutral base tone rather than in the colour it
// should end up. Sprite3D.Modulate multiplies, so asking for a colour directly would come out
// darkened by whatever the art already carries - the base has to be divided back out first.
internal static class SpriteTint
{
    // What the recolourable parts of a person sprite (clothing, hair) are actually drawn in -
    // near-white, so dividing by it barely amplifies noise while still leaving the art some
    // shading of its own.
    private static readonly Color NeutralRecolourableBase = new(0.82f, 0.80f, 0.78f);

    // The multiplier that turns the base tone into `desired`. Feeding the base itself in gives
    // white - no tint at all - which is the property that makes this the right correction
    // rather than an arbitrary scale.
    internal static Color ModulateFor(Color desired) => new(
        desired.R / NeutralRecolourableBase.R,
        desired.G / NeutralRecolourableBase.G,
        desired.B / NeutralRecolourableBase.B);
}
