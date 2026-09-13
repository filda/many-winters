using Godot;

namespace ManyWinters.Godot;

// The [Export] setters are written by Godot itself when the .tres loads, not by any C# caller
// InspectCode can see - hence its "can be made private" is wrong here.
// ReSharper disable MemberCanBePrivate.Global
public partial class ResourceVisualDefinition : Resource
{
    [Export]
    public Color Color { get; set; } = new Color(0.2f, 0.8f, 0.2f);

    // 0 (the default) means unset: ResourceNodeView falls back to its CanFell-based size (TreeSize
    // or DefaultSize). Former decoration kinds (conifer, bush, rock, ...) fit neither default and
    // set an explicit height.
    [Export]
    public float WorldHeight { get; set; }
}
