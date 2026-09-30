namespace ManyWinters.Core.World;

// Fog of war. "Explored" cells stay true forever once seen (a resource once spotted is not
// un-learned); "visible" cells are within someone's sight radius right now, recomputed every
// tick and never persisted. Explored-but-not-visible renders as "remembered", neither as
// "unknown".
public sealed class ExplorationState
{
    private readonly HashSet<ExplorationCell> _explored = new();
    private HashSet<ExplorationCell> _visible = new();

    public IReadOnlyCollection<ExplorationCell> Explored => _explored;

    public static ExplorationCell CellFor(Position position, float cellSizeMeters) =>
        new((int)Math.Floor(position.X / cellSizeMeters), (int)Math.Floor(position.Y / cellSizeMeters));

    public bool IsExplored(ExplorationCell cell) => _explored.Contains(cell);

    public bool IsVisible(ExplorationCell cell) => _visible.Contains(cell);

    // Recomputes Visible from the sight sources (every living person, each tick), then folds it
    // into Explored. A cell counts as visible only if its own centre is within
    // SightRadiusMeters, so sight reads as a circle, not a diamond of squares.
    public void Update(IEnumerable<Position> sightSources, float cellSizeMeters, float sightRadiusMeters)
    {
        var visible = new HashSet<ExplorationCell>();
        var radiusCells = (int)Math.Ceiling(sightRadiusMeters / cellSizeMeters);
        var radiusSquared = sightRadiusMeters * sightRadiusMeters;

        foreach (var source in sightSources)
        {
            var center = CellFor(source, cellSizeMeters);
            for (var dx = -radiusCells; dx <= radiusCells; dx++)
            {
                for (var dy = -radiusCells; dy <= radiusCells; dy++)
                {
                    // Stryker disable once Arithmetic: dx and dy run symmetrically, so subtracting enumerates the same cells
                    var cell = new ExplorationCell(center.X + dx, center.Y + dy);
                    var cellCenterX = (cell.X + 0.5) * cellSizeMeters;
                    var cellCenterY = (cell.Y + 0.5) * cellSizeMeters;
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

    // Erases all exploration so a successor band discovers the world anew.
    public void Reset()
    {
        _explored.Clear();
        _visible.Clear();
    }

    // For SaveGameService only - Explored otherwise grows only through Update.
    internal void RestoreExplored(IEnumerable<ExplorationCell> cells) => _explored.UnionWith(cells);
}
