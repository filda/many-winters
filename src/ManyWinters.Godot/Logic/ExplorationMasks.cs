using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// The exact, unblurred record of where the fog boundary is, one value per texel of the grid the
// shaders sample. Kept apart from the textures it ends up in (FogOfWarRenderer) because what
// goes in each channel is a decision about the two fog tiers, not about how to fill an Image:
//
//   Unexplored - 1 where that point's cell has never been seen, else 0.
//   Remembered - 1 where it has been seen but nobody has it in sight right now, else 0.
//   Explored   - the same boundary as a flag, for the distance field to measure out from.
//
// A blurred copy of the first two is what carries the soft falloff a fog edge should have; the
// shaders multiply sharp by blurred so the softness can only ever show on the unexplored side.
internal static class ExplorationMasks
{
    internal sealed record Masks(float[,] Unexplored, float[,] Remembered, bool[,] Explored);

    internal static Masks Build(RevealableExploration exploration, TexelGrid grid)
    {
        var size = grid.Size;
        var unexplored = new float[size, size];
        var remembered = new float[size, size];
        var explored = new bool[size, size];

        for (var ty = 0; ty < size; ty++)
        {
            var worldZ = grid.WorldAt(ty);
            for (var tx = 0; tx < size; tx++)
            {
                var cell = ExplorationState.CellFor(new Position(grid.WorldAt(tx), worldZ));
                var seen = exploration.IsExplored(cell);

                unexplored[ty, tx] = seen ? 0f : 1f;
                // Explored but out of sight - the two tiers are exclusive, so somewhere
                // currently visible is neither unknown nor remembered.
                remembered[ty, tx] = seen && !exploration.IsVisible(cell) ? 1f : 0f;
                explored[ty, tx] = seen;
            }
        }

        return new Masks(unexplored, remembered, explored);
    }
}
