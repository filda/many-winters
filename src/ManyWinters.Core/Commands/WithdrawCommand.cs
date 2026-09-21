using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Taking something back out of a store. The other half of DepositCommand, and it names what is
// in the store the same way: both tiers, one type (see CarriedThing).
public sealed record WithdrawCommand(Person Person, Entity Building, CarriedThing What) : ICommand
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

        // The store's shortage, not the person's - the two read differently to a player standing
        // at an empty hut (see ActionBlocker.MissingMaterials).
        return What switch
        {
            CarriedThing.Stock stock when Building.Storage!.Get(stock.Kind) < stock.Amount => ActionBlocker.StoreIsEmpty,
            CarriedThing.Worked worked when !Building.Storage!.Assemblies.Contains(worked.Thing) => ActionBlocker.StoreIsEmpty,
            _ => ActionBlocker.None,
        };
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        Fetch(What, world);
    }

    // Unlike Deposit into a building's uncapped storage, this goes into the person's capped
    // inventory (see WorldState.MaxCarryWeightFor).
    private void Fetch(CarriedThing what, WorldState world)
    {
        var items = world.Configuration.ItemCatalog;
        var room = world.MaxCarryWeightFor(Person);

        switch (what)
        {
            case CarriedThing.Stock stock:
                // What does not fit stays on the shelf.
                var added = Person.Inventory.AddUpToCapacity(stock.Kind, stock.Amount, items, room);
                Building.Storage!.Remove(stock.Kind, added);
                break;

            case CarriedThing.Worked worked:
                // Whole or not at all, as off a body or off the ground: half an axe is nothing.
                if (Person.Inventory.AddAssemblyIfItFits(worked.Thing, items, room))
                {
                    Building.Storage!.RemoveAssembly(worked.Thing);
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(what), what, "Unknown kind of thing to fetch.");
        }
    }
}
