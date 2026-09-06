namespace ManyWinters.Core.World;

// Every entity id (PersonId, ResourceNodeId, ...) is a Guid an entity draws for itself the
// moment it's constructed - nobody hands ids out, so nothing has to be asked, counted or
// saved to keep them unique. The deterministic systems that used to key off small
// sequential ids (visual variation, idle wandering, casual teaching) run on SeedOf instead.
public static class EntityId
{
    // For a creator that needs the same world twice (MapLoader's seeded starting map): 16
    // bytes off its own seeded Random rather than the system's. Random.NextBytes is stable
    // for a given seed, so the same generator in the same order yields the same ids.
    public static Guid NextGuid(Random rng)
    {
        var bytes = new byte[16];
        rng.NextBytes(bytes);
        return new Guid(bytes);
    }

    // The id's first 32 bits, little-endian - the int the id-seeded systems consume. Not
    // Guid.GetHashCode(): that's documented as an implementation detail, and a seed has to
    // stay the same across runtimes for a saved world to look the same when reloaded.
    public static int SeedOf(Guid id) => BitConverter.ToInt32(id.ToByteArray(), 0);
}
