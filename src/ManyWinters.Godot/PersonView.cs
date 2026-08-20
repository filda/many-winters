using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class PersonView : Area3D
{
    public const float Radius = 0.3f;
    public const float Height = 1.8f;
    private const float MinScale = 0.92f;
    private const float MaxScale = 1.08f;

    private const string AliveTexturePath = "res://Content/people/person.png";
    private const string DeadTexturePath = "res://Content/people/person_dead.png";
    private const string SelectionMarkerTexturePath = "res://Content/people/selection_marker.png";
    private const float SelectionMarkerHeight = 0.5f;
    private const float SelectionMarkerGap = 0.2f;

    private static readonly Color AliveColor = new(0.9f, 0.7f, 0.5f);
    private static readonly Color DeadColor = new(0.3f, 0.3f, 0.3f);
    private static readonly Color SelectionMarkerFallbackColor = new(0.95f, 0.78f, 0.20f);

    private readonly PersonId _personId;
    private readonly Action<PersonId, MouseButton> _onClicked;
    private Sprite3D _sprite = null!;
    private Sprite3D _selectionMarker = null!;
    private Vector3 _targetPosition;
    private float _interpolationSpeed;

    public PersonView(PersonId personId, Action<PersonId, MouseButton> onClicked)
    {
        _personId = personId;
        _onClicked = onClicked;
    }

    public override void _Ready()
    {
        InputRayPickable = true;

        Scale = Vector3.One * EntityVisualVariation.Scale(_personId.Value, MinScale, MaxScale);
        _targetPosition = Position;

        _sprite = BillboardSprite.Create(AliveTexturePath, Height, AliveColor);
        AddChild(_sprite);

        _selectionMarker = BillboardSprite.Create(SelectionMarkerTexturePath, SelectionMarkerHeight, SelectionMarkerFallbackColor);
        _selectionMarker.Position = new Vector3(0, (Height / 2f) + SelectionMarkerGap + (SelectionMarkerHeight / 2f), 0);
        _selectionMarker.Visible = false;
        AddChild(_selectionMarker);

        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = Radius, Height = Height },
        });

        InputEvent += OnInputEvent;
    }

    // Only the simulation tick moves a person; this just plays that motion back smoothly
    // between ticks instead of snapping once per tick, so speed always matches how far the
    // simulation actually moved them over that tick - never guessed or hardcoded.
    public override void _Process(double delta)
    {
        Position = Position.MoveToward(_targetPosition, _interpolationSpeed * (float)delta);
    }

    public void SetTargetPosition(Vector3 target, float overSeconds)
    {
        var distance = Position.DistanceTo(target);
        _targetPosition = target;
        _interpolationSpeed = overSeconds > 0f ? distance / overSeconds : float.MaxValue;
    }

    public void SetAlive(bool isAlive)
    {
        BillboardSprite.Apply(
            _sprite,
            isAlive ? AliveTexturePath : DeadTexturePath,
            Height,
            isAlive ? AliveColor : DeadColor);
    }

    public void SetSelected(bool selected)
    {
        _selectionMarker.Visible = selected;
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true } mouseEvent)
        {
            _onClicked(_personId, mouseEvent.ButtonIndex);
        }
    }
}
