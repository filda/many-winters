using Godot;
using ManyWinters.Core.Continuity;

namespace ManyWinters.Godot;

public partial class GraveView : Area3D
{
    public const float Size = 0.8f;
    private const float ShadowDiameter = 0.9f;

    private const string MarkedTexturePath = "res://Content/graves/grave_marked.png";
    private const string UnmarkedTexturePath = "res://Content/graves/grave_unmarked.png";

    private static readonly Color MarkedColor = new(0.7f, 0.7f, 0.75f);
    private static readonly Color UnmarkedColor = new(0.4f, 0.3f, 0.2f);

    private readonly GraveId _graveId;
    private readonly bool _isMarked;
    private readonly Action<GraveId> _onSelected;
    private readonly CollisionObject3D.InputEventEventHandler _onMissedClick;
    private Sprite3D _sprite = null!;
    private string _texturePath = null!;

    public GraveView(GraveId graveId, bool isMarked, Action<GraveId> onSelected, CollisionObject3D.InputEventEventHandler onMissedClick)
    {
        _graveId = graveId;
        _isMarked = isMarked;
        _onSelected = onSelected;
        _onMissedClick = onMissedClick;
    }

    public override void _Ready()
    {
        InputRayPickable = true;

        _texturePath = _isMarked ? MarkedTexturePath : UnmarkedTexturePath;
        var fallbackColor = _isMarked ? MarkedColor : UnmarkedColor;

        var groundShadow = GroundShadow.Create(ShadowDiameter);
        groundShadow.Position += new Vector3(0, (-Size / 2f) + GroundShadow.GroundOffset, 0);
        AddChild(groundShadow);

        _sprite = BillboardSprite.Create(_texturePath, Size, fallbackColor);
        AddChild(_sprite);

        // Sized (and centered) to the actual drawn mound/stone, not the full square canvas.
        var extent = SpriteVisibleExtent.Compute(_texturePath, Size);
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(extent.Width, extent.Height, extent.Width) },
            Position = new Vector3(extent.CenterXOffset, extent.CenterYOffset, 0),
        });

        InputEvent += OnInputEvent;
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (camera is not Camera3D camera3D || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            return;
        }

        // The broad-phase collision box (see SpriteVisibleExtent) is bigger than the actual
        // silhouette - Godot only delivers a click to the nearest pickable collider along the
        // ray, so a click landing inside the box but off the opaque pixels (e.g. on this
        // grave's own ground shadow) would otherwise be silently swallowed here instead of
        // reaching the ground underneath. Forward it to whatever a plain ground click at this
        // same spot would have done.
        if (!TryClickAt(camera3D, position) && !HoverRescue.TryClickElsewhere(this, camera3D, position, MouseButton.Left))
        {
            _onMissedClick(camera, @event, position, normal, shapeIdx);
        }
    }

    public bool TryClickAt(Camera3D camera, Vector3 worldPosition)
    {
        if (!SpritePixelHit.IsOpaqueAt(camera, worldPosition, _sprite, _texturePath))
        {
            return false;
        }

        _onSelected(_graveId);
        return true;
    }
}
