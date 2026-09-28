using Godot;
using ManyWinters.Core.Population;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// The second Creature drawn in the world: one layer, whichever texture its species drew for
// itself if it has one, else the flat tinted quad BillboardSprite falls back to already - no
// deer art exists yet, and none is drawn here either. Everything about being a creature that
// walks (bob, per-tick target, death) is CreatureView's; this is only which texture and how tall.
internal partial class AnimalView : CreatureView
{
    private const float MinScale = 0.9f;
    private const float MaxScale = 1.1f;
    private const float ShadowDiameterRatio = 0.5f;
    private const int ScaleSalt = 501;

    // A species with no WorldHeight of its own (no .tres, or one that leaves it at 0) draws at
    // about a person's own height - a deer is not a decoration-scale icon.
    private const float DefaultHeight = 1.6f;

    // For a species kind with no .tres visual definition at all - a placeholder muted green,
    // only ever seen if the content is missing both its definition and its art.
    private static readonly Color DefaultColor = new(0.35f, 0.45f, 0.25f);

    // Cached per species, not loaded per animal - the same reasoning as ResourceNodeView's
    // VisualDefinitionCache: many animals of one species loading the same .tres in one frame risks
    // the same GCHandle race.
    private static readonly Dictionary<SpeciesId, ResourceVisualDefinition?> VisualDefinitionCache = new();

    private readonly Animal _animal;
    private readonly SpeciesId _species;
    private readonly Action<Animal, MouseButton> _onClicked;
    private readonly Color _fallbackColor;

    private SpriteLayer _body = null!;
    private Color _aliveModulate;

    // Internal, like other view constructors: only WorldPresenter builds views.
    internal AnimalView(Animal animal, HoverArbiter hover, Action<Animal, MouseButton> onClicked, InputEventEventHandler onMissedClick)
        : base(animal, NominalHeightFor(animal.Species), hover, onMissedClick)
    {
        _animal = animal;
        _species = animal.Species;
        _onClicked = onClicked;
        _fallbackColor = LoadVisualDefinition(_species)?.Color ?? DefaultColor;
    }

    // What WorldPresenter places this node by (origin half a height above the ground) and the
    // height every layer is created at.
    public float Size => NominalHeight;

    protected override void Build()
    {
        var scale = EntityVisualVariation.RangeFor(_animal.Id.Seed, ScaleSalt, MinScale, MaxScale);
        ScaleAndKeepGroundContact(scale, scale);
        InitializeMotion();

        SetUpGroundShadow(Size * ShadowDiameterRatio);

        // Every layer excluded from the occlusion fade, the same call PersonView makes: an animal
        // is no bigger a thing to hide behind than a person is.
        var texturePath = TexturePathFor(_species);
        var sprite = BillboardSprite.Create(texturePath, Size, EntityVisualVariation.Tint(_fallbackColor, _animal.Id.Seed), excludeFromOcclusionFade: true);
        _aliveModulate = sprite.Modulate;
        _body = Register(sprite, texturePath);
    }

    // Both buttons, the same as a person: left selects it, right asks what may be done with it -
    // today nothing, so WorldInputController's menu for one is always empty and never opens.
    protected override bool WantsClick(MouseButton button) => true;

    protected override bool OnClicked(MouseButton button)
    {
        _onClicked(_animal, button);
        return true;
    }

    protected override void ApplyPose(Vector3 offset) => _body.Sprite.Position = offset;

    // No corpse art exists for any species yet, so a dead animal only stops moving (CreatureView's
    // job) and drains to the same dead tint a person's corpse takes, not a
    // fresh one, since it is already the game's one answer to "this body is not alive".
    protected override void OnAliveChanged(bool isAlive) =>
        _body.BaseModulate = PersonLook.TintFor(isAlive, isDecayed: false) ?? _aliveModulate;

    // Once decayed: the one layer tinted one step further, the same as a person's
    // corpse - no bones art exists here either.
    protected override void OnDecayedChanged() =>
        _body.BaseModulate = PersonLook.TintFor(isAlive: false, isDecayed: true)!.Value;

    // A kind with a species PNG (res://Content/species/{id}/{id}.png) draws it; absent,
    // sprite creation already falls back to a flat tinted quad, which is the whole point -
    // no deer art exists yet.
    private static string TexturePathFor(SpeciesId species) => $"res://Content/species/{species.Value}/{species.Value}.png";

    // A species' own height where its .tres sets one, else a plausible default rather than a
    // resource's decoration-scale fallback. Static because the base class needs it before this
    // view has fields.
    private static float NominalHeightFor(SpeciesId species)
    {
        var visual = LoadVisualDefinition(species);
        return visual is { WorldHeight: > 0f } ? visual.WorldHeight : DefaultHeight;
    }

    private static ResourceVisualDefinition? LoadVisualDefinition(SpeciesId species)
    {
        if (VisualDefinitionCache.TryGetValue(species, out var cached))
        {
            return cached;
        }

        var path = $"res://Content/species/{species.Value}/{species.Value}.tres";
        var definition = ResourceLoader.Exists(path) ? ResourceLoader.Load<ResourceVisualDefinition>(path) : null;
        VisualDefinitionCache[species] = definition;
        return definition;
    }
}
