using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Destroys a fellable resource node (a fruit tree, say), leaving behind one or more one-time
// piles of whatever ResourceDefinition.FellLeaves says (typically wood) that still have to be
// gathered - unlike GatherCommand, which takes from the node repeatedly and leaves it standing.
public sealed record FellCommand(Person Person, ResourceNode Node) : ICommand
{
    // How far a second (or later) leftover - a fallen log next to the stump a tree leaves in
    // its own spot - lands from where the tree stood, so the two don't sit exactly on top of
    // each other.
    private const double SubsequentLeftoverDistance = 1.4;

    public void Execute(WorldState world)
    {
        if (!Person.IsAlive || !Node.IsAlive || !world.IsWithinReach(Person.Position, Node.Position))
        {
            return;
        }

        var resource = world.Configuration.ResourceCatalog.Get(Node.Kind);
        var skillDefinition = world.Configuration.SkillCatalog.Get(resource.Skill);
        if (!resource.CanFell || !Person.KnownTechniques.Contains(skillDefinition.BaseTechnique))
        {
            return;
        }

        Node.IsAlive = false;
        Node.DeathTick = world.Clock.CurrentTick;
        Node.CauseOfDeath = ResourceDeathCause.Felled;

        if (resource.FellLeaves is not { } leftovers)
        {
            return;
        }

        for (var i = 0; i < leftovers.Count; i++)
        {
            var leftover = leftovers[i];
            if (leftover.Amount <= 0)
            {
                continue;
            }

            var position = i == 0 ? Node.Position : OffsetPosition(Node.Position, Node.Id.Seed, i);
            new SpawnResourceNodeCommand(leftover.Kind, position, leftover.Amount).Execute(world);
        }
    }

    // Deterministic from the node's own id (see EntityId.SeedOf) and the leftover's index, not
    // a shared mutable Random - the same felled tree drops its log in the same spot on replay.
    private static Position OffsetPosition(Position origin, int nodeSeed, int index)
    {
        var rng = new Random(SeedHash.Avalanche(unchecked((uint)nodeSeed + ((uint)index * 2654435761u))));
        var angle = rng.NextDouble() * Math.Tau;
        return new Position(origin.X + (SubsequentLeftoverDistance * Math.Cos(angle)),
            origin.Y + (SubsequentLeftoverDistance * Math.Sin(angle)));
    }
}
