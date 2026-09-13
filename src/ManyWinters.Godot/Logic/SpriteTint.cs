using Godot;

namespace ManyWinters.Godot.Logic;

// Recolouring a sprite drawn in a neutral base tone. Sprite3D.Modulate multiplies, so asking
// for a colour directly comes out darkened by the base - it has to be divided back out first.
internal static class SpriteTint
{
    // What the recolourable parts of a person sprite (clothing, hair) are drawn in - near-white,
    // so dividing by it barely amplifies noise while leaving the art some shading of its own.
    private static readonly Color NeutralRecolourableBase = new(0.82f, 0.80f, 0.78f);

    // The multiplier that turns the base tone into `desired`. Feeding the base itself in gives
    // white, no tint at all - the property that makes this a correction, not an arbitrary scale.
    internal static Color ModulateFor(Color desired) => new(
        desired.R / NeutralRecolourableBase.R,
        desired.G / NeutralRecolourableBase.G,
        desired.B / NeutralRecolourableBase.B);
}
