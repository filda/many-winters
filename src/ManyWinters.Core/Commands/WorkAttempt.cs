using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Whether a directed attempt at working something comes off, and how good it is if it does.
//
// A sound idea is never refused for want of skill - a beginner who reaches for the right
// material can get a cord out of it, they will just spoil more of it getting there (see
// docs/materials-and-crafting-architecture.md section 7, "Skill level as a success modifier,
// not a gate"). Whether the idea is sound at all is a property question the commands ask
// separately; this only says whether the hands managed it.
//
// Both numbers walk the same road from novice to practised, because they are the same fact
// about a person seen twice: the hand that fails less also turns out better work.
public static class WorkAttempt
{
    // What a beginner manages: succeeds one time in five, and what they do turn out is poor.
    private const float NoviceShare = 0.2f;
    private const int PracticesForMastery = 50;

    private static readonly float MasteryLevel = Skills.LevelAfter(PracticesForMastery);

    // Working a thing is how somebody comes to know it, whether or not the attempt came off:
    // they had it in their hands and saw what it did, and a spoiled try says as much as a good
    // one (see Beliefs, SimulationRules.UnderstandingFromWorkingIt). This is the *reach* a
    // player buys by directing an attempt - somebody can be sent to try a substance the whole
    // band understands nothing about, and they come back understanding it.
    public static void TeachesWhatItIs(WorldState world, Person person, MaterialId material)
    {
        if (world.Configuration.MaterialCatalog.Find(material) is { } actual)
        {
            person.Beliefs.LearnAll(actual, world.Configuration.Rules.UnderstandingFromWorkingIt);
        }
    }

    public static float ChanceFor(Person person, SkillTypeId skill) => Practised(person, skill);

    public static float QualityFor(Person person, SkillTypeId skill) => Practised(person, skill);

    // Deterministic from the person's seed, the verb and the tick - never a shared Random, as
    // every other roll in the game is (see WorldState.PassesCasualTeachingRoll). The tick is in
    // the mix so a second try is a second roll, not the same one again; that's also why an
    // attempt costs time (see SimulationRules.TicksPerWorkAttempt), or a player could stand at a
    // held clock and press until it worked.
    public static bool Succeeds(Person person, SkillTypeId skill, TechniqueId verb, long tick)
    {
        var mixed = unchecked((uint)(person.Id.Seed * 73856093) ^ (uint)(StableStringHash(verb.Value) * 19349663) ^ ((uint)tick * 2654435761u));

        // Stryker disable once Equality: NextDouble() returning exactly the chance has
        // probability zero, so < and <= are the same roll
        return new Random(SeedHash.Avalanche(mixed)).NextDouble() < ChanceFor(person, skill);
    }

    private static float Practised(Person person, SkillTypeId skill)
    {
        var mastery = Math.Clamp(person.Skills.Get(skill) / MasteryLevel, 0f, 1f);

        return NoviceShare + ((1f - NoviceShare) * mastery);
    }

    // Not string.GetHashCode(): .NET randomizes it per process, and this roll must be stable
    // across runs (same reasoning as WorldState.StableStringHash).
    private static int StableStringHash(string value)
    {
        var hash = 17;
        foreach (var character in value)
        {
            hash = unchecked((hash * 31) + character);
        }

        return hash;
    }
}
