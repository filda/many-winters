using Godot;
using ManyWinters.Core.Continuity;

namespace ManyWinters.Godot;

public partial class GraveView : Area3D
{
    public const float Size = 0.8f;

    private const string MarkedTexturePath = "res://Content/graves/grave_marked.png";
    private const string UnmarkedTexturePath = "res://Content/graves/grave_unmarked.png";

    private static readonly Color MarkedColor = new(0.7f, 0.7f, 0.75f);
    private static readonly Color UnmarkedColor = new(0.4f, 0.3f, 0.2f);

    private readonly GraveId _graveId;
    private readonly bool _isMarked;
    private readonly Action<GraveId> _onSelected;

    public GraveView(GraveId graveId, bool isMarked, Action<GraveId> onSelected)
    {
        _graveId = graveId;
        _isMarked = isMarked;
        _onSelected = onSelected;
    }

    public override void _Ready()
    {
        InputRayPickable = true;

        var texturePath = _isMarked ? MarkedTexturePath : UnmarkedTexturePath;
        var fallbackColor = _isMarked ? MarkedColor : UnmarkedColor;

        AddChild(BillboardSprite.Create(texturePath, Size, fallbackColor));

        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(Size, Size, Size) },
        });

        InputEvent += OnInputEvent;
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            _onSelected(_graveId);
        }
    }
}
