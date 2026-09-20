using ManyWinters.Core.Materials;

namespace ManyWinters.Core.Continuity;

// The words a band has for the things it makes.
//
// Nothing is named in advance. A configuration nobody has a word for is described by what it is
// made of ("lashed stone wedge and wood stick"); once the band has named it, that is what it is
// called, and every later thing of the same shape is called that too - which is what makes
// naming a moment worth having rather than a label (see
// docs/materials-and-crafting-architecture.md section 8).
//
// Held by the world rather than by each person, because there is one band. When there are
// several, a word will belong to whoever knows it, and words will then be teachable, forgettable
// and open to being garbled like anything else somebody believes (docs/todo/todo.md, "vznik
// jazyka"). The shape here does not stand in the way of that: it is a lookup from a pattern to a
// word, and whose word it is can be added around it.
public sealed class Vocabulary
{
    private readonly Dictionary<string, string> _words = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Words => _words;

    public bool HasAWordFor(Assembly assembly) => _words.ContainsKey(AssemblyPattern.SignatureOf(assembly));

    public string? WordFor(Assembly assembly) =>
        _words.GetValueOrDefault(AssemblyPattern.SignatureOf(assembly));

    // Naming a thing that already has a name renames it: the band changed its mind, which is
    // theirs to do.
    public void Name(Assembly assembly, string word) => _words[AssemblyPattern.SignatureOf(assembly)] = word;

    public void Restore(string signature, string word) => _words[signature] = word;
}
