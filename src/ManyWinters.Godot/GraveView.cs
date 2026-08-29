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
    private Sprite3D _sprite = null!;
    private string _texturePath = null!;

    public GraveView(GraveId graveId, bool isMarked, Action<GraveId> onSelected)
    {
        _graveId = graveId;
        _isMarked = isMarked;
        _onSelected = onSelected;
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
            Position = new Vector3(0, extent.CenterYOffset, 0),
        });

        InputEvent += OnInputEvent;
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (camera is Camera3D camera3D
            && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
            && SpritePixelHit.IsOpaqueAt(camera3D, position, _sprite, _texturePath))
        {
            _onSelected(_graveId);
        }
    }
}
