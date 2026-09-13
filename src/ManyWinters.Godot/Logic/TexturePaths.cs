using ManyWinters.Core.Construction;

namespace ManyWinters.Godot.Logic;

// Building res:// paths of content assets by name alone. Nothing here touches the filesystem;
// whether a path exists is ResourceLoader's job, with the caller that can afford to ask.
internal static class TexturePaths
{
    internal static string ForBuilding(BuildingKindId kind)
        => $"res://Content/buildings/{kind.Value}/{kind.Value}.png";

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
