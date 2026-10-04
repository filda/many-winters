namespace ManyWinters.Core.World;

// The world's entities bucketed by where they stand, so "what is near here" reads a few cells
// instead of every tree, bush and tuft on the map. An entity never moves - Position is fixed at
// creation - so it is filed once when added and unfiled once when removed.
//
// Answers come back in the order the entities were added, the same order Entities lists them
// in: a nearest-wins search keeps the first of two equally near, and a sum of pushes depends
// on the order it adds them, so reading from cells must not change what the simulation decides.
public sealed class EntityGrid
{
    private readonly double _cellSize;

    // Each entity filed with when it was added, so an answer is put back in that order by
    // comparing numbers rather than looking each entity up.
    private readonly Dictionary<(long X, long Y), List<(long Order, Entity Entity)>> _cells = new();
    private long _nextOrder;

    public EntityGrid(double cellSize)
    {
        if (cellSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "A cell must have a size.");
        }

        _cellSize = cellSize;
    }

    public void Add(Entity entity)
    {
        var key = CellOf(entity.Position.X, entity.Position.Y);
        if (!_cells.TryGetValue(key, out var cell))
        {
            cell = new List<(long Order, Entity Entity)>();
            _cells[key] = cell;
        }

        cell.Add((_nextOrder++, entity));
    }

    public void Remove(Entity entity)
    {
        var key = CellOf(entity.Position.X, entity.Position.Y);
        if (!_cells.TryGetValue(key, out var cell))
        {
            return;
        }

        cell.RemoveAll(filed => ReferenceEquals(filed.Entity, entity));
        if (cell.Count == 0)
        {
            _cells.Remove(key);
        }
    }

    // Every entity no further than radius from center, in the order they were added.
    public List<Entity> Within(Position center, double radius)
    {
        var found = new List<(long Order, Entity Entity)>();
        var (minX, minY) = CellOf(center.X - radius, center.Y - radius);
        var (maxX, maxY) = CellOf(center.X + radius, center.Y + radius);
        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                if (!_cells.TryGetValue((x, y), out var cell))
                {
                    continue;
                }

                foreach (var filed in cell)
                {
                    if (WorldState.Distance(center, filed.Entity.Position) <= radius)
                    {
                        found.Add(filed);
                    }
                }
            }
        }

        found.Sort((a, b) => a.Order.CompareTo(b.Order));
        return found.ConvertAll(filed => filed.Entity);
    }

    private (long X, long Y) CellOf(double x, double y) => ((long)Math.Floor(x / _cellSize), (long)Math.Floor(y / _cellSize));
}
