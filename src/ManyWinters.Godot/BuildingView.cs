using Godot;
using ManyWinters.Core.Construction;

namespace ManyWinters.Godot;

public partial class BuildingView : Node3D
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
    {
        _buildingId = buildingId;
        _kind = kind;
    }

    public override void _Ready()
    {
        var fallbackColor = EntityVisualVariation.Tint(ColorFor(_kind), _buildingId.Value);
        var scale = EntityVisualVariation.Scale(_buildingId.Value, MinScale, MaxScale);
        Scale = Vector3.One * scale;
        // Same ground-contact fix as PersonView/ResourceNodeView: WorldPresenter set this
        // node's own Position assuming Scale stayed 1, so Scale.Y != 1 shifts the sprite's
        // (and the ground shadow's, both children scaled along with it) bottom edge away
        // from the ground by Size/2*(scale-1). Shifting Position back by that same amount
        // cancels it out.
        Position += new Vector3(0f, (Size / 2f) * (scale - 1f), 0f);

        var groundShadow = GroundShadow.Create(ShadowDiameter);
        groundShadow.Position += new Vector3(0, (-Size / 2f) + GroundShadow.GroundOffset, 0);
        AddChild(groundShadow);

        AddChild(BillboardSprite.Create(TexturePathFor(_kind), Size, fallbackColor));
    }

    private static string TexturePathFor(BuildingKindId kind)
        => $"res://Content/buildings/{kind.Value}/{kind.Value}.png";

    private static Color ColorFor(BuildingKindId kind)
    {
        var path = $"res://Content/buildings/{kind.Value}/{kind.Value}.tres";
        var visual = ResourceLoader.Exists(path) ? ResourceLoader.Load<BuildingVisualDefinition>(path) : null;
        return visual?.Color ?? new Color(0.6f, 0.6f, 0.6f);
    }
}
