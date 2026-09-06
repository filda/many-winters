namespace ManyWinters.Core.World;

// Thomas Wang's 32-bit integer avalanche, shared by every deterministic system that keys off
// an entity's own seed (see EntityId.SeedOf): idle wandering, casual teaching, and the Godot
// layer's per-instance visual variation. System.Random's legacy algorithm correlates badly on
// nearby small seeds, so two entities whose seeds happen to sit close together would draw
// eerily similar first values - reading as synchronized wandering, or as two neighbouring
// trees tinted identically. This spreads them apart first while staying a pure function of
// the seed, so the same entity looks and behaves the same on every reload.
//
// Callers mix their own inputs down to one value first (a salt, a tick, another entity's
// seed) and pass the result here; what they mix in is their business, this is only the
// spreading step.
public static class SeedHash
{
    public static int Avalanche(uint value)
    {
        // Stryker disable once Bitwise: value is uint, so >> and >>> are the same operation
        value = unchecked((value ^ (value >> 16)) * 0x45d9f3bu);

        // Stryker disable once Bitwise: as above
        value = unchecked((value ^ (value >> 16)) * 0x45d9f3bu);

        // Stryker disable once Bitwise: as above
        value ^= value >> 16;

        return unchecked((int)value);
    }
}
