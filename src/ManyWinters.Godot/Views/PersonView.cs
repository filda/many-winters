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

    // A cardboard-cutout-on-a-stick bounce while walking. Each person draws their own rate and
    // amplitude from these ranges in Build; a shared exact rate reads as a synchronized gait.
    private const float MinWalkCyclesPerSecond = 8f;
    private const float MaxWalkCyclesPerSecond = 12f;
    private const float MinBobAmplitude = 0.06f;
    private const float MaxBobAmplitude = 0.10f;

    // Standing still is not standing frozen: the walk's own bob, a fifth slower and about half as
    // high, so rest and walk hand over without a change of rhythm. Each person bobs at their own
    // rate from their own phase, so a crowd at rest does not bounce in unison.
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
    // away.
    private const float IdleFadeInSeconds = 0.35f;
    private const float IdleFadeOutSeconds = 0.15f;

    // How quickly the last step's bob eases away once standing, so nobody stays frozen
    // mid-bounce.
    private const float StepSettleSeconds = 0.25f;

    private static readonly Color AliveColor = new(0.9f, 0.7f, 0.5f);

    private readonly Person _person;
    private readonly Action<Person, MouseButton> _onClicked;

    // The same layers twice, standing and laid on their side, so SetAlive swaps to the *same*
    // hairstyle/clothing lying down.
    private readonly PersonLook _standing;
    private readonly PersonLook _lying;
    private SpriteLayer _body = null!;
    private SpriteLayer _clothing = null!;
    private SpriteLayer _hair = null!;

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
    // invariant is the presentation layer's own business.
    internal PersonView(Person person, HoverArbiter hover, Action<Person, MouseButton> onClicked, InputEventEventHandler onMissedClick)
        : base(Height, hover, onMissedClick)
    {
        _person = person;
        _onClicked = onClicked;
        _standing = PersonLook.For(_person.Id.Seed, _person.Sex, lyingDown: false);
        _lying = PersonLook.For(_person.Id.Seed, _person.Sex, lyingDown: true);
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

        // Every layer is excluded from the occlusion fade: a person is too small to hide much, and
        // a ghosted one reads as a bug - with nobody selected the fade aims at the camera's own
        // target, so at the start whoever stood in front of the band turned see-through.
        var body = BillboardSprite.Create(_standing.Body, Height, AliveColor, excludeFromOcclusionFade: true);
        _aliveBodyModulate = body.Modulate;
        _body = Register(body, _standing.Body);

        // AlphaCutMode.Disabled, not the default OpaquePrepass: an overlay at the body's exact
        // position and depth needs ordinary alpha blending to composite cleanly, since
        // OpaquePrepass has no defined order between two billboards at one depth.
        var clothing = BillboardSprite.Create(_standing.Clothing, Height, _standing.ClothingColor, SpriteBase3D.AlphaCutMode.Disabled, renderPriority: 1, excludeFromOcclusionFade: true);
        clothing.Modulate = SpriteTint.ModulateFor(_standing.ClothingColor);
        _clothing = Register(clothing, _standing.Clothing);

        var hair = BillboardSprite.Create(_standing.Hair, Height, _standing.HairColor, SpriteBase3D.AlphaCutMode.Disabled, renderPriority: 2, excludeFromOcclusionFade: true);
        hair.Modulate = SpriteTint.ModulateFor(_standing.HairColor);
        _hair = Register(hair, _standing.Hair);
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
        if (LaunchOptions.Still)
        {
            // A still session: hold the tick target and a neutral pose, skipping the real-time
            // walk/idle bob that would otherwise shift every person frame to frame.
            Position = _targetPosition;
            _stepOffset = Vector3.Zero;
            _idleWeight = 0f;
            ApplyPose();
            return;
        }

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

        // The last step's pose is held, not snapped to neutral, through the tick-boundary gap;
        // snapping reads as a synchronized hiccup across the crowd. The walk phase stays where
        // it stopped for the same reason, so phases drift apart instead of rewinding together.
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
    // higher than that, and so must its target.
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
        var look = isAlive ? _standing : _lying;
        Retexture(_body, look.Body, isAlive ? _aliveBodyModulate : PersonLook.DeadTint, AliveColor);
        Retexture(_clothing, look.Clothing, isAlive ? SpriteTint.ModulateFor(look.ClothingColor) : PersonLook.DeadTint, look.ClothingColor);
        Retexture(_hair, look.Hair, isAlive ? SpriteTint.ModulateFor(look.HairColor) : PersonLook.DeadTint, look.HairColor);

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
