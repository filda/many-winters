using Godot;
using ManyWinters.Core.Population;
using ManyWinters.Presentation.Logic;
using ManyWinters.Presentation.Sprites;

namespace ManyWinters.Presentation.Views;

// A person, drawn paper-doll style: body, garment and hairstyle layered on top, each an
// independent seeded pick, each swapped for its lying-down counterpart on death. Everything
// about a creature that walks (the walk/idle bob, the per-tick target, the death pose reset) is
// CreatureView's; this keeps only what belongs to a person - the three layers, their looks, and
// what a click on one means.
public partial class PersonView : CreatureView
{
    public const float Height = 1.8f;
    private const float MinScale = 0.92f;
    private const float MaxScale = 1.08f;
    private const float ShadowDiameter = 0.9f;

    private static readonly Color AliveColor = new(0.9f, 0.7f, 0.5f);

    private readonly Person _person;
    private readonly Action<Person, MouseButton> _onClicked;

    // The same layers twice, standing and laid on their side, so SetAlive swaps to the *same*
    // hairstyle/clothing lying down.
    private PersonLook _standing;
    private PersonLook _lying;
    private SpriteLayer _body = null!;
    private SpriteLayer _clothing = null!;
    private SpriteLayer _hair = null!;

    // The body layer's alive colour, so SetAlive can put it back: normally white, but a missing
    // texture leaves the fallback colour here.
    private Color _aliveBodyModulate;

    // Internal, like the HoverArbiter it takes: only WorldPresenter builds views, and the hover
    // invariant is the presentation layer's business.
    internal PersonView(Person person, LifeCycle lifeCycle, double ageInYears, PresentationSettings presentation, HoverArbiter hover, Action<Person, MouseButton> onClicked, InputEventEventHandler onMissedClick)
        : base(person, lifeCycle, ageInYears, Height, presentation, hover, onMissedClick)
    {
        _person = person;
        _onClicked = onClicked;
        _standing = PersonLook.For(_person.Id.Seed, _person.Sex, Stage, lyingDown: false);
        _lying = PersonLook.For(_person.Id.Seed, _person.Sex, Stage, lyingDown: true);
    }

    protected override void Build()
    {
        // A narrow range, but the ground-contact correction applies all the same.
        ApplySeedScale(EntityVisualVariation.Scale(_person.Id.Seed, MinScale, MaxScale));
        InitializeMotion();

        SetUpGroundShadow(ShadowDiameter);

        // Every layer is excluded from the occlusion fade: a person is too small to hide much, and
        // a ghosted one reads as a bug - with nobody selected the fade aims at the camera's
        // target, so at the start whoever stood in front of the band turned see-through.
        var body = BillboardSprite.Create(_standing.Body, Height, AliveColor, excludeFromOcclusionFade: true);
        _aliveBodyModulate = body.Modulate;
        _body = Register(body, _standing.Body);

        // Ordinary alpha blending, not the default opaque-prepass mode: an overlay at the body's
        // exact position and depth needs to composite cleanly, since the opaque-prepass mode has
        // no defined order between two billboards at one depth.
        var clothing = BillboardSprite.Create(_standing.Clothing, Height, _standing.ClothingColor, SpriteBase3D.AlphaCutMode.Disabled, renderPriority: 1, excludeFromOcclusionFade: true);
        clothing.Modulate = SpriteTint.ModulateFor(_standing.ClothingColor);
        _clothing = Register(clothing, _standing.Clothing);

        var hair = BillboardSprite.Create(_standing.Hair, Height, _standing.HairColor, SpriteBase3D.AlphaCutMode.Disabled, renderPriority: 2, excludeFromOcclusionFade: true);
        hair.Modulate = SpriteTint.ModulateFor(_standing.HairColor);
        _hair = Register(hair, _standing.Hair);
    }

    // A person answers to either button - left selects them, right is an order aimed at them -
    // unlike everything else in the world, which only ever takes a left click.
    protected override bool WantsClick(MouseButton button) => true;

    protected override bool OnClicked(MouseButton button)
    {
        _onClicked(_person, button);
        return true;
    }

    // All three layers take the same offset, not their own - they are one rigid cutout.
    protected override void ApplyPose(Vector3 offset)
    {
        _body.Sprite.Position = offset;
        _clothing.Sprite.Position = offset;
        _hair.Sprite.Position = offset;
    }

    // Each layer swaps to its own generated lying-down variant - the same hairstyle/clothing
    // this person had standing. Retexture carries the new base colour, since applying a new
    // texture resets the tint to white.
    protected override void OnAliveChanged(bool isAlive) =>
        ApplyLook(isAlive ? _standing : _lying, PersonLook.TintFor(isAlive, isDecayed: false));

    // The child's layers are another silhouette from the grown ones, and a child who dies stays a
    // child: the new look is worked out for the state the person is in, living, dead or decayed.
    protected override void OnStageChanged(LifeStage stage)
    {
        _standing = PersonLook.For(_person.Id.Seed, _person.Sex, stage, lyingDown: false);
        _lying = PersonLook.For(_person.Id.Seed, _person.Sex, stage, lyingDown: true);
        ApplyLook(IsAlive ? _standing : _lying, PersonLook.TintFor(IsAlive, IsDecayed));
        RefreshCollisionShape();
    }

    // Once decayed: the same lying-down layers, tinted one step further towards
    // bone. There is no bones art yet, and this only fires once a person is already dead, so
    // the lying-down look is already in place.
    protected override void OnDecayedChanged()
    {
        ApplyLook(_lying, PersonLook.TintFor(isAlive: false, isDecayed: true));
    }

    private void ApplyLook(PersonLook look, Color? tint)
    {
        Retexture(_body, look.Body, tint ?? _aliveBodyModulate, AliveColor);
        Retexture(_clothing, look.Clothing, tint ?? SpriteTint.ModulateFor(look.ClothingColor), look.ClothingColor);
        Retexture(_hair, look.Hair, tint ?? SpriteTint.ModulateFor(look.HairColor), look.HairColor);
    }
}
