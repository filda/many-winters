using Godot;

namespace ManyWinters.Godot;

// The [Export] setters are written by Godot itself when the .tres loads, not by any C# caller
// InspectCode can see - hence its "can be made private" is wrong here.
// ReSharper disable MemberCanBePrivate.Global
public partial class BuildingVisualDefinition : Resource
{
    [Export]
    public Color Color { get; set; } = new Color(0.6f, 0.6f, 0.6f);
}
