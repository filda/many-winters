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
    // independent wiggles. Each person draws their own rate/amplitudes once (see _Ready) from
    // within these ranges - a shared exact rate is what made everyone's gait read as
    // synchronized even after IdleTask's paths stopped lining up.
    private const float MinWalkCyclesPerSecond = 8f;
    private const float MaxWalkCyclesPerSecond = 12f;
    private const float MinBobAmplitude = 0.06f;
    private const float MaxBobAmplitude = 0.10f;
    private const float MinRockAmplitude = 0.08f;
    private const float MaxRockAmplitude = 0.16f;

    private const string BodyMaleTexturePath = "res://Content/people/person_body_male.png";
    private const string BodyFemaleTexturePath = "res://Content/people/person_body_female.png";
    private const string BodyMaleDeadTexturePath = "res://Content/people/person_body_male_dead.png";
    private const string BodyFemaleDeadTexturePath = "res://Content/people/person_body_female_dead.png";

    // Layered on top of the body, paper-doll style (same renderPriority-ordered compositing
    // as ResourceNodeView's fruit overlay) - clothing first, hair on top of that. Each is an
    // independent seeded pick (see the constructor) from these small libraries, recoloured
    // at runtime rather than baked per-variant, so the combinatorics (body x clothing x hair
    // x colours) come from a handful of source images. Each "_dead" entry below is that same
    // layer rotated onto its side and re-seated at ground level (generate_sprites.py's
    // _lay_down) - index-matched to its standing counterpart so SetAlive can swap to the
    // *same* hairstyle/clothing lying down, not a generic one.
    private static readonly string[] HairTexturePaths =
    [
        "res://Content/people/hair_short.png",
        "res://Content/people/hair_long.png",
        "res://Content/people/hair_tied.png",
    ];

    private static readonly string[] HairDeadTexturePaths =
    [
        "res://Content/people/hair_short_dead.png",
        "res://Content/people/hair_long_dead.png",
        "res://Content/people/hair_tied_dead.png",
    ];

    private static readonly string[] ClothingTexturePaths =
    [
        "res://Content/people/clothing_robe.png",
        "res://Content/people/clothing_tunic.png",
        "res://Content/people/clothing_cloak.png",
    ];

    private static readonly string[] ClothingDeadTexturePaths =
    [
        "res://Content/people/clothing_robe_dead.png",
        "res://Content/people/clothing_tunic_dead.png",
        "res://Content/people/clothing_cloak_dead.png",
    ];

    private static readonly Color[] HairColorOptions =
    [
        new(0.22f, 0.16f, 0.11f),
        new(0.32f, 0.22f, 0.14f),
        new(0.45f, 0.40f, 0.34f),
    ];

    private static readonly Color[] ClothingColorOptions =
    [
        new(0.34f, 0.24f, 0.16f),
        new(0.33f, 0.36f, 0.42f),
        new(0.47f, 0.27f, 0.15f),
        new(0.40f, 0.36f, 0.20f),
    ];

    private static readonly Color AliveColor = new(0.9f, 0.7f, 0.5f);

    // Dead keeps this same person's own body/clothing/hair (still who they were), just
    // drained of colour, rather than swapping to one generic corpse everyone shares.
    // Modulate can only multiply, not truly desaturate, a multi-toned texture like the
    // body layer's skin+boots+accents - a flat muted-grey multiply doesn't reduce every
    // pixel to literal grey, but it darkens and mutes them enough to read as "the life gone
    // out of it" without needing a custom desaturation shader for what's otherwise a small
    // polish detail.
    private static readonly Color DeadTint = new(0.5f, 0.5f, 0.52f);

    // generate_sprites.py's NEUTRAL_RECOLOURABLE - hair/clothing art is drawn in this light
    // grey, not pure white, so a colour assigned straight to Modulate (a simple per-channel
    // multiply) would render darker/muddier than intended. Dividing the desired colour by
    // this base first compensates, so the final on-screen colour actually lands on what was
    // asked for.
    private static readonly Color NeutralRecolourableBase = new(0.82f, 0.80f, 0.78f);

    private readonly PersonId _personId;
    private readonly Action<PersonId, MouseButton> _onClicked;
    private readonly CollisionObject3D.InputEventEventHandler _onMissedClick;
    private readonly string _aliveTexturePath;
    private readonly string _deadTexturePath;
    private string _clothingAliveTexturePath = null!;
    private string _clothingDeadTexturePath = null!;
    private string _hairAliveTexturePath = null!;
    private string _hairDeadTexturePath = null!;
    private Sprite3D _sprite = null!;
    private Sprite3D _clothingSprite = null!;
    private Sprite3D _hairSprite = null!;
    private Color _clothingColor;
    private Color _hairColor;
    private Color _normalModulate;
    private Color _normalClothingModulate;
    private Color _normalHairModulate;
    private CollisionShape3D _collisionShape = null!;
    private Vector3 _targetPosition;
    private float _interpolationSpeed;
    private float _walkPhase;
    private float _walkCyclesPerSecond;
    private float _bobAmplitude;
    private float _rockAmplitude;
    private bool _isHovered;
    private bool _isAlive = true;
    private string _currentBodyTexturePath = null!;

    public PersonView(PersonId personId, Action<PersonId, MouseButton> onClicked, CollisionObject3D.InputEventEventHandler onMissedClick)
    {
        _personId = personId;
        _onClicked = onClicked;
        _onMissedClick = onMissedClick;
        // Body gender is its own independent seeded pick (distinct salt, see _Ready for the
        // rest) - deliberately not derived from the same draw as hairstyle/clothing below,
        // so gender doesn't end up correlated with them.
        var isMale = EntityVisualVariation.IndexFor(_personId.Value, salt: 4, 2) == 0;
        _aliveTexturePath = isMale ? BodyMaleTexturePath : BodyFemaleTexturePath;
        _deadTexturePath = isMale ? BodyMaleDeadTexturePath : BodyFemaleDeadTexturePath;
    }

    public override void _Ready()
    {
        InputRayPickable = true;

        var scale = EntityVisualVariation.Scale(_personId.Value, MinScale, MaxScale);
        Scale = Vector3.One * scale;
        // WorldPresenter positioned this node's own origin at groundHeight + Height/2,
        // assuming Scale stayed 1 - the sprite (centered, spanning local Y from -Height/2
        // to +Height/2) then has its bottom edge land exactly on the ground. Scale.Y above
        // multiplies that -Height/2 by scale before it's added to Position, so anyone
        // shorter than scale=1 floats with a small gap under their feet and anyone taller
        // sinks in - narrow enough a range here (0.92-1.08) to go unnoticed, unlike the
        // same bug at ResourceNodeView's much wider tree range. Shifting this node's own
        // Position by the same amount the scale just displaced the ground-contact point
        // cancels it back out, regardless of which way it went.
        Position += new Vector3(0f, (Height / 2f) * (scale - 1f), 0f);
        _walkCyclesPerSecond = EntityVisualVariation.RangeFor(_personId.Value, salt: 1, MinWalkCyclesPerSecond, MaxWalkCyclesPerSecond);
        _bobAmplitude = EntityVisualVariation.RangeFor(_personId.Value, salt: 2, MinBobAmplitude, MaxBobAmplitude);
        _rockAmplitude = EntityVisualVariation.RangeFor(_personId.Value, salt: 3, MinRockAmplitude, MaxRockAmplitude);
        _targetPosition = Position;

        var groundShadow = GroundShadow.Create(ShadowDiameter);
        groundShadow.Position += new Vector3(0, (-Height / 2f) + GroundShadow.GroundOffset, 0);
        AddChild(groundShadow);

        // BillboardSprite.Create always uses FixedY now (switched from full/spherical so a
        // standing figure's own feet actually land at ground level at this camera's oblique
        // tilt - see its own doc comment). That trade-off cuts both ways here specifically:
        // the walk-cycle's local Z "roll" below (_walkPhase) was tuned assuming full billboard,
        // where a Z roll reads as a proper side-to-side lean; under FixedY it may instead read
        // as a forward/backward tilt. Needs a live look once the ground-contact fix is
        // confirmed - if the walk rock looks wrong now, that's the reason.
        _sprite = BillboardSprite.Create(_aliveTexturePath, Height, AliveColor);
        _normalModulate = _sprite.Modulate;
        AddChild(_sprite);

        // Disabled, not the default OpaquePrepass - same reason as ResourceNodeView's fruit
        // overlay: an overlay sharing the body's exact position/depth needs ordinary alpha
        // blending to composite on top cleanly, OpaquePrepass has no defined draw order
        // between two billboards at the same depth.
        var clothingIndex = EntityVisualVariation.IndexFor(_personId.Value, salt: 5, ClothingTexturePaths.Length);
        _clothingAliveTexturePath = ClothingTexturePaths[clothingIndex];
        _clothingDeadTexturePath = ClothingDeadTexturePaths[clothingIndex];
        _clothingColor = ClothingColorOptions[EntityVisualVariation.IndexFor(_personId.Value, salt: 6, ClothingColorOptions.Length)];
        _clothingSprite = BillboardSprite.Create(_clothingAliveTexturePath, Height, _clothingColor, SpriteBase3D.AlphaCutMode.Disabled, renderPriority: 1);
        _clothingSprite.Modulate = ModulateFor(_clothingColor);
        _normalClothingModulate = _clothingSprite.Modulate;
        AddChild(_clothingSprite);

        var hairIndex = EntityVisualVariation.IndexFor(_personId.Value, salt: 7, HairTexturePaths.Length);
        _hairAliveTexturePath = HairTexturePaths[hairIndex];
        _hairDeadTexturePath = HairDeadTexturePaths[hairIndex];
        _hairColor = HairColorOptions[EntityVisualVariation.IndexFor(_personId.Value, salt: 8, HairColorOptions.Length)];
        _hairSprite = BillboardSprite.Create(_hairAliveTexturePath, Height, _hairColor, SpriteBase3D.AlphaCutMode.Disabled, renderPriority: 2);
        _hairSprite.Modulate = ModulateFor(_hairColor);
        _normalHairModulate = _hairSprite.Modulate;
        AddChild(_hairSprite);

        _collisionShape = new CollisionShape3D();
        AddChild(_collisionShape);
        _currentBodyTexturePath = _aliveTexturePath;
        ApplyExtent(_currentBodyTexturePath);

        InputEvent += OnInputEvent;
        // The broad-phase collision shape can only ever be a bounding box around the actual
        // silhouette (see SpriteVisibleExtent) - MouseExited still means "no longer even
        // close", but entering hover for real is decided pixel-by-pixel in OnInputEvent, not
        // here.
        MouseExited += OnMouseExited;
    }

    private static Color ModulateFor(Color desired) => new(
        desired.R / NeutralRecolourableBase.R,
        desired.G / NeutralRecolourableBase.G,
        desired.B / NeutralRecolourableBase.B);

    private void SetHovered(bool hovered)
    {
        if (hovered == _isHovered)
        {
            return;
        }

        _isHovered = hovered;
        var scale = Vector3.One * (hovered ? HoverHighlight.ScaleFactor : 1f);
        _sprite.Modulate = hovered ? HoverHighlight.TintFor(_normalModulate) : _normalModulate;
        _sprite.Scale = scale;
        _clothingSprite.Modulate = hovered ? HoverHighlight.TintFor(_normalClothingModulate) : _normalClothingModulate;
        _clothingSprite.Scale = scale;
        _hairSprite.Modulate = hovered ? HoverHighlight.TintFor(_normalHairModulate) : _normalHairModulate;
        _hairSprite.Scale = scale;
    }

    private void OnMouseExited() => SetHovered(false);

    // Lets HoverRescue ask "is this exact point actually opaque on you", for when some other
    // entity's broad-phase box won the pick instead - see its own doc comment for why that's
    // not just a hypothetical.
    public bool TryHoverAt(Camera3D camera, Vector3 worldPosition)
    {
        var opaque = SpritePixelHit.IsOpaqueAt(camera, worldPosition, _sprite, _currentBodyTexturePath, GlobalPosition);
        SetHovered(opaque);
        return opaque;
    }

    // Only the simulation tick moves a person; this just plays that motion back smoothly
    // between ticks instead of snapping once per tick, so speed always matches how far the
    // simulation actually moved them over that tick - never guessed or hardcoded.
    public override void _Process(double delta)
    {
        Position = Position.MoveToward(_targetPosition, _interpolationSpeed * (float)delta);

        var isWalking = Position.DistanceTo(_targetPosition) > 0.001f;
        if (isWalking)
        {
            _walkPhase += (float)delta * _walkCyclesPerSecond;
            var bob = new Vector3(0, MathF.Sin(_walkPhase) * _bobAmplitude, 0);
            var rotation = new Vector3(0, 0, MathF.Sin(_walkPhase * 0.5f) * _rockAmplitude);
            // All three layers move as one rigid cutout, not independently.
            _sprite.Position = bob;
            _sprite.Rotation = rotation;
            _clothingSprite.Position = bob;
            _clothingSprite.Rotation = rotation;
            _hairSprite.Position = bob;
            _hairSprite.Rotation = rotation;
        }

        // Deliberately no "not walking" branch that snaps _walkPhase/_sprite back to
        // neutral: everyone shares the same tick cadence (Main's single _tickAccumulator),
        // so the interpolation from the previous target finishing a frame or two early -
        // right at that shared tick boundary - hit every walking person at once. Snapping to
        // a neutral pose and rewinding the phase to 0 there read as a synchronized hiccup
        // across the whole crowd. Holding the last pose instead means those stray frames are
        // invisible, and phases drift apart naturally instead of all rewinding together.
    }

    public void SetTargetPosition(Vector3 target, float overSeconds)
    {
        var distance = Position.DistanceTo(target);
        _targetPosition = target;
        _interpolationSpeed = overSeconds > 0f ? distance / overSeconds : float.MaxValue;
    }

    public void SetAlive(bool isAlive)
    {
        // Main calls this every tick for every person regardless of whether IsAlive actually
        // changed - without this guard, re-deriving the tint every tick would silently
        // overwrite the hover tint once a second on every living person, independent of
        // _isHovered (which never got a chance to notice, since the very next real hover
        // re-check finds the same, still-true bool and no-ops).
        if (isAlive == _isAlive)
        {
            return;
        }

        _isAlive = isAlive;

        // Each layer swaps to its own matching rotated-onto-its-side variant (see
        // generate_sprites.py's _lay_down) - the same hairstyle/clothing this person had
        // standing, not a generic corpse. BillboardSprite.Apply resets Modulate to white,
        // which is why the actual tint is assigned afterward, not before.
        BillboardSprite.Apply(_sprite, isAlive ? _aliveTexturePath : _deadTexturePath, Height, AliveColor);
        BillboardSprite.Apply(_clothingSprite, isAlive ? _clothingAliveTexturePath : _clothingDeadTexturePath, Height, _clothingColor);
        BillboardSprite.Apply(_hairSprite, isAlive ? _hairAliveTexturePath : _hairDeadTexturePath, Height, _hairColor);

        _normalModulate = isAlive ? Colors.White : DeadTint;
        _normalClothingModulate = isAlive ? ModulateFor(_clothingColor) : DeadTint;
        _normalHairModulate = isAlive ? ModulateFor(_hairColor) : DeadTint;

        // Re-derives from the (now updated) normal colours rather than skipping this while
        // hovered - otherwise dying while already hovered would leave the old alive-hover
        // tint showing until the next real hover state change.
        _sprite.Modulate = _isHovered ? HoverHighlight.TintFor(_normalModulate) : _normalModulate;
        _clothingSprite.Modulate = _isHovered ? HoverHighlight.TintFor(_normalClothingModulate) : _normalClothingModulate;
        _hairSprite.Modulate = _isHovered ? HoverHighlight.TintFor(_normalHairModulate) : _normalHairModulate;

        // The rotated "lying down" texture already reads as flat on the ground - any
        // leftover walk bob/rock from mid-stride would tilt it off that, so clear it once
        // there's no more walking to re-derive it each frame (isWalking only ever updates
        // these while actually moving - see _Process).
        if (!isAlive)
        {
            _sprite.Position = Vector3.Zero;
            _sprite.Rotation = Vector3.Zero;
            _clothingSprite.Position = Vector3.Zero;
            _clothingSprite.Rotation = Vector3.Zero;
            _hairSprite.Position = Vector3.Zero;
            _hairSprite.Rotation = Vector3.Zero;
        }

        // Dead uses a differently-shaped (wider/shorter, lying down) silhouette - the
        // collision box and the marker's resting height both need to follow it.
        _currentBodyTexturePath = isAlive ? _aliveTexturePath : _deadTexturePath;
        ApplyExtent(_currentBodyTexturePath);
    }

    // How high above this person's own origin their actual head sits - for Main's
    // screen-space selection marker overlay (see SpriteVisibleExtent's doc comment: content
    // isn't necessarily centered in its canvas, so the nominal Height/2 alone would float
    // above or sink below a real head depending on the texture's own margins).
    public float HeadHeightOffset
    {
        get
        {
            var extent = SpriteVisibleExtent.Compute(_currentBodyTexturePath, Height);
            return extent.CenterYOffset + (extent.Height / 2f);
        }
    }

    // Sized/positioned to the sprite's actual drawn silhouette, not its full square canvas -
    // a standing figure doesn't fill its canvas edge to edge, so a collision shape based on
    // the nominal Height would be oversized (hovering near-but-not-on the figure would still
    // trigger it). Keyed off the body layer only, not clothing/hair - close enough for now,
    // see docs/todo/todo.md.
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

        // GlobalPosition (this node's own, not _sprite's) - see SpritePixelHit.IsOpaqueAt's
        // spriteCenterOverride doc comment: the walk bob moves _sprite's local Position every
        // frame, which must not feed into where the hit-test plane is anchored.
        switch (@event)
        {
            case InputEventMouseMotion:
                if (!TryHoverAt(camera3D, position))
                {
                    HoverRescue.TryHoverElsewhere(this, camera3D, position);
                }

                break;
            case InputEventMouseButton { Pressed: true } mouseEvent:
                // The broad-phase collision box (see ApplyExtent) is bigger than the actual
                // silhouette - Godot only delivers a click to the nearest pickable collider
                // along the ray, so a click landing inside the box but off the opaque pixels
                // (e.g. on the ground shadow at this person's feet) would otherwise be
                // silently swallowed here instead of reaching the ground underneath. Try
                // whatever else is actually at this point first (HoverRescue's click
                // counterpart), only falling all the way back to a plain ground-click order
                // if nothing there turns out to be real either.
                if (!TryClickAt(camera3D, position, mouseEvent.ButtonIndex)
                    && !HoverRescue.TryClickElsewhere(this, camera3D, position, mouseEvent.ButtonIndex))
                {
                    _onMissedClick(camera, @event, position, normal, shapeIdx);
                }

                break;
        }
    }

    public bool TryClickAt(Camera3D camera, Vector3 worldPosition, MouseButton button)
    {
        if (!SpritePixelHit.IsOpaqueAt(camera, worldPosition, _sprite, _currentBodyTexturePath, GlobalPosition))
        {
            return false;
        }

        _onClicked(_personId, button);
        return true;
    }
}
