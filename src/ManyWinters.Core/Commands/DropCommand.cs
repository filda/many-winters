using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Putting down anything a person is carrying. One command for both tiers, because putting
// something down is one act: what differs is only what lands, and that is the difference
// between the tiers rather than between two verbs (see CarriedThing, Inventory).
//
// A stack lands as a pile with a count on it; a made thing lands as itself, since it has no
// count and rounding it into one is what the two tiers exist to avoid (see Entity.Made).
public sealed record DropCommand(Person Person, CarriedThing What) : ICommand
{
    // What a made thing on the ground is called as far as the map is concerned. Every one of
    // them shares it, because what a thing *is* lives in its shape (AssemblyPattern) and what it
    // is *called* lives in the band's own words (Vocabulary) - neither of which an entity kind
    // is the right place for. Art will be chosen from the shape when there is art to choose
    // (docs/sprite-pipeline-architecture.md), not from this.
    public static readonly EntityKindId MadeThingKind = new("made_thing");

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
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

        world.AddEntity(TakeFromPack());
    }

    private Entity TakeFromPack()
    {
        switch (What)
        {
            case CarriedThing.Stock stock:
                Person.Inventory.Remove(stock.Kind, stock.Amount);
                return new Entity
                {
                    Kind = new EntityKindId(stock.Kind.Value),
                    Category = EntityCategory.Pile,
                    Position = Person.Position,
                    StaticAmount = stock.Amount,
                };

            case CarriedThing.Worked worked:
                Person.Inventory.RemoveAssembly(worked.Thing);
                return new Entity
                {
                    Kind = MadeThingKind,
                    Category = EntityCategory.Pile,
                    Position = Person.Position,
                    Made = worked.Thing,
                };

            default:
                throw new ArgumentOutOfRangeException(nameof(What), What, "Unknown kind of thing to put down.");
        }
    }
}
