namespace ManyWinters.Core.Continuity;

// A title and the lines under it, ready to be carved over an ending (see Epitaph). Plain text
// with no layout of its own: what is drawn where is the presentation layer's business.
public sealed record Inscription(string Title, IReadOnlyList<string> Lines);
