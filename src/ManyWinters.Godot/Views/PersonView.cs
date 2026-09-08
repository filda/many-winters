using Godot;
using ManyWinters.Core.Population;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// A person, drawn paper-doll style: a body with a garment and a hairstyle layered on top,
// each an independent seeded pick, and each swapped for its lying-down counterpart on death.
// The walk cycle is the only animation any view has, which is why this is the one that keeps
// processing frames when nothing is fading.
internal partial class PersonView : SpriteEntityView
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

    private readonly Person _person;
    private readonly Action<Person, MouseButton> _onClicked;
    private readonly string _aliveTexturePath;
    private readonly string _deadTexturePath;
    private string _clothingAliveTexturePath = null!;
    private string _clothingDeadTexturePath = null!;
    private string _hairAliveTexturePath = null!;
    private string _hairDeadTexturePath = null!;
    private SpriteLayer _body = null!;
    private SpriteLayer _clothing = null!;
    private SpriteLayer _hair = null!;
    private Color _clothingColor;
    private Color _hairColor;

    // What the body layer looks like alive, kept because SetAlive has to be able to put it
    // back - it is normally plain white, but a missing texture leaves the fallback colour
    // here instead, and that is still the honest "in full sight, alive" colour for it.
    private Color _aliveBodyModulate;
    private Vector3 _targetPosition;
    private float _interpolationSpeed;
    private float _walkPhase;
    private float _walkCyclesPerSecond;
    private float _bobAmplitude;
    private float _rockAmplitude;
    private bool _isAlive = true;

    // Internal, like the HoverArbiter it takes: WorldPresenter is the only thing that ever
    // builds a view, and the hover invariant it hands over is the presentation layer's own
    // business (see AssemblyInfo).
    internal PersonView(Person person, HoverArbiter hover, Action<Person, MouseButton> onClicked, InputEventEventHandler onMissedClick)
        : base(Height, hover, onMissedClick)
    {
        _person = person;
        _onClicked = onClicked;
        // Body gender is its own independent seeded pick (distinct salt, see _Ready for the
        // rest) - deliberately not derived from the same draw as hairstyle/clothing below,
        // so gender doesn't end up correlated with them.
        var isMale = EntityVisualVariation.IndexFor(_person.Id.Seed, salt: 4, 2) == 0;
        _aliveTexturePath = isMale ? BodyMaleTexturePath : BodyFemaleTexturePath;
        _deadTexturePath = isMale ? BodyMaleDeadTexturePath : BodyFemaleDeadTexturePath;
    }

    protected override void Build()
    {
        // Narrow enough a range here (0.92-1.08) that the ground-contact correction goes
        // unnoticed either way, unlike at ResourceNodeView's much wider tree range - but the
        // correction is the same one, and it lives in the base class now.
        var scale = EntityVisualVariation.Scale(_person.Id.Seed, MinScale, MaxScale);
        ScaleAndKeepGroundContact(scale, scale);
        _walkCyclesPerSecond = EntityVisualVariation.RangeFor(_person.Id.Seed, salt: 1, MinWalkCyclesPerSecond, MaxWalkCyclesPerSecond);
        _bobAmplitude = EntityVisualVariation.RangeFor(_person.Id.Seed, salt: 2, MinBobAmplitude, MaxBobAmplitude);
        _rockAmplitude = EntityVisualVariation.RangeFor(_person.Id.Seed, salt: 3, MinRockAmplitude, MaxRockAmplitude);
        _targetPosition = Position;

        SetUpGroundShadow(ShadowDiameter);

        // BillboardSprite.Create always uses FixedY now (switched from full/spherical so a
        // standing figure's own feet actually land at ground level at this camera's oblique
        // tilt - see its own doc comment). That trade-off cuts both ways here specifically:
        // the walk-cycle's local Z "roll" below (_walkPhase) was tuned assuming full billboard,
        // where a Z roll reads as a proper side-to-side lean; under FixedY it may instead read
        // as a forward/backward tilt. Needs a live look once the ground-contact fix is
        // confirmed - if the walk rock looks wrong now, that's the reason.
        var body = BillboardSprite.Create(_aliveTexturePath, Height, AliveColor);
        _aliveBodyModulate = body.Modulate;
        _body = Register(body, _aliveTexturePath);

        // Disabled, not the default OpaquePrepass - same reason as ResourceNodeView's fruit
        // overlay: an overlay sharing the body's exact position/depth needs ordinary alpha
        // blending to composite on top cleanly, OpaquePrepass has no defined draw order
        // between two billboards at the same depth.
        var clothingIndex = EntityVisualVariation.IndexFor(_person.Id.Seed, salt: 5, ClothingTexturePaths.Length);
        _clothingAliveTexturePath = ClothingTexturePaths[clothingIndex];
        _clothingDeadTexturePath = ClothingDeadTexturePaths[clothingIndex];
        _clothingColor = ClothingColorOptions[EntityVisualVariation.IndexFor(_person.Id.Seed, salt: 6, ClothingColorOptions.Length)];
        var clothing = BillboardSprite.Create(_clothingAliveTexturePath, Height, _clothingColor, SpriteBase3D.AlphaCutMode.Disabled, renderPriority: 1);
        clothing.Modulate = SpriteTint.ModulateFor(_clothingColor);
        _clothing = Register(clothing, _clothingAliveTexturePath);

        var hairIndex = EntityVisualVariation.IndexFor(_person.Id.Seed, salt: 7, HairTexturePaths.Length);
        _hairAliveTexturePath = HairTexturePaths[hairIndex];
        _hairDeadTexturePath = HairDeadTexturePaths[hairIndex];
        _hairColor = HairColorOptions[EntityVisualVariation.IndexFor(_person.Id.Seed, salt: 8, HairColorOptions.Length)];
        var hair = BillboardSprite.Create(_hairAliveTexturePath, Height, _hairColor, SpriteBase3D.AlphaCutMode.Disabled, renderPriority: 2);
        hair.Modulate = SpriteTint.ModulateFor(_hairColor);
        _hair = Register(hair, _hairAliveTexturePath);
    }

    // The walk cycle runs whether or not anything is fading, so processing never switches off.
    protected override bool NeedsEveryFrame => true;

    // A person answers to either button - left selects them, right is an order aimed at them -
    // unlike everything else in the world, which only ever takes a left click.
    protected override bool WantsClick(MouseButton button) => true;

    // The walk bob moves the layers' local Position every frame, so the hit-test plane has to
    // be pinned to this node's own position instead: anchoring it to a sprite that is bobbing
    // sweeps the sampled pixel across silhouette edges under a cursor that never moved, and
    // reads as the hover flickering on and off.
    protected override Vector3? PixelHitAnchor => GlobalPosition;

    protected override bool OnClicked(MouseButton button)
    {
        _onClicked(_person, button);
        return true;
    }

    // Only the simulation tick moves a person; this just plays that motion back smoothly
    // between ticks instead of snapping once per tick, so speed always matches how far the
    // simulation actually moved them over that tick - never guessed or hardcoded.
    protected override void OnProcess(double delta)
    {
        Position = Position.MoveToward(_targetPosition, _interpolationSpeed * (float)delta);

        if (WalkCycle.IsWalking(Position, _targetPosition))
        {
            _walkPhase = WalkCycle.Advanced(_walkPhase, (float)delta, _walkCyclesPerSecond);
            var pose = WalkCycle.PoseAt(_walkPhase, _bobAmplitude, _rockAmplitude);

            // All three layers take the same pose, not their own.
            _body.Sprite.Position = pose.Offset;
            _body.Sprite.Rotation = pose.Rotation;
            _clothing.Sprite.Position = pose.Offset;
            _clothing.Sprite.Rotation = pose.Rotation;
            _hair.Sprite.Position = pose.Offset;
            _hair.Sprite.Rotation = pose.Rotation;
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
        _interpolationSpeed = WalkCycle.InterpolationSpeed(Position.DistanceTo(target), overSeconds);
        _targetPosition = target;
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
        // standing, not a generic corpse. Retexture carries each layer's new base colour with
        // it, because BillboardSprite.Apply underneath resets Modulate to white.
        Retexture(_body, isAlive ? _aliveTexturePath : _deadTexturePath, isAlive ? _aliveBodyModulate : DeadTint, AliveColor);
        Retexture(_clothing, isAlive ? _clothingAliveTexturePath : _clothingDeadTexturePath, isAlive ? SpriteTint.ModulateFor(_clothingColor) : DeadTint, _clothingColor);
        Retexture(_hair, isAlive ? _hairAliveTexturePath : _hairDeadTexturePath, isAlive ? SpriteTint.ModulateFor(_hairColor) : DeadTint, _hairColor);

        // Re-painted from the (now updated) base colours rather than skipped while hovered or
        // mid-fade - otherwise dying while already hovered left the old alive-hover tint
        // showing until the next real hover state change.
        ApplyTints();

        // The rotated "lying down" texture already reads as flat on the ground - any
        // leftover walk bob/rock from mid-stride would tilt it off that, so clear it once
        // there's no more walking to re-derive it each frame (isWalking only ever updates
        // these while actually moving - see _Process).
        if (!isAlive)
        {
            _body.Sprite.Position = Vector3.Zero;
            _body.Sprite.Rotation = Vector3.Zero;
            _clothing.Sprite.Position = Vector3.Zero;
            _clothing.Sprite.Rotation = Vector3.Zero;
            _hair.Sprite.Position = Vector3.Zero;
            _hair.Sprite.Rotation = Vector3.Zero;
        }

        // Dead uses a differently-shaped (wider/shorter, lying down) silhouette - the
        // collision box and the marker's resting height both read off the layers, so both
        // follow from re-measuring them.
        RefreshCollisionShape();
    }

}
