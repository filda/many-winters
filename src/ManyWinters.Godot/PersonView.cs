using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class PersonView : Area3D
{
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

    private static readonly Color AliveColor = new(0.9f, 0.7f, 0.5f);
    private static readonly Color DeadColor = new(0.3f, 0.3f, 0.3f);

    private readonly PersonId _personId;
    private readonly Action<PersonId, MouseButton> _onClicked;
    private Sprite3D _sprite = null!;
    private Color _normalModulate;
    private CollisionShape3D _collisionShape = null!;
    private Vector3 _targetPosition;
    private float _interpolationSpeed;
    private float _walkPhase;
    private bool _isHovered;
    private string _currentTexturePath = AliveTexturePath;

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
        _sprite = BillboardSprite.Create(AliveTexturePath, Height, AliveColor);
        _normalModulate = _sprite.Modulate;
        AddChild(_sprite);

        _collisionShape = new CollisionShape3D();
        AddChild(_collisionShape);
        ApplyExtent(AliveTexturePath);

        InputEvent += OnInputEvent;
        // The broad-phase collision shape can only ever be a bounding box around the actual
        // silhouette (see SpriteVisibleExtent) - MouseExited still means "no longer even
        // close", but entering hover for real is decided pixel-by-pixel in OnInputEvent, not
        // here.
        MouseExited += OnMouseExited;
    }

    private void SetHovered(bool hovered)
    {
        if (hovered == _isHovered)
        {
            return;
        }

        _isHovered = hovered;
        _sprite.Modulate = hovered ? HoverHighlight.TintFor(_normalModulate) : _normalModulate;
        _sprite.Scale = Vector3.One * (hovered ? HoverHighlight.ScaleFactor : 1f);
    }

    private void OnMouseExited() => SetHovered(false);

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
            var rockAngle = MathF.Sin(_walkPhase * 0.5f) * RockAmplitude;
            _sprite.Position = bob;
            _sprite.Rotation = new Vector3(0, 0, rockAngle);
        }
        else
        {
            _walkPhase = 0f;
            _sprite.Position = Vector3.Zero;
            _sprite.Rotation = Vector3.Zero;
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
        _normalModulate = _sprite.Modulate;
        _currentTexturePath = texturePath;
        // Dead uses a different (sideways, wider/shorter) silhouette - the collision shape
        // and the marker's resting height both need to follow it, not stay sized/positioned
        // for a standing figure.
        ApplyExtent(texturePath);
    }

    // How high above this person's own origin their actual head sits - for Main's
    // screen-space selection marker overlay (see SpriteVisibleExtent's doc comment: content
    // isn't necessarily centered in its canvas, so the nominal Height/2 alone would float
    // above or sink below a real head depending on the texture's own margins).
    public float HeadHeightOffset
    {
        get
        {
            var extent = SpriteVisibleExtent.Compute(_currentTexturePath, Height);
            return extent.CenterYOffset + (extent.Height / 2f);
        }
    }

    // Sized/positioned to the sprite's actual drawn silhouette, not its full square canvas -
    // a standing figure doesn't fill its canvas edge to edge, so a collision shape based on
    // the nominal Height would be oversized (hovering near-but-not-on the figure would still
    // trigger it).
    private void ApplyExtent(string texturePath)
    {
        var extent = SpriteVisibleExtent.Compute(texturePath, Height);
        _collisionShape.Shape = new BoxShape3D { Size = new Vector3(extent.Width, extent.Height, extent.Width) };
        _collisionShape.Position = new Vector3(extent.CenterXOffset, extent.CenterYOffset, 0);
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (camera is not Camera3D camera3D)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseMotion:
                SetHovered(SpritePixelHit.IsOpaqueAt(camera3D, position, _sprite, _currentTexturePath));
                break;
            case InputEventMouseButton { Pressed: true } mouseEvent
                when SpritePixelHit.IsOpaqueAt(camera3D, position, _sprite, _currentTexturePath):
                _onClicked(_personId, mouseEvent.ButtonIndex);
                break;
        }
    }
}
