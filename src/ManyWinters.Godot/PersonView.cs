using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class PersonView : Area3D
{
    public const float Radius = 0.3f;
    public const float Height = 1.8f;
    private const float MinScale = 0.92f;
    private const float MaxScale = 1.08f;
    private const float ShadowDiameter = 0.9f;

    // A cardboard-cutout-on-a-stick wobble while actually walking, rather than gliding
    // like a ghost: a vertical bob plus a side-to-side rock, both driven by the same phase
    // accumulator (rock at half the bob's frequency - one full lean cycle per two bounces,
    // roughly matching a two-footed gait) so they read as one coherent waddle, not two
    // independent wiggles.
    private const float WalkCyclesPerSecond = 10f;
    private const float BobAmplitude = 0.08f;
    private const float RockAmplitude = 0.12f;

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
    private Sprite3D _hoverOutline = null!;
    private Sprite3D _selectionMarker = null!;
    private Vector3 _targetPosition;
    private float _interpolationSpeed;
    private float _walkPhase;

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

        var groundShadow = GroundShadow.Create(ShadowDiameter);
        groundShadow.Position += new Vector3(0, (-Height / 2f) + GroundShadow.GroundOffset, 0);
        AddChild(groundShadow);

        // Keep the default full billboard (not FixedY): the camera sits at a fixed 45deg
        // elevation, and only full billboard keeps the sprite's own face-normal genuinely
        // aligned with the actual (elevated) camera direction. FixedY holds the sprite
        // perfectly upright instead, so a local Z "roll" ends up misaligned with that
        // oblique view and reads as a forward/backward tilt rather than side-to-side.
        _hoverOutline = SpriteOutline.Create(AliveTexturePath, Height, SpriteOutline.HoverColor);
        _hoverOutline.Visible = false;
        AddChild(_hoverOutline);

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
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    private void OnMouseEntered() => _hoverOutline.Visible = true;

    private void OnMouseExited() => _hoverOutline.Visible = false;

    // Only the simulation tick moves a person; this just plays that motion back smoothly
    // between ticks instead of snapping once per tick, so speed always matches how far the
    // simulation actually moved them over that tick - never guessed or hardcoded.
    public override void _Process(double delta)
    {
        Position = Position.MoveToward(_targetPosition, _interpolationSpeed * (float)delta);

        var isWalking = Position.DistanceTo(_targetPosition) > 0.001f;
        if (isWalking)
        {
            _walkPhase += (float)delta * WalkCyclesPerSecond;
            var bob = new Vector3(0, MathF.Sin(_walkPhase) * BobAmplitude, 0);
            var rock = new Vector3(0, 0, MathF.Sin(_walkPhase * 0.5f) * RockAmplitude);
            _sprite.Position = bob;
            _sprite.Rotation = rock;
            // Kept in lockstep with _sprite - a separate sibling sprite (see SpriteOutline)
            // otherwise just sits still while the real sprite bobs and rocks away from it.
            _hoverOutline.Position = bob;
            _hoverOutline.Rotation = rock;
        }
        else
        {
            _walkPhase = 0f;
            _sprite.Position = Vector3.Zero;
            _sprite.Rotation = Vector3.Zero;
            _hoverOutline.Position = Vector3.Zero;
            _hoverOutline.Rotation = Vector3.Zero;
        }
    }

    public void SetTargetPosition(Vector3 target, float overSeconds)
    {
        var distance = Position.DistanceTo(target);
        _targetPosition = target;
        _interpolationSpeed = overSeconds > 0f ? distance / overSeconds : float.MaxValue;
    }

    public void SetAlive(bool isAlive)
    {
        var texturePath = isAlive ? AliveTexturePath : DeadTexturePath;
        BillboardSprite.Apply(_sprite, texturePath, Height, isAlive ? AliveColor : DeadColor);
        // Dead uses a different (sideways) silhouette - re-point the outline at it too,
        // otherwise it keeps tracing the old standing shape.
        SpriteOutline.Apply(_hoverOutline, texturePath, Height);
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
