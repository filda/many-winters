using System.Text;

namespace ManyWinters.Core.Materials;

// What kind of thing an object is, as opposed to what it is made of: the arrangement of shapes
// alone (see docs/materials-and-crafting-architecture.md section 8).
//
// Material is deliberately left out. An axe is an axe whether its head is stone or flint, and
// that is how words work - the substance stays an adjective in front of the name rather than
// part of it. So the first band to lash a wedge to a shaft coins one word, and every later one
// of those is that word for free, including ones made of substances nobody had then heard of.
//
// The signature is a string rather than a type of its own because the only thing anyone does
// with it is look a name up by it (see Vocabulary), and it has to survive a save unchanged.
public static class AssemblyPattern
{
    public static string SignatureOf(Assembly assembly)
    {
        var signature = new StringBuilder();
        Write(assembly, signature);

        return signature.ToString();
    }

    private static void Write(Assembly assembly, StringBuilder into)
    {
        switch (assembly)
        {
            case Assembly.Part part:
                into.Append(part.Form.Value);
                return;

            case Assembly.Joined joined:
                // Sorted, so lashing a head to a haft and a haft to a head are the same kind of
                // thing and do not earn the band two words for it.
                var left = SignatureOf(joined.Left);
                var right = SignatureOf(joined.Right);
                var first = string.CompareOrdinal(left, right) <= 0 ? left : right;
                var second = ReferenceEquals(first, left) ? right : left;

                into.Append('(').Append(first).Append('+').Append(second).Append(')');
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(assembly), assembly, "Unknown kind of worked thing.");
        }
    }
}
