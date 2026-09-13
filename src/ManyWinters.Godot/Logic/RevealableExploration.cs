using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// The presentation layer's view of a cell's exploration state, with one override: the
// inspector's "Reveal Map" toggle, under which every cell reads as explored and in sight - a
// debugging view. The simulation's ExplorationState is never touched, so switching the reveal
// off restores the honest picture. Every reader (WorldPresenter, FogOfWarRenderer, GroundClouds
// via the fog's distance field) goes through this one lens.
public sealed class RevealableExploration
{
    private readonly ExplorationState _exploration;

    public RevealableExploration(ExplorationState exploration)
    {
        _exploration = exploration;
    }

    public bool RevealAll { get; set; }

    public bool IsExplored(ExplorationCell cell) => RevealAll || _exploration.IsExplored(cell);

    public bool IsVisible(ExplorationCell cell) => RevealAll || _exploration.IsVisible(cell);
}
