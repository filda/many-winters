using Godot;
using ManyWinters.Presentation.Views;

namespace ManyWinters.Godot;

// The [Export] setters are written by Godot itself when the .tres loads, not by any C# caller
// InspectCode can see - hence its "can be made private" is wrong here.
// ReSharper disable MemberCanBePrivate.Global
public partial class BuildingVisualDefinition : Resource, IBuildingVisualDefinition
{
    [Export]
    public Color Color { get; set; } = new Color(0.6f, 0.6f, 0.6f);
}
