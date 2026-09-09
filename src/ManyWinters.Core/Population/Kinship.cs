namespace ManyWinters.Core.Population;

// Who is too closely related to have a child with. Parents and children, and siblings by
// either parent - one generation out in each direction, which is as far as the family table
// the game actually keeps can see anyway (a Person names its mother and father and nothing
// beyond them, so cousins are already invisible here).
public static class Kinship
{
    public static bool AreCloseKin(Person a, Person b) =>
        IsParentOf(a, b) || IsParentOf(b, a) || ShareAParent(a, b);

    private static bool IsParentOf(Person parent, Person child) =>
        ReferenceEquals(child.Mother, parent) || ReferenceEquals(child.Father, parent);

    // Person.Unknown is where every line that nobody remembers ends up (see its own doc
    // comment), so two people who merely both have unrecorded parents are not siblings -
    // without this the whole starting band would count as one family.
    private static bool ShareAParent(Person a, Person b) =>
        (IsRecorded(a.Mother) && (ReferenceEquals(a.Mother, b.Mother) || ReferenceEquals(a.Mother, b.Father)))
        || (IsRecorded(a.Father) && (ReferenceEquals(a.Father, b.Mother) || ReferenceEquals(a.Father, b.Father)));

    private static bool IsRecorded(Person parent) => !ReferenceEquals(parent, Person.Unknown);
}
