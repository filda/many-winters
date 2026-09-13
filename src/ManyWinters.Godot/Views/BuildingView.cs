using Godot;
using ManyWinters.Core.Construction;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// The one view nothing can point at or click yet: the inspector has no page for a hut (see
// docs/todo/todo.md on a player menu), so it takes no hover arbiter and no missed-click
// handler, which makes SpriteEntityView skip the collision shape and ray picking. Everything
// else a world sprite does - seeded size, ground shadow, fog dimming - it does.
internal partial class BuildingView : SpriteEntityView
{
    // A one-room hut should clear a person's head (PersonView.Height) with some roof to spare;
    // shorter reads as knee-high despite the door in the art.
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

    // Cached per kind for the same reason (and the same C#-bridge crash under repeated
    // ResourceLoader.Load) as ResourceNodeView.VisualDefinitionCache; a camp is a handful of
    // huts, so this is about the two halves of one concern behaving alike.
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
