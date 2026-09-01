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
}
