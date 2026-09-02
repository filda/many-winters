namespace ManyWinters.Core.World;

// One square of the fog-of-war grid - deliberately much coarser than a Position (see
// ExplorationState.CellSizeMeters) since fog only needs to track roughly where the group has
// been, not exact footprints.
public readonly record struct ExplorationCell(int X, int Y);
