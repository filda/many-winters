namespace ManyWinters.Core.World;

// Every entity id (PersonId, ResourceNodeId, ...) is a Guid the entity draws for itself when
// constructed - nobody hands ids out, so nothing has to be counted or saved to keep them
// unique. Deterministic per-entity systems (visual variation, idle wandering, casual teaching)
// key off SeedOf.
public static class EntityId
{
    // For a creator that needs the same world twice (MapLoader's seeded map): 16 bytes off its
    // own seeded Random, which is stable for a given seed.
    public static Guid NextGuid(Random rng)
    {
        var bytes = new byte[16];
        rng.NextBytes(bytes);
        return new Guid(bytes);
    }

    // The id's first 32 bits, little-endian. Not Guid.GetHashCode(): that is an implementation
    // detail, and a seed has to survive a reload on another runtime.
    public static int SeedOf(Guid id) => BitConverter.ToInt32(id.ToByteArray(), 0);
}
