namespace ManyWinters.Godot.Logic;

// The largest size at which a line of text still fits the room it has.
//
// For a title that has to stay on one line however long the name inside it turns out to be: a
// band is named after its oldest member, so the same sentence is a different width every game
// and no phrasing can be short enough for all of them.
internal static class TextFit
{
    // Walks down from the largest size rather than solving for one. The measurer is a real font,
    // whose width per size is not quite proportional (hinting, kerning, an outline), and a title
    // is measured once when it goes up - a few dozen calls then cost nothing.
    //
    // Floors at minSize even when that still does not fit: a title shrunk past reading is no
    // longer a title, and a word over the edge says more than a whisper does.
    internal static int LargestThatFits(Func<int, float> widthAt, int maxSize, int minSize, float available)
    {
        for (var size = maxSize; size > minSize; size--)
        {
            if (widthAt(size) <= available)
            {
                return size;
            }
        }

        return minSize;
    }
}
