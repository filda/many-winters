namespace ManyWinters.Core.Population;

// Shared with MapLoader's starting crowd so a person spawned later (Main's "Spawn Person"
// button) is named like one who was there from the start.
public static class PersonNames
{
    public static readonly string[] Pool =
    [
        "Ava", "Bran", "Tora", "Kael", "Mira", "Doran", "Liska", "Faro",
        "Ivy", "Rask", "Sela", "Bodin", "Yara", "Corin", "Vessa",
    ];

    // Names for a successor band arriving after the first one dies out. Disjoint from Pool so the
    // old band can be distinguished from the new one on a grave or in the roster.
    public static readonly string[] AlternativePool =
    [
        "Tove", "Harald", "Sigrun", "Bjorn", "Elsa", "Runar", "Hildur", "Sigurd",
        "Ingrid", "Torsten", "Freya", "Gunnar", "Astrid", "Sven", "Ylva",
    ];

    // Names for the starting crowd's dead parents (WorldState.Forebears), disjoint from Pool so
    // "child of Orla and Hesk" on a grave can't be mistaken for a couple still walking around.
    public static readonly string[] Forebears =
    [
        "Orla", "Hesk", "Maren", "Tovin", "Enna", "Garrod", "Wyn", "Brannoc", "Ilse",
        "Rurik", "Saoirse", "Kellan", "Nessa", "Aldric", "Freya", "Osric", "Tamsin", "Wulf",
    ];
}
