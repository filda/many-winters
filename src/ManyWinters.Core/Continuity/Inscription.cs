namespace ManyWinters.Core.Continuity;

// A title, the lines under it, and the words that close it (see Epitaph, Prologue). Plain
// text: layout is the presentation layer's business. The closing words are addressed to the
// reader rather than about the band - they are what the overlay shows under the title and
// what a click on them carries out (InscriptionOverlay), and the chronicle leaves them off
// the page (ChroniclePanel). They are absent for a band with nobody left: there is nothing
// to go on for, and the screen holds the offer of a successor alone.
public sealed record Inscription(string Title, IReadOnlyList<string> Lines, string? Dismissal);
