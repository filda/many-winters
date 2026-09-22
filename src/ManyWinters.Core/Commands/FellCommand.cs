using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Destroys a fellable resource node (a fruit tree, say), leaving behind one or more one-time
// piles of whatever ResourceDefinition.FellLeaves says (typically wood) that still have to be
// gathered - unlike GatherCommand, which takes from the node repeatedly and leaves it standing.
public sealed record FellCommand(Person Person, Entity Node) : ICommand
{
    // How far a second (or later) leftover - a fallen log next to the stump a tree leaves in
    // its own spot - lands from where the tree stood, so the two don't sit exactly on top of
    // each other.
    private const double SubsequentLeftoverDistance = 1.4;

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (Node.Growth is not { IsAlive: true })
        {
            return ActionBlocker.TargetIsGone;
        }

        if (!world.IsWithinReach(Person.Position, Node.Position))
        {
            return ActionBlocker.TooFar;
        }

        var resource = world.Configuration.ResourceCatalog.Get(Node.Kind);
        if (!resource.CanFell)
        {
            return ActionBlocker.CannotBeFelled;
        }

        var skillDefinition = world.Configuration.SkillCatalog.Get(resource.Skill);
        if (resource.RequiresToolToFell && Person.Inventory.BestChoppingScore(world.Configuration.ItemCatalog) <= 0f)
        {
            return ActionBlocker.MissingTool;
        }

        return Person.KnownTechniques.Contains(skillDefinition.BaseTechnique)
            ? ActionBlocker.None
            : ActionBlocker.NotLearned;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        var growth = Node.Growth!;
        growth.IsAlive = false;
        growth.DeathTick = world.Clock.CurrentTick;
        growth.CauseOfDeath = ResourceDeathCause.Felled;

        var leftovers = world.Configuration.ResourceCatalog.Get(Node.Kind).FellLeaves;
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

    // Deterministic from the node's own id (see IdGeneration.SeedOf) and the leftover's index, not
    // a shared mutable Random - the same felled tree drops its log in the same spot on replay.
    private static Position OffsetPosition(Position origin, int nodeSeed, int index)
    {
        var rng = new Random(SeedHash.Avalanche(unchecked((uint)nodeSeed + ((uint)index * 2654435761u))));
        var angle = rng.NextDouble() * Math.Tau;
        return new Position(origin.X + (SubsequentLeftoverDistance * Math.Cos(angle)),
            origin.Y + (SubsequentLeftoverDistance * Math.Sin(angle)));
    }
}
