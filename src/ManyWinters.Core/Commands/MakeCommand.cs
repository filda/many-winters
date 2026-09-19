using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Replaces the formerly separate CraftCommand and ConstructCommand: both were "check materials,
// remove them, produce the output," differing only in where the output landed. That difference
// is now derived from weight rather than authored by which command was called (see
// docs/materials-and-crafting-architecture.md section 5) - a light output goes into the maker's
// pack, a heavy one (more than fits under WorldState.MaxCarryWeightFor) comes into existence in
// the world instead. Position is only consulted for that heavy case; when it is omitted the
// output is placed wherever the maker is standing.
public sealed record MakeCommand(Person Person, ItemKindId Output, Position? Position = null) : ICommand
{
    private const float StartingCondition = 100f;

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        var recipe = world.Configuration.RecipeCatalog.Get(Output);
        if (Person.Inventory.Get(recipe.InputItem) < recipe.InputAmount)
        {
            return ActionBlocker.MissingMaterials;
        }

        if (!FitsInInventory(world) && !world.IsWithinReach(Person.Position, TargetPosition))
        {
            return ActionBlocker.TooFar;
        }

        return ActionBlocker.None;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        var recipe = world.Configuration.RecipeCatalog.Get(Output);
        Person.Inventory.Remove(recipe.InputItem, recipe.InputAmount);

        if (FitsInInventory(world))
        {
            Person.Inventory.Add(Output, 1);
            return;
        }

        // Only one recipe today (storage_hut) ever lands here, so EntityCategory.Building is
        // hardcoded rather than authored per recipe; the first placeable-but-not-building output
        // (a canoe, say) needs this to become a real choice.
        world.AddEntity(new Entity
        {
            Kind = new EntityKindId(Output.Value),
            Category = EntityCategory.Building,
            Position = TargetPosition,
            Condition = StartingCondition,
            Storage = new Inventory(),
        });
    }

    // Wherever the maker is standing, unless a specific spot was asked for - so "make an axe"
    // from the person's own card and "build a storage hut here" from a ground click both go
    // through this one command.
    private Position TargetPosition => Position ?? Person.Position;

    private bool FitsInInventory(WorldState world) =>
        Person.Inventory.HasRoomFor(Output, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Person));
}
