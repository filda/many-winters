using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record RepairCommand(Person Person, Entity Building) : ICommand
{
    private const float RepairConditionAmount = 25f;
    private const float MaxCondition = 100f;

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (Building.Condition is null or >= MaxCondition)
        {
            return ActionBlocker.NothingToRepair;
        }

        if (!world.IsWithinReach(Person.Position, Building.Position))
        {
            return ActionBlocker.TooFar;
        }

        return Person.Inventory.Get(CostItem(world)) < RepairCost(world)
            ? ActionBlocker.MissingMaterials
            : ActionBlocker.None;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        Person.Inventory.Remove(CostItem(world), RepairCost(world));
        Building.Condition = Math.Min(MaxCondition, Building.Condition!.Value + RepairConditionAmount);
    }

    private ItemKindId CostItem(WorldState world) => Recipe(world).InputItem;

    // A quarter of what the building cost to put up, and never free.
    private int RepairCost(WorldState world) => Math.Max(1, Recipe(world).InputAmount / 4);

    private RecipeDefinition Recipe(WorldState world) =>
        world.Configuration.RecipeCatalog.Get(new ItemKindId(Building.Kind.Value));
}
