using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;

namespace ManyWinters.Godot.Logic;

// Which picture stands for a thing somebody is carrying, for the bench where the pack is shown as
// what it is rather than as a list of names.
//
// Several candidates, best first, because the game's art is not laid out by carried thing: a few
// items were drawn their own icon, everything gathered is drawn as the resource it came off, and a
// worked thing is drawn by its shape. Which of them exists is ResourceLoader's question - nothing
// here touches the filesystem.
internal static class ItemIcons
{
    internal static IReadOnlyList<string> For(CarriedThing thing) => thing switch
    {
        CarriedThing.Stock stock => [TexturePaths.ForItem(stock.Kind.Value), TexturePaths.ForResource(stock.Kind.Value)],
        CarriedThing.Worked { Thing: Assembly.Part part } => [TexturePaths.ForForm(part.Form.Value)],
        // Two things bound together are not one shape, and nothing is drawn for a joint yet: the
        // bench shows such a thing as a blank of its own rather than as one of its halves.
        _ => [],
    };
}
