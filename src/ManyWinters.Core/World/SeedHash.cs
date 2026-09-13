namespace ManyWinters.Core.World;

// 32-bit integer avalanche (xorshift-multiply), shared by every deterministic system that keys
// off an entity's seed (see EntityId.SeedOf). System.Random correlates badly on nearby small
// seeds - neighbouring entities would wander in sync or be tinted alike - so seeds are spread
// first. Callers mix their own inputs (a salt, a tick, another seed) down to one value before
// calling; this is only the spreading step.
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
