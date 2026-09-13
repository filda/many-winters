namespace ManyWinters.Core.World;

// Fog of war. "Explored" cells stay true forever once seen (a resource once spotted is not
// un-learned); "visible" cells are within someone's sight radius right now, recomputed every
// tick and never persisted. Explored-but-not-visible renders as "remembered", neither as
// "unknown".
public sealed class ExplorationState
{
    // Far coarser than a Position: fog only tracks roughly where the group has been, and every
    // cell costs rendering on the Godot side. Fine enough that a SightRadiusMeters circle spans
    // a 6-cell radius and reads as a circle rather than an octagon, while rebuilding the fog
    // mesh (cost scales with cell count squared) on a newly explored cell stays a rare event.
    public const float CellSizeMeters = 2.5f;

    // How far a person sees. Smaller than IdleSearchRadius: sight is what the player knows
    // about, search is what a person can reach without discovering anything new on the way.
    public const float SightRadiusMeters = 15f;

    private readonly HashSet<ExplorationCell> _explored = new();
    private HashSet<ExplorationCell> _visible = new();

    public IReadOnlyCollection<ExplorationCell> Explored => _explored;

    public static ExplorationCell CellFor(Position position) =>
        new((int)Math.Floor(position.X / CellSizeMeters), (int)Math.Floor(position.Y / CellSizeMeters));

    public bool IsExplored(ExplorationCell cell) => _explored.Contains(cell);

    public bool IsVisible(ExplorationCell cell) => _visible.Contains(cell);

    // Recomputes Visible from the sight sources (every living person, each tick - see
    // WorldState.Advance), then folds it into Explored. A cell counts as visible only if its own
    // centre is within SightRadiusMeters, so sight reads as a circle, not a diamond of squares.
    public void Update(IEnumerable<Position> sightSources)
    {
        var visible = new HashSet<ExplorationCell>();
        var radiusCells = (int)Math.Ceiling(SightRadiusMeters / CellSizeMeters);
        var radiusSquared = SightRadiusMeters * SightRadiusMeters;

        foreach (var source in sightSources)
        {
            var center = CellFor(source);
            for (var dx = -radiusCells; dx <= radiusCells; dx++)
            {
                for (var dy = -radiusCells; dy <= radiusCells; dy++)
                {
                    // Stryker disable once Arithmetic: dx and dy run symmetrically, so subtracting enumerates the same cells
                    var cell = new ExplorationCell(center.X + dx, center.Y + dy);
                    var cellCenterX = (cell.X + 0.5) * CellSizeMeters;
                    var cellCenterY = (cell.Y + 0.5) * CellSizeMeters;
                    var dxToCenter = cellCenterX - source.X;
                    var dyToCenter = cellCenterY - source.Y;

                    // Stryker disable once Equality: a cell center landing exactly on the
                    // radius has probability zero, so <= and < accept the same cells
                    if (((dxToCenter * dxToCenter) + (dyToCenter * dyToCenter)) <= radiusSquared)
                    {
                        visible.Add(cell);
                    }
                }
            }
        }

        _visible = visible;
        _explored.UnionWith(visible);
    }

    // For SaveGameService only - Explored otherwise grows only through Update.
    internal void RestoreExplored(IEnumerable<ExplorationCell> cells) => _explored.UnionWith(cells);
}
