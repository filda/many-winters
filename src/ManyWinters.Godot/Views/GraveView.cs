using Godot;
using ManyWinters.Core.Continuity;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// Clickable - the inspector shows who lies here - but never lit up: a highlight means "there
// is an order to give here", and there is none. Passing no hover arbiter says so and keeps a
// grave transparent to HoverRescue, so the cursor still finds whatever stands behind it.
internal partial class GraveView : SpriteEntityView
{
    public const float Size = 0.8f;
    private const float ShadowDiameter = 0.9f;

    private const string MarkedTexturePath = "res://Content/graves/grave_marked.png";
    private const string UnmarkedTexturePath = "res://Content/graves/grave_unmarked.png";

    private static readonly Color MarkedColor = new(0.7f, 0.7f, 0.75f);
    private static readonly Color UnmarkedColor = new(0.4f, 0.3f, 0.2f);

    private readonly Grave _grave;
    private readonly Action<Grave> _onSelected;

    public GraveView(Grave grave, Action<Grave> onSelected, InputEventEventHandler onMissedClick)
        : base(Size, hover: null, onMissedClick)
    {
        _grave = grave;
        _onSelected = onSelected;
    }

    protected override void Build()
    {
        SetUpGroundShadow(ShadowDiameter);

        var texturePath = _grave.IsMarked ? MarkedTexturePath : UnmarkedTexturePath;
        var fallbackColor = _grave.IsMarked ? MarkedColor : UnmarkedColor;
        Register(BillboardSprite.Create(texturePath, Size, fallbackColor), texturePath);
    }

    protected override bool OnClicked(MouseButton button)
    {
        _onSelected(_grave);
        return true;
    }
}
