using ManyWinters.Core.Continuity;

namespace ManyWinters.Tests.TestSupport;

// What a written inscription has to read like, whoever wrote it (Prologue, Epitaph). Shared,
// because both are drawn from lists of wordings (PhraseDraw) and both are checked the same way:
// over many seeds, so every variant of every sentence is looked at at least once.
public static class InscriptionAssertions
{
    // The title as a heading, every line under it as a sentence, and the closing words as the
    // one piece that is neither: an imperative to the reader, carved without a full stop.
    public static void AssertReadsAsAnInscription(Inscription inscription)
    {
        AssertReadsAsATitle(inscription.Title);
        Assert.All(inscription.Lines, AssertReadsAsASentence);
        AssertReadsCleanly(inscription.Dismissal);
    }

    // The title is the one piece of an inscription that is not a sentence: it names what the
    // lines are about and is carved without a full stop.
    private static void AssertReadsAsATitle(string title)
    {
        AssertReadsCleanly(title);
        Assert.False(title.EndsWith('.'), $"A title takes no full stop: '{title}'");
    }

    // Every carved line has to read as a sentence.
    private static void AssertReadsAsASentence(string line)
    {
        AssertReadsCleanly(line);
        Assert.EndsWith(".", line);
    }

    // The title and the lines under it, for what has to hold of both.
    // Everything an inscription says, in the order it is said: the closing words too, since
    // they are spoken over the band like the rest (see AWomanIsSpokenOfAsShe).
    public static IEnumerable<string> AllText(Inscription inscription) =>
        inscription.Lines.Prepend(inscription.Title).Append(inscription.Dismissal);

    // An empty phrase slotted into a template leaves a double space or a dangling comma, so this
    // also catches a variant that says nothing.
    private static void AssertReadsCleanly(string line)
    {
        Assert.False(string.IsNullOrWhiteSpace(line));
        Assert.True(char.IsUpper(line[0]), $"Does not start with a capital: '{line}'");
        Assert.DoesNotContain("  ", line);
        Assert.DoesNotContain(" .", line);
        Assert.DoesNotContain(" ,", line);
        Assert.DoesNotContain(" ;", line);
        Assert.DoesNotContain(",.", line);
    }
}
