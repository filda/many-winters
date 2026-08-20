using ManyWinters.Core.Construction;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record RepairCommand(PersonId PersonId, BuildingId BuildingId) : ICommand
{
    private const float RepairConditionAmount = 25f;
    private const float MaxCondition = 100f;

    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        var building = world.Buildings.FirstOrDefault(b => b.Id == BuildingId);
        if (person is null
            || building is null
            || building.Condition >= MaxCondition
            || WorldState.Distance(person.Position, building.Position) > WorldState.MaxInteractionDistance)
        {
            return;
        }

        var definition = world.BuildingCatalog.Get(building.Kind);
        var repairCost = Math.Max(1, definition.RequiredAmount / 4);
        if (!person.Inventory.Remove(definition.RequiredItem, repairCost))
        {
            return;
        }

        building.Condition = Math.Min(MaxCondition, building.Condition + RepairConditionAmount);
    }
}
