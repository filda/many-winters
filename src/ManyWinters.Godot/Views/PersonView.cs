using Godot;
using ManyWinters.Core.Population;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// A person, drawn paper-doll style: body, garment and hairstyle layered on top, each an
// independent seeded pick, each swapped for its lying-down counterpart on death. The walk cycle
// is the only per-frame animation any view has, so this one keeps processing when nothing fades.
internal partial class PersonView : SpriteEntityView
{
    public const float Height = 1.8f;
    private const float MinScale = 0.92f;
    private const float MaxScale = 1.08f;
    private const float ShadowDiameter = 0.9f;

    // A cardboard-cutout-on-a-stick bounce while walking (see WalkCycle). Each person draws
    // their own rate and amplitude from these ranges in Build; a shared exact rate reads as a
    // synchronized gait.
    private const float MinWalkCyclesPerSecond = 8f;
    private const float MaxWalkCyclesPerSecond = 12f;
    private const float MinBobAmplitude = 0.06f;
    private const float MaxBobAmplitude = 0.10f;

    // Standing still is not standing frozen (see IdleSway): the walk's own bob, a fifth slower
    // and about half as high, so rest and walk hand over without a change of rhythm. Each person
    // bobs at their own rate from their own phase, so a crowd at rest does not bounce in unison.
    private const float IdleCyclesPerWalkCycle = 0.8f;
    private const float MinIdleBobAmplitude = 0.03f;
    private const float MaxIdleBobAmplitude = 0.05f;

    // A person is "standing" only after being still this long. Between ticks the interpolated
    // step arrives a frame or two before the next target (everyone shares Main's tick
    // accumulator), and treating that gap as standing makes the whole crowd sway and snap back
    // every tick.
    private const float StandingAfterSeconds = 0.3f;

    // How quickly the idle bob fades in once standing and, faster, out once walking. The bob's
    // weight eases, not the bob itself - eased directly, an eight-hertz signal is mostly damped
    // away (see IdleSway).
    private const float IdleFadeInSeconds = 0.35f;
    private const float IdleFadeOutSeconds = 0.15f;

    // How quickly the last step's bob eases away once standing, so nobody stays frozen
    // mid-bounce.
    private const float StepSettleSeconds = 0.25f;

    private const string BodyMaleTexturePath = "res://Content/people/person_body_male.png";
    private const string BodyFemaleTexturePath = "res://Content/people/person_body_female.png";
    private const string BodyMaleDeadTexturePath = "res://Content/people/person_body_male_dead.png";
    private const string BodyFemaleDeadTexturePath = "res://Content/people/person_body_female_dead.png";

    // Layered on the body, paper-doll style: clothing first, hair on top, each an independent
    // seeded pick recoloured at runtime, so body x clothing x hair x colours come from a handful
    // of images. Each "_dead" entry is the same layer laid on its side (generate_sprites.py's
    // _lay_down), index-matched so SetAlive swaps to the *same* hairstyle/clothing lying down.
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

    // Dead keeps this person's own body/clothing/hair, drained of colour, rather than a shared
    // generic corpse. Modulate can only multiply, not desaturate, but a muted grey darkens the
    // multi-toned body enough to read as lifeless without a shader.
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

    // The body layer's alive colour, so SetAlive can put it back: normally white, but a missing
    // texture leaves the fallback colour here.
    private Color _aliveBodyModulate;
    private Vector3 _targetPosition;
    private float _interpolationSpeed;
    private float _walkPhase;
    private float _walkCyclesPerSecond;
    private float _bobAmplitude;
    private float _idlePhase;
    private float _idleBobAmplitude;
    private float _idleWeight;
    private float _standingSeconds;

    // The walk's bob, exact while walking and easing away once standing; ApplyPose adds the
    // idle bob, whose weight fades the other way, so the hand-over is never a jump.
    private Vector3 _stepOffset;
    private bool _isAlive = true;

    // Internal, like the HoverArbiter it takes: only WorldPresenter builds views, and the hover
    // invariant is the presentation layer's own business (see AssemblyInfo).
    internal PersonView(Person person, HoverArbiter hover, Action<Person, MouseButton> onClicked, InputEventEventHandler onMissedClick)
        : base(Height, hover, onMissedClick)
    {
        _person = person;
        _onClicked = onClicked;
        // Its own salt, distinct from the hairstyle/clothing picks in Build, so gender is not
        // correlated with them.
        var isMale = EntityVisualVariation.IndexFor(_person.Id.Seed, salt: 4, 2) == 0;
        _aliveTexturePath = isMale ? BodyMaleTexturePath : BodyFemaleTexturePath;
        _deadTexturePath = isMale ? BodyMaleDeadTexturePath : BodyFemaleDeadTexturePath;
    }

