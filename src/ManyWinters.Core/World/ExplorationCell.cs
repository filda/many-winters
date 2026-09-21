namespace ManyWinters.Core.World;

// One square of the fog-of-war grid, far coarser than a Position; fog only tracks roughly
// where the group has been.
public readonly record struct ExplorationCell(int X, int Y);
