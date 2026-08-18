using Godot;

namespace ManyWinters.Godot;

public partial class ResourceVisualDefinition : Resource
{
    [Export]
    public Color Color { get; set; } = new Color(0.2f, 0.8f, 0.2f);
}
