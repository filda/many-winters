using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Destroys a fellable resource node (a fruit tree, say), leaving behind a one-time pile of
// whatever ResourceDefinition.FellLeavesKind says (typically wood) that still has to be
// gathered - unlike GatherCommand, which takes from the node repeatedly and leaves it standing.
public sealed record FellCommand(PersonId PersonId, ResourceNodeId ResourceNodeId) : ICommand
{
    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        var node = world.ResourceNodes.FirstOrDefault(n => n.Id == ResourceNodeId && n.IsAlive);
        if (person is null || node is null || !world.IsWithinReach(person.Position, node.Position))
        {
            return;
        }

        var resource = world.Configuration.ResourceCatalog.Get(node.Kind);
        var skillDefinition = world.Configuration.SkillCatalog.Get(resource.Skill);
        if (!resource.CanFell || !person.KnownTechniques.Contains(skillDefinition.BaseTechnique))
        {
            return;
        }

        node.IsAlive = false;
        node.DeathTick = world.Clock.CurrentTick;
        node.CauseOfDeath = ResourceDeathCause.Felled;

        if (resource.FellLeavesKind is { } leftoverKind && resource.FellLeavesAmount > 0)
        {
            new SpawnResourceNodeCommand(leftoverKind, node.Position, resource.FellLeavesAmount).Execute(world);
        }
    }
}
