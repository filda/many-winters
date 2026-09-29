using Godot;

namespace ManyWinters.Presentation.Views;

// What a building kind's .tres under Content/ says about how to draw it; the same split as
// IResourceVisualDefinition, for the same reason.
public interface IBuildingVisualDefinition
{
    Color Color { get; }
}
