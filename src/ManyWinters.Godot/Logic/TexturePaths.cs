using ManyWinters.Core.Construction;

namespace ManyWinters.Godot.Logic;

// Building the res:// paths of content assets by name alone. Nothing here touches the
// filesystem - deciding whether a path actually exists is ResourceLoader's job and belongs
// with the caller that can afford to ask.
internal static class TexturePaths
{
    internal static string ForBuilding(BuildingKindId kind)
        => $"res://Content/buildings/{kind.Value}/{kind.Value}.png";

    // Splits at the last dot, so a directory containing one doesn't get mistaken for the
    // extension.
    internal static string InsertBeforeExtension(string path, string suffix)
    {
        var dot = path.LastIndexOf('.');
        return path[..dot] + suffix + path[dot..];
    }

    // Variant 0 is the original hand-authored asset, which carries no suffix at all - only
    // later shape variants get one, and it goes before the extension (apple_tree_trunk_v1.png)
    // or the file simply won't be found.
    internal static string VariantSuffixed(string path, int variant) =>
        variant == 0 ? path : InsertBeforeExtension(path, $"_v{variant}");
}
