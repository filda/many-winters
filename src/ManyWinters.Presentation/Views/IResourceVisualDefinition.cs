using Godot;

namespace ManyWinters.Presentation.Views;

// What a resource kind's or a species' .tres under Content/ says about how to draw it. The
// Resource script that Godot fills from the file has to live in the Godot project (the engine
// instantiates it by res:// path), which this assembly cannot reference back; the views read the
// loaded resource through this instead.
public interface IResourceVisualDefinition
{
    Color Color { get; }

    // 0 means unset: the view falls back to its own default size.
    float WorldHeight { get; }
}
