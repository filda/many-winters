using Godot;
using ManyWinters.Core.Construction;

namespace ManyWinters.Godot;

public partial class BuildingView : Node3D
{
    public const float Size = 1.2f;
    private const float MinScale = 0.9f;
    private const float MaxScale = 1.1f;
    private const float ShadowDiameter = 1.5f;

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
        Scale = Vector3.One * EntityVisualVariation.Scale(_buildingId.Value, MinScale, MaxScale);

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
