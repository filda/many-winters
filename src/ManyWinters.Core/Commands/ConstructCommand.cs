using ManyWinters.Core.Construction;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record ConstructCommand(Person Person, BuildingKindId Kind, Position Position) : ICommand
{
    public void Execute(WorldState world)
    {
        if (!Person.IsAlive || !world.IsWithinReach(Person.Position, Position))
        {
            return;
        }

        var definition = world.Configuration.BuildingCatalog.Get(Kind);
        if (!Person.Inventory.Remove(definition.RequiredItem, definition.RequiredAmount))
        {
            return;
        }

        world.AddBuilding(new Building
        {
            Kind = Kind,
            Position = Position,
        });
    }
}
