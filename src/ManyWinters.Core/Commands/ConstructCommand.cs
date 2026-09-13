using ManyWinters.Core.Construction;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record ConstructCommand(Person Person, BuildingKindId Kind, Position Position) : ICommand
{
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

        world.AddBuilding(new Building
        {
            Kind = Kind,
            Position = Position,
        });
    }
}
