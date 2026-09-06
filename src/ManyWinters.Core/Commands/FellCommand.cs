using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Destroys a fellable resource node (a fruit tree, say), leaving behind a one-time pile of
// whatever ResourceDefinition.FellLeavesKind says (typically wood) that still has to be
// gathered - unlike GatherCommand, which takes from the node repeatedly and leaves it standing.
public sealed record FellCommand(Person Person, ResourceNode Node) : ICommand
{
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

        if (resource.FellLeavesKind is { } leftoverKind && resource.FellLeavesAmount > 0)
        {
            new SpawnResourceNodeCommand(leftoverKind, Node.Position, resource.FellLeavesAmount).Execute(world);
        }
    }
}