    protected override void Build()
    {
        // A narrow range, but the ground-contact correction applies all the same.
        var scale = EntityVisualVariation.Scale(_person.Id.Seed, MinScale, MaxScale);
        ScaleAndKeepGroundContact(scale, scale);
        _walkCyclesPerSecond = EntityVisualVariation.RangeFor(_person.Id.Seed, salt: 1, MinWalkCyclesPerSecond, MaxWalkCyclesPerSecond);
        _bobAmplitude = EntityVisualVariation.RangeFor(_person.Id.Seed, salt: 2, MinBobAmplitude, MaxBobAmplitude);
        _idleBobAmplitude = EntityVisualVariation.RangeFor(_person.Id.Seed, salt: 5, MinIdleBobAmplitude, MaxIdleBobAmplitude);
        _idlePhase = EntityVisualVariation.RangeFor(_person.Id.Seed, salt: 7, 0f, MathF.Tau);
        _targetPosition = Position;

        SetUpGroundShadow(ShadowDiameter);

        var body = BillboardSprite.Create(_aliveTexturePath, Height, AliveColor);
        _aliveBodyModulate = body.Modulate;
        _body = Register(body, _aliveTexturePath);

        // AlphaCutMode.Disabled, not the default OpaquePrepass: an overlay at the body's exact
        // position and depth needs ordinary alpha blending to composite cleanly, since
        // OpaquePrepass has no defined order between two billboards at one depth (as for
        // ResourceNodeView's fruit overlay).
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

    // The walk bob moves the layers every frame, so the hit-test plane is pinned to this node's
    // position; anchored to a bobbing sprite, the sampled pixel sweeps across silhouette edges
    // and the hover flickers.
    protected override Vector3? PixelHitAnchor => GlobalPosition;

    protected override bool OnClicked(MouseButton button)
    {
        _onClicked(_person, button);
        return true;
    }

    // Only the simulation tick moves a person; this plays that motion back smoothly between
    // ticks, at whatever speed matches how far the tick actually moved them.
    protected override void OnProcess(double delta)
    {
        Position = Position.MoveToward(_targetPosition, _interpolationSpeed * (float)delta);
        var seconds = (float)delta;

        if (!_isAlive)
        {
            return;
        }

        // The idle bob runs all the time and is only faded in and out, so a step beginning or
        // ending carries on in the same rhythm instead of restarting it.
        _idlePhase = WalkCycle.Advanced(_idlePhase, seconds, _walkCyclesPerSecond * IdleCyclesPerWalkCycle);

        if (WalkCycle.IsWalking(Position, _targetPosition))
        {
            _standingSeconds = 0f;
            _walkPhase = WalkCycle.Advanced(_walkPhase, seconds, _walkCyclesPerSecond);
            _stepOffset = WalkCycle.BobAt(_walkPhase, _bobAmplitude);
            _idleWeight = IdleSway.Settle(_idleWeight, 0f, seconds, IdleFadeOutSeconds);
            ApplyPose();
            return;
        }

        // The last step's pose is held, not snapped to neutral, through the tick-boundary gap
        // (see StandingAfterSeconds); snapping reads as a synchronized hiccup across the crowd.
        // The walk phase stays where it stopped for the same reason, so phases drift apart
        // instead of rewinding together.
        _standingSeconds += seconds;
        if (_standingSeconds < StandingAfterSeconds)
        {
            ApplyPose();
            return;
        }

        // Genuinely standing: the step's bounce eases away and the idle bob fades in.
        _stepOffset = IdleSway.Settle(_stepOffset, Vector3.Zero, seconds, StepSettleSeconds);
        _idleWeight = IdleSway.Settle(_idleWeight, 1f, seconds, IdleFadeInSeconds);
        ApplyPose();
    }

    // All three layers take the same offset, not their own - they are one rigid cutout.
    private void ApplyPose()
    {
        var offset = _stepOffset + WalkCycle.BobAt(_idlePhase, _idleBobAmplitude * _idleWeight);
        _body.Sprite.Position = offset;
        _clothing.Sprite.Position = offset;
        _hair.Sprite.Position = offset;
    }

    // `target` is where WorldSpace.ToRender puts an unscaled person; this view stands a little
    // higher than that (see SpriteEntityView.GroundContactCorrection), and so must its target.
    public void SetTargetPosition(Vector3 target, float overSeconds)
    {
        var corrected = target + GroundContactCorrection;
        _interpolationSpeed = WalkCycle.InterpolationSpeed(Position.DistanceTo(corrected), overSeconds);
        _targetPosition = corrected;
    }

    public void SetAlive(bool isAlive)
    {
        // Main calls this every tick for every person whether or not IsAlive changed; without
        // the guard every living person would be retextured and re-measured once a tick.
        if (isAlive == _isAlive)
        {
            return;
        }

        _isAlive = isAlive;

        // Each layer swaps to its own lying-down variant (generate_sprites.py's _lay_down) - the
        // same hairstyle/clothing this person had standing. Retexture carries the new base
        // colour, since BillboardSprite.Apply resets Modulate to white.
        Retexture(_body, isAlive ? _aliveTexturePath : _deadTexturePath, isAlive ? _aliveBodyModulate : DeadTint, AliveColor);
        Retexture(_clothing, isAlive ? _clothingAliveTexturePath : _clothingDeadTexturePath, isAlive ? SpriteTint.ModulateFor(_clothingColor) : DeadTint, _clothingColor);
        Retexture(_hair, isAlive ? _hairAliveTexturePath : _hairDeadTexturePath, isAlive ? SpriteTint.ModulateFor(_hairColor) : DeadTint, _hairColor);

        // Re-painted from the updated base colours even mid-fade, so the fog tint composes with
        // the new ones at once.
        ApplyTints();

        // The lying-down texture already reads as flat on the ground; a leftover bob would lift
        // it, and OnProcess neither walks nor sways the dead to re-derive it.
        if (!isAlive)
        {
            _stepOffset = Vector3.Zero;
            _idleWeight = 0f;
            ApplyPose();
        }

        // The lying-down silhouette is wider and shorter; the collision box and the marker's
        // height both follow from re-measuring the layers.
        RefreshCollisionShape();
    }

}
