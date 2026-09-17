using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// A fresh node is full: MaxAmount is what it regenerates toward (see WorldState.Advance). The
// id is normally the node's own to draw (see EntityId) - only a creator that must produce the
// same world twice (MapLoader) names one.
public sealed record SpawnResourceNodeCommand(EntityId Id, EntityKindId Kind, Position Position, float Amount) : ICommand
{
    public SpawnResourceNodeCommand(EntityKindId kind, Position position, float amount)
        : this(EntityId.New(), kind, position, amount)
    {
    }

    // World-building, not a player action (see SpawnPersonCommand.Blocker).
    public ActionBlocker Blocker(WorldState world) => ActionBlocker.None;

    public void Execute(WorldState world) => world.AddEntity(new Entity
    {
        Id = Id,
        Kind = Kind,
        Category = EntityCategory.Growable,
        Position = Position,
        Growth = new GrowthState
        {
            RemainingAmount = Amount,
            MaxAmount = Amount,
        },
    });
}
