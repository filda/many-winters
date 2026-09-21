using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Putting something a person carries into a store. Takes a CarriedThing, as putting one down
// does (DropCommand): a store holds an Inventory like a pack does, so both its tiers go in.
public sealed record DepositCommand(Person Person, Entity Building, CarriedThing What) : ICommand
{
    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (!world.IsWithinReach(Person.Position, Building.Position))
        {
            return ActionBlocker.TooFar;
        }

        return What switch
        {
            CarriedThing.Stock stock when Person.Inventory.Get(stock.Kind) < stock.Amount => ActionBlocker.MissingMaterials,
            CarriedThing.Worked worked when !Person.Inventory.Assemblies.Contains(worked.Thing) => ActionBlocker.MissingMaterials,
            _ => ActionBlocker.None,
        };
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        PutAway(What);
    }

    // A store's room is uncapped, so everything offered goes in (see WithdrawCommand, where the
    // pack's capacity is what decides).
    private void PutAway(CarriedThing what)
    {
        switch (what)
        {
            case CarriedThing.Stock stock:
                Person.Inventory.Remove(stock.Kind, stock.Amount);
                Building.Storage!.Add(stock.Kind, stock.Amount);
                break;

            case CarriedThing.Worked worked:
                Person.Inventory.RemoveAssembly(worked.Thing);
                Building.Storage!.AddAssembly(worked.Thing);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(what), what, "Unknown kind of thing to put away.");
        }
    }
}
