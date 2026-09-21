using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// Building res:// paths of content assets by name alone. Nothing here touches the filesystem;
// whether a path exists is ResourceLoader's job, with the caller that can afford to ask.
internal static class TexturePaths
{
    internal static string ForBuilding(EntityKindId kind)
        => $"res://Content/buildings/{kind.Value}/{kind.Value}.png";

    // A thing's own icon, drawn for the item itself (warm_clothing).
    internal static string ForItem(string kind) => $"res://Content/items/{kind}/{kind}.png";

    // What a gathered material is drawn with: the ground icon of the resource it comes off
    // (wood, pear), which is the same picture lying down rather than growing.
    internal static string ForResource(string kind) => $"res://Content/resources/{kind}/{kind}.png";

    // A worked thing is its shape before it is its substance: a stick is stick-shaped whatever
    // wood it was cut from.
    internal static string ForForm(string form) => $"res://Content/forms/{form}/{form}.png";

    // Splits at the last dot, so a directory containing one is not mistaken for the extension.
    internal static string InsertBeforeExtension(string path, string suffix)
    {
        var dot = path.LastIndexOf('.');
        return path[..dot] + suffix + path[dot..];
    }

    // Variant 0 is the original asset and carries no suffix; later variants take one before the
    // extension (apple_tree_trunk_v1.png).
    internal static string VariantSuffixed(string path, int variant) =>
        variant == 0 ? path : InsertBeforeExtension(path, $"_v{variant}");
}
