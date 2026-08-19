using Godot;

namespace ManyWinters.Godot;

public partial class BuildingVisualDefinition : Resource
{
    [Export]
    public Color Color { get; set; } = new Color(0.6f, 0.6f, 0.6f);
}
