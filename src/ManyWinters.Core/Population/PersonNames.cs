namespace ManyWinters.Core.Population;

// Shared with MapLoader's starting crowd so a person spawned later (Main's "Spawn Person"
// button) reads the same as one who was there from the beginning, instead of a placeholder
// like "Person 4".
public static class PersonNames
{
    public static readonly string[] Pool =
    [
        "Ava", "Bran", "Tora", "Kael", "Mira", "Doran", "Liska", "Faro",
        "Ivy", "Rask", "Sela", "Bodin", "Yara", "Corin", "Vessa",
    ];

    // Names for the starting crowd's dead parents (see MapLoader and WorldState.Forebears) -
    // deliberately disjoint from Pool, so "child of Orla and Hesk" on a grave can never be
    // mistaken for a couple still walking around camp.
    public static readonly string[] Forebears =
    [
        "Orla", "Hesk", "Maren", "Tovin", "Enna", "Garrod", "Wyn", "Brannoc", "Ilse",
        "Rurik", "Saoirse", "Kellan", "Nessa", "Aldric", "Freya", "Osric", "Tamsin", "Wulf",
    ];
}
