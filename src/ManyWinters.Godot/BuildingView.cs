using Godot;
using ManyWinters.Core.Construction;

namespace ManyWinters.Godot;

public partial class BuildingView : Node3D
{
    public const float Size = 1.2f;
    private const float MaxRotationDegrees = 25f;

    private readonly BuildingId _buildingId;
    private readonly BuildingKindId _kind;

    public BuildingView(BuildingId buildingId, BuildingKindId kind)
    {
        _buildingId = buildingId;
        _kind = kind;
    }

    public override void _Ready()
    {
        var color = EntityVisualVariation.Tint(ColorFor(_kind), _buildingId.Value);
        RotationDegrees = new Vector3(0f, EntityVisualVariation.RotationDegrees(_buildingId.Value, MaxRotationDegrees), 0f);

        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(Size, Size, Size) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
        });
    }

    private static Color ColorFor(BuildingKindId kind)
    {
        var path = $"res://Content/buildings/{kind.Value}/{kind.Value}.tres";
        var visual = ResourceLoader.Exists(path) ? ResourceLoader.Load<BuildingVisualDefinition>(path) : null;
        return visual?.Color ?? new Color(0.6f, 0.6f, 0.6f);
    }
}
