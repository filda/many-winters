namespace ManyWinters.Core.Population;

// Who is too closely related to have a child with: parents, children, and siblings by either
// parent. One generation out is as far as a Person's family table sees, so cousins are invisible.
public static class Kinship
{
    public static bool AreCloseKin(Person a, Person b) =>
        IsParentOf(a, b) || IsParentOf(b, a) || ShareAParent(a, b);

    private static bool IsParentOf(Person parent, Person child) =>
        ReferenceEquals(child.Mother, parent) || ReferenceEquals(child.Father, parent);

    // Person.Unknown is where every unremembered line ends, so two people who merely both have
    // unrecorded parents are not siblings - otherwise the whole starting band would be one family.
    private static bool ShareAParent(Person a, Person b) =>
        (IsRecorded(a.Mother) && (ReferenceEquals(a.Mother, b.Mother) || ReferenceEquals(a.Mother, b.Father)))
        || (IsRecorded(a.Father) && (ReferenceEquals(a.Father, b.Mother) || ReferenceEquals(a.Father, b.Father)));

    private static bool IsRecorded(Person parent) => !ReferenceEquals(parent, Person.Unknown);
}
