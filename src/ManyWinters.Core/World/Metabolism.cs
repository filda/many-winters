using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;

namespace ManyWinters.Core.World;

public static class Metabolism
{
    // One creature's hunger, eating and death for the tick - called from inside the creature
    // loop in WorldState.Advance so creatures are still processed one at a time in that same
    // order, rather than as a separate pass that would reorder a death relative to other
    // creatures' actions this tick.
    public static void Advance(WorldState world, Creature creature, long currentTick, float baseHungerMultiplier)
    {
        var rules = world.Configuration.Rules;
        var itemCatalog = world.Configuration.ItemCatalog;

        // An infant at its mother's side is fed and not hungry; the cost lands on her
        // as NursingHungerMultiplier below. Once she dies or leaves it behind, the
        // countdown is real.
        if (world.IsBeingNursed(creature))
        {
            creature.Needs.Hunger = 0f;
        }
        else
        {
            var insulation = creature.Inventory.Counts.Keys.Sum(kind => itemCatalog.InsulationFor(kind));
            var hungerMultiplier = Math.Max(1f, baseHungerMultiplier - insulation);
            if (world.NursingInfantOf(creature) is not null)
            {
                hungerMultiplier *= rules.NursingHungerMultiplier;
            }

            // The species' own winter reserve - 1 for a human, so this changes nothing
            // about a person.
            var speciesHungerMultiplier = world.Configuration.SpeciesCatalog.Get(creature.Species).HungerPerTickMultiplier;

            creature.Needs.Hunger = Math.Min(creature.Needs.Hunger + (rules.HungerPerTick * hungerMultiplier * speciesHungerMultiplier), creature.MaxHunger);
        }

        TryAutoEat(world, creature);

        var diedOfOldAge = world.AgeInYearsAt(creature, currentTick) >= world.LifeCycleOf(creature).MaxLifespanYears;
        // Their own MaxHunger, not the rules'.
        if (creature.Needs.Hunger >= creature.MaxHunger || diedOfOldAge)
        {
            creature.IsAlive = false;
            creature.DeathTick = currentTick;
            creature.CauseOfDeath = diedOfOldAge ? DeathCause.OldAge : DeathCause.Hunger;
            world.FillCarcass(creature);
        }
    }

    // Same behaviour as the player's Eat button: eats through whatever food is on hand until no
    // longer hungry. Runs every tick whatever task is active, even a player-issued one - a
    // starving creature should not wait for a free moment to eat from their own pack.
    private static void TryAutoEat(WorldState world, Creature creature)
    {
        // A meal, not a nibble: nothing until hunger has built up, then EatCommand eats to zero.
        if (!world.IsHungryEnoughToEat(creature))
        {
            return;
        }

        foreach (var kind in creature.Inventory.Counts.Keys.ToList())
        {
            // Stryker disable once Equality,Statement,Block: EatCommand no-ops at zero hunger anyway, so this only saves the remaining calls
            if (creature.Needs.Hunger <= 0f)
            {
                break;
            }

            new EatCommand(creature, kind).Execute(world);
        }
    }
}
