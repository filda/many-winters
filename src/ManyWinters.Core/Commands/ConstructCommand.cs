using ManyWinters.Core.Construction;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record ConstructCommand(PersonId PersonId, BuildingKindId Kind, Position Position) : ICommand
{
    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        if (person is null || !world.IsWithinReach(person.Position, Position))
        {
            return;
        }

        var definition = world.Configuration.BuildingCatalog.Get(Kind);
        if (!person.Inventory.Remove(definition.RequiredItem, definition.RequiredAmount))
        {
            return;
        }

        world.AddBuilding(new Building
        {
            Id = world.NextBuildingId,
            Kind = Kind,
            Position = Position,
        });
    }
}
