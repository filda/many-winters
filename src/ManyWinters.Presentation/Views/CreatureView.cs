using Godot;
using ManyWinters.Core.Population;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Views;

// Everything about a Creature that walks: the per-tick target position and interpolation between
// simulation ticks, the walk bob and the idle bob that hands over to it, and the pose reset a
// death settles into. PersonView was the only Creature view until a second one was added; this
// is what the two share, widened off Person to Creature exactly where PersonView only ever read
// what every Creature has: the seed, the position, IsAlive. A subclass keeps only what is its
// own: which layers it draws, what a click on it means, and - for PersonView - the paper-doll
// retexturing a death swaps in.
public abstract partial class CreatureView : SpriteEntityView
{
    // A cardboard-cutout-on-a-stick bounce while walking. Each creature draws their own rate and
    // amplitude from these ranges, so a shared exact rate would read as a synchronized gait.
    private const float MinWalkCyclesPerSecond = 8f;
    private const float MaxWalkCyclesPerSecond = 12f;
    private const float MinBobAmplitude = 0.06f;
    private const float MaxBobAmplitude = 0.10f;

    // Standing still is not standing frozen: the walk's own bob, a fifth slower and about half as
    // high, so rest and walk hand over without a change of rhythm. Each creature bobs at their own
    // rate from their own phase, so a crowd at rest does not bounce in unison.
    private const float IdleCyclesPerWalkCycle = 0.8f;
    private const float MinIdleBobAmplitude = 0.03f;
    private const float MaxIdleBobAmplitude = 0.05f;

    // A creature is "standing" only after being still this long. Between ticks the interpolated
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

    private readonly Creature _creature;

    private float _walkCyclesPerSecond;
    private float _bobAmplitude;
    private float _idleBobAmplitude;
    private Vector3 _targetPosition;
    private float _interpolationSpeed;
    private float _walkPhase;
    private float _idlePhase;
    private float _idleWeight;
    private float _standingSeconds;

    // The walk's bob, exact while walking and easing away once standing; ApplyPose adds the
    // idle bob, whose weight fades the other way, so the hand-over is never a jump.
    private Vector3 _stepOffset;
    private bool _isAlive = true;
    private bool _isDecayed;

    private protected CreatureView(Creature creature, float nominalHeight, PresentationSettings presentation, HoverArbiter? hover, InputEventEventHandler? onMissedClick)
        : base(nominalHeight, presentation, hover, onMissedClick)
    {
        _creature = creature;
    }

    // The walk cycle runs whether or not anything is fading, so processing never switches off.
    protected sealed override bool NeedsEveryFrame => true;

    // The walk bob moves the layers every frame, so the hit-test plane is pinned to this node's
    // position; anchored to a bobbing sprite, the sampled pixel sweeps across silhouette edges
    // and the hover flickers.
    protected sealed override Vector3? PixelHitAnchor => GlobalPosition;

    // `target` is the position an unscaled creature would render at; this view stands a little
    // higher than that, and so must its target.
    public void SetTargetPosition(Vector3 target, float overSeconds)
    {
        var corrected = target + GroundContactCorrection;
        _interpolationSpeed = WalkCycle.InterpolationSpeed(Position.DistanceTo(corrected), overSeconds);
        _targetPosition = corrected;
    }

    // Main calls this every tick for every creature whether or not IsAlive changed; without the
    // guard every living one would be re-measured once a tick.
    public void SetAlive(bool isAlive)
    {
        if (isAlive == _isAlive)
        {
            return;
        }

        _isAlive = isAlive;
        OnAliveChanged(isAlive);
        ApplyTints();

        // A corpse is not walked or swayed by OnProcess any more; a leftover bob would leave it
        // floating above the ground it just settled onto.
        if (!isAlive)
        {
            _stepOffset = Vector3.Zero;
            _idleWeight = 0f;
            ApplyPose();
        }

        // A lying-down silhouette can be wider and shorter than a standing one; the collision
        // box and the marker's height both follow from re-measuring the layers.
        RefreshCollisionShape();
    }

    // Called every tick for every creature, whether or not the decayed flag changed, the same
    // way SetAlive is - the guard below is what makes the no-change case cost nothing. One-way:
    // there is no coming back from a decayed corpse, so a caller passing false once true is
    // already the case is simply ignored rather than un-deciding it.
    public void SetDecayed(bool isDecayed)
    {
        if (_isDecayed || !isDecayed)
        {
            return;
        }

        _isDecayed = true;
        OnDecayedChanged();
    }

    // Draws this creature's own walk/idle rates from its seed and primes the tick target at
    // wherever WorldPresenter placed the node. Called from a subclass's Build(), after
    // ScaleAndKeepGroundContact - the same order every subclass follows.
    protected void InitializeMotion()
    {
        var seed = _creature.Id.Seed;
        _walkCyclesPerSecond = EntityVisualVariation.RangeFor(seed, salt: 1, MinWalkCyclesPerSecond, MaxWalkCyclesPerSecond);
        _bobAmplitude = EntityVisualVariation.RangeFor(seed, salt: 2, MinBobAmplitude, MaxBobAmplitude);
        _idleBobAmplitude = EntityVisualVariation.RangeFor(seed, salt: 5, MinIdleBobAmplitude, MaxIdleBobAmplitude);
        _idlePhase = EntityVisualVariation.RangeFor(seed, salt: 7, 0f, MathF.Tau);
        _targetPosition = Position;
    }

    // Only the simulation tick moves a creature; this plays that motion back smoothly between
    // ticks, at whatever speed matches how far the tick actually moved it.
    protected sealed override void OnProcess(double delta)
    {
        if (LaunchOptions.Still)
        {
            // A still session: hold the tick target and a neutral pose, skipping the real-time
            // walk/idle bob that would otherwise shift every creature frame to frame.
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

    // Moves every layer this creature draws by the same rigid offset - one cutout, not
    // independently animated parts.
    protected abstract void ApplyPose(Vector3 offset);

    // What a subclass does the moment IsAlive actually flips - PersonView swaps every layer's
    // texture for its lying-down counterpart; AnimalView, with no corpse art of its own, only
    // retints. Nothing by default.
    protected virtual void OnAliveChanged(bool isAlive)
    {
    }

    // What a subclass does the moment its corpse decays past recognition - there is no bones art,
    // so both PersonView and AnimalView only deepen the tint their own dead look already applied.
    // Nothing by default.
    protected virtual void OnDecayedChanged()
    {
    }

    private void ApplyPose() => ApplyPose(_stepOffset + WalkCycle.BobAt(_idlePhase, _idleBobAmplitude * _idleWeight));
}
