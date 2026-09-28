using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Gathering food no longer relieves hunger directly - it only fills the gatherer's inventory, so
// something has to spend it back down again. Eats just enough of the given food item to reach zero hunger,
// or all of it if there isn't that much - not a fixed amount, since a UI "Eat" action shouldn't need
// the caller to first work out how much hunger is left to satisfy.
public sealed record EatCommand(Creature Actor, ItemKindId FoodItem) : ICommand
{
    // A person who never learned even this can be holding a full inventory of food and still
    // starve - eating (like gathering) has to be taught, not assumed.
    public static readonly SkillTypeId Skill = new("eating");

    // Why this meal cannot happen, over `availableUnits` of `food` from wherever they come:
    // EatCommand passes what the eater carries, GatherCommand what the node would give up.
    //
    // Knowledge is asked last on purpose: a hungry creature with an empty pack is told the pack
    // is empty, not that they never learned to eat, and the player's menu can forgive NotLearned
    // without that hiding a second reason underneath.
    public static ActionBlocker EatingBlocker(WorldState world, Creature actor, ItemKindId food, int availableUnits)
    {
        if (!actor.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        // This is what keeps the division in Eat from being by zero - an item nobody described,
        // or one this creature's species cannot digest, restores nothing at all.
        // Stryker disable once Equality: with < instead, a zero rate divides to infinity, which
        // converts to a negative unit count that the caller then refuses anyway - the same
        // answer by a worse route, and not one worth writing a test around
        if (world.HungerRestoredPerUnitFor(actor, food) <= 0f)
        {
            return ActionBlocker.NotEdible;
        }

        // Nothing to put right. The player's Eat button deliberately stops here rather than at
        // the check idle decision-making uses to decide someone is hungry enough to eat on their
        // own: being told to eat is not the same as deciding to.
        if (actor.Needs.Hunger <= 0f)
        {
            return ActionBlocker.NotHungry;
        }

        if (availableUnits <= 0)
        {
            return ActionBlocker.MissingMaterials;
        }

        // Find, not Get - a caller with no "eating" skill registered at all (a minimal test
        // world) just means this can never succeed, not a crash.
        return world.Configuration.SkillCatalog.Find(Skill) is { } skillDefinition
            && actor.KnownTechniques.Contains(skillDefinition.BaseTechnique)
            ? ActionBlocker.None
            : ActionBlocker.NotLearned;
    }

    // The act of eating, apart from where the food comes from: EatCommand feeds from
    // the inventory, GatherCommand straight from the source being picked ("into the mouth"),
    // and both have to gate, satisfy and train identically. Eats just enough of the
    // `availableUnits` on offer to reach zero hunger (or all of them if that isn't enough) and
    // returns how many, leaving the caller to take exactly that many from wherever they were.
    public static int Eat(WorldState world, Creature actor, ItemKindId food, int availableUnits)
    {
        if (EatingBlocker(world, actor, food, availableUnits) is not ActionBlocker.None)
        {
            return 0;
        }

        var skillDefinition = world.Configuration.SkillCatalog.Get(Skill);
        var restoredPerUnit = world.HungerRestoredPerUnitFor(actor, food);
        if (actor.KnownTechniques.Contains(skillDefinition.EfficientTechnique))
        {
            restoredPerUnit *= world.Configuration.Rules.EfficientHungerRestoredMultiplier;
        }

        // At least one: EatingBlocker has already refused both a creature with no hunger to put
        // right and a caller offering nothing to eat.
        var unitsNeeded = (int)MathF.Ceiling(actor.Needs.Hunger / restoredPerUnit);
        var unitsEaten = Math.Min(availableUnits, unitsNeeded);

        actor.Needs.Hunger = Math.Max(0f, actor.Needs.Hunger - (unitsEaten * restoredPerUnit));

        actor.Skills.Increase(Skill, world.Configuration.Rules.SkillGainPerMeal);
        if (actor.Skills.Get(Skill) >= Skills.LevelAfter(world.Configuration.Rules.PracticesBeforeDiscovery))
        {
            actor.KnownTechniques.Add(skillDefinition.EfficientTechnique);
        }

        return unitsEaten;
    }

    public ActionBlocker Blocker(WorldState world) =>
        EatingBlocker(world, Actor, FoodItem, Actor.Inventory.Get(FoodItem));

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        // Unguarded: removing zero units leaves the count exactly as it was, so there is
        // nothing for a "did we actually eat" check to save.
        Actor.Inventory.Remove(FoodItem, Eat(world, Actor, FoodItem, Actor.Inventory.Get(FoodItem)));
    }
}
