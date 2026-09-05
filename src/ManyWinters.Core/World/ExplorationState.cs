namespace ManyWinters.Core.World;

// Fog of war (todo #13): tracks which of the world's coarse grid cells the group has ever seen
// ("explored" - stays true forever once set, so a resource once spotted isn't un-learned when
// everyone wanders off) versus which are within someone's sight radius *right now*
// ("visible" - recomputed fresh every tick from current positions, never persisted). A cell
// that's Explored but not Visible is what the presentation layer renders as "remembered" -
// dimmed/desaturated rather than hidden - and one that's neither is "unknown" - not yet seen at
// all.
public sealed class ExplorationState
{
    // Far coarser than a Position or the decoration-placement spacing (MapLoader/TerrainRenderer
    // both work in centimeters-to-single-digit-meters) - fog only needs to track roughly where
    // the group has been, and a grid this fine would just mean tracking (and, on the Godot side,
    // rendering) many more cells for no gameplay benefit.
    //
    // 5m used to mean a person's own SightRadiusMeters (15m) circle rasterized to a mere
    // 3-cell radius - visibly a rough octagon, not a circle, no matter how much the rendered
    // mesh's surface got smoothed afterward (see FogOfWarRenderer's own history and the
    // user's "ty hrany u osadníků" - the explored *shape* itself was polygonal, not the mesh's
    // texture/lighting). 2.5m instead gives that circle roughly double the resolution
    // (6-cell radius) while staying coarse enough that rebuilding the fog mesh (which scales
    // with cell count squared) on every newly-explored cell stays a rare-event cost, not a
    // per-tick one.
    public const float CellSizeMeters = 2.5f;

    // How far a person can currently see - deliberately smaller than IdleSearchRadius
    // (WorldState's autonomous "go find something to do" range): sight is what the *player*
    // currently knows about, search is what a person can already reach without discovering
    // anything new along the way.
    public const float SightRadiusMeters = 15f;

    private readonly HashSet<ExplorationCell> _explored = new();
    private HashSet<ExplorationCell> _visible = new();

    public IReadOnlyCollection<ExplorationCell> Explored => _explored;

    public IReadOnlyCollection<ExplorationCell> Visible => _visible;

    public static ExplorationCell CellFor(Position position) =>
        new((int)Math.Floor(position.X / CellSizeMeters), (int)Math.Floor(position.Y / CellSizeMeters));

    public bool IsExplored(ExplorationCell cell) => _explored.Contains(cell);

    public bool IsVisible(ExplorationCell cell) => _visible.Contains(cell);

    // Recomputes Visible from scratch against the given sight sources' current positions (every
    // living person's position, each tick - see WorldState.Advance), then folds it into Explored
    // - the one-way "stays known forever" half of the fog. A cell only counts as within sight if
    // its own center is within SightRadiusMeters of a source - not merely the grid square it
    // falls in - so sight reads as a circle, not a diamond of coarse squares.
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

    // For SaveGameService round-tripping only - Explored is otherwise only ever grown via
    // Update, never set directly.
    internal void RestoreExplored(IEnumerable<ExplorationCell> cells) => _explored.UnionWith(cells);
}
