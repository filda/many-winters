using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Knowledge;

// What people standing together say to each other about the stuff of the world. Talk, not
// instruction: nobody needs to know how to teach to mention that a stone shatters, which is why
// this does not go through TeachCommand and why understanding can spread before anyone has
// learned to teach at all.
//
// What is passed on is only what the teller would act on themselves, and it lands as hearsay -
// held less firmly than what the listener could have found out by handling it. So one mention is
// talk and two are conviction, and somebody who then handles the stuff settles the matter for
// themselves. Today the account passed on is always true; the listener's only doubt is how often
// they have heard it.
public static class Hearsay
{
    public static void Advance(WorldState world, long currentTick)
    {
        var rules = world.Configuration.Rules;

        foreach (var teller in world.People)
        {
            if (!teller.IsAlive)
            {
                continue;
            }

            foreach (var listener in world.People)
            {
                if (ReferenceEquals(listener, teller)
                    || !listener.IsAlive
                    || !world.IsWithinReach(teller.Position, listener.Position))
                {
                    continue;
                }

                MentionSomething(teller, listener, rules, currentTick);
            }
        }
    }

    // One thing per pair per tick, as casual teaching passes at most one technique: a
    // conversation, not a lecture.
    private static void MentionSomething(Person teller, Person listener, SimulationRules rules, long currentTick)
    {
        // Ordered, because a dictionary's own order is nobody's promise and this has to replay
        // the same way twice.
        foreach (var held in teller.Beliefs.Held.OrderBy(entry => entry.Key.Material.Value, StringComparer.Ordinal).ThenBy(entry => entry.Key.Property))
        {
            var (material, property) = held.Key;

            // People repeat what they were told, not only what they have proved for themselves -
            // which is what lets a tale travel A to B to C, gathering error at every hop (see
            // docs/knowledge-transmission-architecture.md section 1). A vague inkling from
            // having once brushed past the stuff is still beneath saying out loud, so the bar
            // is what a telling itself is worth.
            if (teller.Beliefs.ConfidenceIn(material, property) < rules.HearsayConfidence
                || listener.Beliefs.ConfidenceIn(material, property) >= teller.Beliefs.ConfidenceIn(material, property))
            {
                continue;
            }

            if (!PassesBeliefSharingRoll(teller, listener, material, property, currentTick, rules.BeliefSharingChancePerTick))
            {
                continue;
            }

            var asTold = Distorted(held.Value.Value, teller, listener, material, property, currentTick, rules);
            listener.Beliefs.Learn(material, property, asTold, rules.HearsayConfidence);
            return;
        }
    }

    // What the listener takes away, which is not quite what was said. Nobody tells it better
    // than they know it, so the error is laid on top of whatever the teller already believed -
    // that is what makes it accumulate along a chain rather than being one lossy event (see
    // docs/knowledge-transmission-architecture.md section 1).
    //
    // A practised teacher narrows it to nothing, which is the lever the player has: train
    // somebody to teach, or send people to find things out first-hand. Nothing here marks a
    // belief as wrong, and nobody holding one can tell (section 6) - two people simply come to
    // disagree, and reality settles it when somebody next works the stuff.
    private static float Distorted(float told, Person teller, Person listener, MaterialId material, MaterialProperty property, long currentTick, SimulationRules rules)
    {
        var fidelity = WorkAttempt.Practised(teller, TeachCommand.TeachingSkill);
        var reach = rules.HearsayDistortion * (1f - fidelity);
        if (reach <= 0f)
        {
            return told;
        }

        var mixed = unchecked((uint)(teller.Id.Seed * 2654435761u)
                              ^ (uint)(listener.Id.Seed * 40503)
                              ^ (uint)(SeedHash.StableStringHash(material.Value) * 19349663)
                              ^ (uint)((int)property * 83492791)
                              ^ ((uint)currentTick * 73856093u));

        var drift = ((float)new Random(SeedHash.Avalanche(mixed)).NextDouble() * 2f) - 1f;

        // Never below nothing: a substance cannot be less than not fibrous at all. No ceiling,
        // because density is not on a 0-1 scale and a tall tale about how heavy stone is should
        // be tellable.
        return Math.Max(0f, told + (drift * reach));
    }

    // Deterministic from the pair, what is being said and the tick, as every other roll is.
    private static bool PassesBeliefSharingRoll(Person teller, Person listener, MaterialId material, MaterialProperty property, long currentTick, float chance)
    {
        var mixed = unchecked((uint)(teller.Id.Seed * 73856093)
                              ^ (uint)(listener.Id.Seed * 19349663)
                              ^ (uint)(SeedHash.StableStringHash(material.Value) * 83492791)
                              ^ (uint)((int)property * 2654435761u)
                              ^ ((uint)currentTick * 40503u));

        // Stryker disable once Equality: NextDouble() returning exactly the chance has
        // probability zero, so < and <= are the same roll
        return new Random(SeedHash.Avalanche(mixed)).NextDouble() < chance;
    }
}
