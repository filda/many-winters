using Godot;

namespace ManyWinters.Godot;

// Hover feedback via a size bump (+ a modest tint) on the sprite itself, not a second
// scaled-up "rim" sprite behind it (see git history on the now-removed SpriteOutline) - a
// duplicate, coincident billboard turned out to depend on depth-test/billboard-orientation
// behavior that misbehaved in ways two separate fix attempts couldn't pin down.
//
// Tint alone (tried first) wasn't reliably visible: Modulate is a pure multiply, and this
// art style is heavily black crosshatch ink - multiplying an already-near-black pixel by
// any reasonable tint leaves it near-black (can't brighten what's already near zero by
// scaling it), so a large fraction of every sprite's area barely changed regardless of
// which color was picked. A size change has no such blind spot.
public static class HoverHighlight
{
    public const float ScaleFactor = 1.1f;
    private static readonly Color TintColor = new(1f, 0.85f, 0.15f);
    private const float TintBlend = 0.8f;

    public static Color TintFor(Color normalModulate) => normalModulate.Lerp(TintColor, TintBlend);
}
