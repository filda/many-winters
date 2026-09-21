namespace ManyWinters.Core.Continuity;

// A title, the lines under it, and the words that close it. Plain text: layout is the
// presentation layer's business. The closing words address the reader rather than the band -
// an overlay shows them under the title and a click on them carries them out, while the
// chronicle leaves them off the page. They are null for a band with nobody left: only the
// offer of a successor remains.
public sealed record Inscription(string Title, IReadOnlyList<string> Lines, string? Dismissal);
