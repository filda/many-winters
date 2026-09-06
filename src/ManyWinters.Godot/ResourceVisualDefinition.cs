using Godot;

namespace ManyWinters.Godot;

// The [Export] setters are written by Godot itself when the .tres loads, not by any C# caller
// InspectCode can see - hence its "can be made private" is wrong here.
// ReSharper disable MemberCanBePrivate.Global
public partial class ResourceVisualDefinition : Resource
{
    [Export]
    public Color Color { get; set; } = new Color(0.2f, 0.8f, 0.2f);

    // 0 (the default) means "unset" - ResourceNodeView falls back to its own CanFell-based
    // Size instead (TreeSize for a fellable fruit tree, DefaultSize for a small icon like
    // mushroom/potato). Kinds ported over from what used to be purely-visual terrain
    // decoration (conifer/deciduous trees, bushes, rocks, ...) need their own explicit
    // height instead, since neither of those two defaults matches what they used to be
    // scattered at.
    [Export]
    public float WorldHeight { get; set; }
}
