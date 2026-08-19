using Godot;
using ManyWinters.Core.Construction;

namespace ManyWinters.Godot;

public partial class BuildingView : Node3D
{
    public const float Size = 1.2f;

    private readonly BuildingKindId _kind;

    public BuildingView(BuildingKindId kind)
    {
        _kind = kind;
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(Size, Size, Size) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = ColorFor(_kind) },
        });
    }

    private static Color ColorFor(BuildingKindId kind)
    {
        var path = $"res://Content/buildings/{kind.Value}/{kind.Value}.tres";
        var visual = ResourceLoader.Exists(path) ? ResourceLoader.Load<BuildingVisualDefinition>(path) : null;
        return visual?.Color ?? new Color(0.6f, 0.6f, 0.6f);
    }
}
