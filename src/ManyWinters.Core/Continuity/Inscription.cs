namespace ManyWinters.Core.Continuity;

// A title and the lines under it (see Epitaph, Prologue). Plain text: layout is the
// presentation layer's business.
public sealed record Inscription(string Title, IReadOnlyList<string> Lines);
