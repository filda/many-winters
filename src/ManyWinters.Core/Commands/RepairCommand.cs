using ManyWinters.Core.Construction;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record RepairCommand(Person Person, Building Building) : ICommand
{
    private const float RepairConditionAmount = 25f;
    private const float MaxCondition = 100f;

    public void Execute(WorldState world)
    {
        if (!Person.IsAlive
            || Building.Condition >= MaxCondition
            || !world.IsWithinReach(Person.Position, Building.Position))
        {
            return;
        }

        var definition = world.Configuration.BuildingCatalog.Get(Building.Kind);
        var repairCost = Math.Max(1, definition.RequiredAmount / 4);
        if (!Person.Inventory.Remove(definition.RequiredItem, repairCost))
        {
            return;
        }

        Building.Condition = Math.Min(MaxCondition, Building.Condition + RepairConditionAmount);
    }
}
