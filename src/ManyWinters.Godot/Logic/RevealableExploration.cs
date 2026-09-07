using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// What the presentation layer asks about a cell's exploration state, with one override: the
// inspector's "Reveal Map" toggle. Switched on, every cell reads as explored and in sight, so
// the whole terrain, every resource node and every cloud bank show as if fog of war did not
// exist - a debugging view, not a gameplay one. The simulation's own ExplorationState is never
// touched: it keeps recording where the group has really been, so switching the reveal off
// again restores the honest picture. Every reader (WorldPresenter, FogOfWarRenderer, and
// GroundClouds through the fog's distance field) goes through this one lens, so there is no
// second place where "is this revealed" has to be remembered.
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
