using Godot;

namespace ManyWinters.Godot.Ui;

// Small pictures for what a person can do with whatever is picked in the workshop - Eat, Drop
// and Make - drawn in code rather than loaded off disk. Nobody has painted these yet, and a
// plain shape set in the same dark ink as the rest of the page is a better placeholder than a
// word rendered small enough to look like a toolbar.
internal static class WorkshopIcons
{
    private const int Size = 20;
    private const float Radius = (Size / 2f) - 1f;
    private static readonly Color Ink = InscriptionFont.DarkInk;

    // A button that carries one of these pictures beside its word rather than instead of it -
    // the picture is what catches the eye, the word is still what says what pressing it does.
    internal static Button Button(string label, Texture2D icon) => new()
    {
        Icon = icon,
        Text = label,
        ExpandIcon = false,
    };

    // A circle with a wedge bitten out of the right of it - the one shape a bitten apple is
    // drawn with everywhere.
    internal static ImageTexture Eat() => Draw((dx, dy) =>
    {
        if ((dx * dx) + (dy * dy) > Radius * Radius)
        {
            return false;
        }

        var angle = Mathf.RadToDeg(Mathf.Atan2(dy, dx));
        return angle is < -28f or > 28f;
    });

    // A shaft down the middle ending in a downward point, the way an arrow into a container is
    // drawn.
    internal static ImageTexture Drop() => Draw((dx, dy) =>
    {
        const float headTop = -2f;
        const float headHalfWidth = 5f;

        if (Mathf.Abs(dx) <= 1.5f && dy is >= -Radius and <= headTop)
        {
            return true;
        }

        var spread = (dy - headTop) / (Radius - headTop) * headHalfWidth;
        return dy is > headTop and <= Radius && Mathf.Abs(dx) <= headHalfWidth - spread;
    });

    // A mallet - a head across the top and a haft below it, the plainest tool silhouette there
    // is for "made by hand".
    internal static ImageTexture Make() => Draw((dx, dy) =>
    {
        const float headBottom = -1f;

        var head = Mathf.Abs(dx) <= 6f && dy is >= -Radius and <= headBottom;
        var haft = Mathf.Abs(dx) <= 1.5f && dy is > headBottom and <= Radius;
        return head || haft;
    });

    // Rasterised once from a shape asked in its own centred coordinates, (0, 0) being the
    // middle of the square - every shape above reads the same way a compass would, rather than
    // as image-space offsets from a corner.
    private static ImageTexture Draw(Func<float, float, bool> inside)
    {
        var image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (inside(x - (Size / 2f) + 0.5f, y - (Size / 2f) + 0.5f))
                {
                    image.SetPixel(x, y, Ink);
                }
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
