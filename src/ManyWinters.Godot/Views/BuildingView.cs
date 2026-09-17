using Godot;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// A hut in the camp: a store to put wood into and take it back out of, and something to mend
// when the weather has had at it. It lights up under the cursor and answers to both buttons like
// the rest of the world does, because there are orders to give here (see TargetActions) - it had
// neither for as long as there was no menu to give them from.
internal partial class BuildingView : SpriteEntityView
{
    // A one-room hut should clear a person's head (PersonView.Height) with some roof to spare;
    // shorter reads as knee-high despite the door in the art.
    public const float Size = 2.8f;
    private const float MinScale = 0.9f;
    private const float MaxScale = 1.1f;
    private const float ShadowDiameter = 3.5f;

    private readonly Entity _building;
    private readonly Action<Entity, MouseButton> _onClicked;

    // Internal for the same reason as PersonView's constructor: only WorldPresenter builds views.
    internal BuildingView(Entity building, HoverArbiter hover, Action<Entity, MouseButton> onClicked, InputEventEventHandler onMissedClick)
        : base(Size, hover, onMissedClick)
    {
        _building = building;
        _onClicked = onClicked;
    }

    protected override void Build()
    {
        var fallbackColor = EntityVisualVariation.Tint(ColorFor(_building.Kind), _building.Id.Seed);
        var scale = EntityVisualVariation.Scale(_building.Id.Seed, MinScale, MaxScale);
        ScaleAndKeepGroundContact(scale, scale);
        SetUpGroundShadow(ShadowDiameter);

        var texturePath = TexturePaths.ForBuilding(_building.Kind);
        Register(BillboardSprite.Create(texturePath, Size, fallbackColor), texturePath);
    }

    // Both buttons: a store has no single obvious thing to do with it, so either one opens the
    // list of what it can do (see Main).
    protected override bool WantsClick(MouseButton button) => true;

    protected override bool OnClicked(MouseButton button)
    {
        _onClicked(_building, button);
        return true;
    }

    // Cached per kind for the same reason (and the same C#-bridge crash under repeated
    // ResourceLoader.Load) as ResourceNodeView.VisualDefinitionCache; a camp is a handful of
    // huts, so this is about the two halves of one concern behaving alike.
    private static readonly Dictionary<EntityKindId, BuildingVisualDefinition?> VisualDefinitionCache = new();

    private static Color ColorFor(EntityKindId kind)
    {
        if (!VisualDefinitionCache.TryGetValue(kind, out var visual))
        {
            var path = $"res://Content/buildings/{kind.Value}/{kind.Value}.tres";
            visual = ResourceLoader.Exists(path) ? ResourceLoader.Load<BuildingVisualDefinition>(path) : null;
            VisualDefinitionCache[kind] = visual;
        }

        return visual?.Color ?? new Color(0.6f, 0.6f, 0.6f);
    }
}
