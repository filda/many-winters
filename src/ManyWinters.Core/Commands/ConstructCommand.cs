using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record ConstructCommand(Person Person, EntityKindId Kind, Position Position) : ICommand
{
    private const float StartingCondition = 100f;

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (!world.IsWithinReach(Person.Position, Position))
        {
            return ActionBlocker.TooFar;
        }

        var definition = world.Configuration.BuildingCatalog.Get(Kind);
        return Person.Inventory.Get(definition.RequiredItem) < definition.RequiredAmount
            ? ActionBlocker.MissingMaterials
            : ActionBlocker.None;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        var definition = world.Configuration.BuildingCatalog.Get(Kind);
        Person.Inventory.Remove(definition.RequiredItem, definition.RequiredAmount);

        world.AddEntity(new Entity
        {
            Kind = Kind,
            Category = EntityCategory.Building,
            Position = Position,
            Condition = StartingCondition,
            Storage = new Inventory(),
        });
    }
}
