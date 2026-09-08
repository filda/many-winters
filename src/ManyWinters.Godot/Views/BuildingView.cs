using Godot;
using ManyWinters.Core.Construction;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// The one view nothing can point at or click yet: the inspector has no page for a hut (see
// docs/todo/todo.md's note about a real player menu), so it is built inert - no hover arbiter
// and no missed-click handler, which is what makes SpriteEntityView skip the collision shape
// and ray picking altogether. Everything else a world sprite does, it does: seeded size
// variation, a ground shadow, and dimming when the group walks away from its camp.
internal partial class BuildingView : SpriteEntityView
{
    // Was 1.2 - shorter than PersonView.Height (1.8), reading as knee-high next to a person
    // despite the art depicting a door someone could actually walk through. A modest one-room
    // hut should clear a person's head with some roof to spare.
    public const float Size = 2.8f;
    private const float MinScale = 0.9f;
    private const float MaxScale = 1.1f;
    private const float ShadowDiameter = 3.5f;

    private readonly BuildingId _buildingId;
    private readonly BuildingKindId _kind;

    public BuildingView(BuildingId buildingId, BuildingKindId kind)
        : base(Size, hover: null, onMissedClick: null)
    {
        _buildingId = buildingId;
        _kind = kind;
    }

    protected override void Build()
    {
        var fallbackColor = EntityVisualVariation.Tint(ColorFor(_kind), _buildingId.Seed);
        var scale = EntityVisualVariation.Scale(_buildingId.Seed, MinScale, MaxScale);
        ScaleAndKeepGroundContact(scale, scale);
        SetUpGroundShadow(ShadowDiameter);

        var texturePath = TexturePaths.ForBuilding(_kind);
        Register(BillboardSprite.Create(texturePath, Size, fallbackColor), texturePath);
    }

    // Cached per kind, not reloaded per building - the same reasoning (and the same C#-bridge
    // crash under repeated ResourceLoader.Load of one path) as ResourceNodeView's own
    // definition cache. A camp is a handful of huts today, so this is about keeping the two
    // halves of one concern behaving alike rather than about the load itself.
    private static readonly Dictionary<BuildingKindId, BuildingVisualDefinition?> VisualDefinitionCache = new();

    private static Color ColorFor(BuildingKindId kind)
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
