using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Gathering food no longer relieves hunger directly (see GatherCommand) - it only fills the
// gatherer's inventory, so something has to spend it back down again. Eats just enough of the
// given food item to reach zero hunger, or all of it if there isn't that much - not a fixed
// amount, since a UI "Eat" action shouldn't need the caller to first work out how much hunger
// is left to satisfy.
public sealed record EatCommand(Person Person, ItemKindId FoodItem) : ICommand
{
    // A person who never learned even this can be holding a full inventory of food and still
    // starve - eating (like gathering) has to be taught, not assumed (see
    // SkillDefinition.BaseTechnique).
    public static readonly SkillTypeId Skill = new("eating");

    // A better cook/eater gets more out of the same food rather than eating faster or needing
    // less of it - simplest bonus that still gives EfficientTechnique a real effect, same
    // pattern as a tool's ToolHarvestBonus for gathering.
    private const float EfficientHungerRestoredMultiplier = 1.2f;

    private const float SkillGainPerMeal = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // The practice curve is not linear any more (see Skills.Increase), so the threshold is
    // stated as the number of tries it stands for rather than as a level.
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    public ActionBlocker Blocker(WorldState world) =>
        EatingBlocker(world, Person, FoodItem, Person.Inventory.Get(FoodItem));

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        // Unguarded: removing zero units leaves the count exactly as it was, so there is
        // nothing for an "did we actually eat" check to save.
        Person.Inventory.Remove(FoodItem, Eat(world, Person, FoodItem, Person.Inventory.Get(FoodItem)));
    }

    // Why this meal cannot happen, over `availableUnits` of `food` from wherever they come:
    // EatCommand passes what the person carries, GatherCommand what the node would give up.
    //
    // Knowledge is asked last on purpose (see ActionBlocker.NotLearned): a hungry person with an
    // empty pack is told the pack is empty, not that they never learned to eat, and the player's
    // menu can forgive NotLearned without that hiding a second reason underneath.
    public static ActionBlocker EatingBlocker(WorldState world, Person person, ItemKindId food, int availableUnits)
    {
        if (!person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        // This is what keeps the division in Eat from being by zero - an item nobody described
        // restores nothing at all (see ItemCatalog.HungerRestoredPerUnitFor).
        // Stryker disable once Equality: with < instead, a zero rate divides to infinity, which
        // converts to a negative unit count that the caller then refuses anyway - the same
        // answer by a worse route, and not one worth writing a test around
        if (world.Configuration.ItemCatalog.HungerRestoredPerUnitFor(food) <= 0f)
        {
            return ActionBlocker.NotEdible;
        }

        // Nothing to put right. The player's Eat button deliberately stops here rather than at
        // WorldState.IsHungryEnoughToEat: being told to eat is not the same as deciding to.
        if (person.Needs.Hunger <= 0f)
        {
            return ActionBlocker.NotHungry;
        }

        if (availableUnits <= 0)
        {
            return ActionBlocker.MissingMaterials;
        }

        // Find, not Get - a caller with no "eating" skill registered at all (a minimal test
        // world, say) just means this can never succeed, not a crash.
        return world.Configuration.SkillCatalog.Find(Skill) is { } skillDefinition
            && person.KnownTechniques.Contains(skillDefinition.BaseTechnique)
            ? ActionBlocker.None
            : ActionBlocker.NotLearned;
    }

    // The act of eating itself, apart from where the food comes from: EatCommand feeds from
    // the inventory, GatherCommand straight from the source being picked ("into the mouth"),
    // and both have to gate, satisfy and train identically. Eats just enough of the
    // `availableUnits` on offer to reach zero hunger (or all of them if that isn't enough) and
    // returns how many, leaving the caller to take exactly that many from wherever they were.
    public static int Eat(WorldState world, Person person, ItemKindId food, int availableUnits)
    {
        if (EatingBlocker(world, person, food, availableUnits) is not ActionBlocker.None)
        {
            return 0;
        }

        var skillDefinition = world.Configuration.SkillCatalog.Get(Skill);
        var restoredPerUnit = world.Configuration.ItemCatalog.HungerRestoredPerUnitFor(food);
        if (person.KnownTechniques.Contains(skillDefinition.EfficientTechnique))
        {
            restoredPerUnit *= EfficientHungerRestoredMultiplier;
        }

        // At least one: EatingBlocker has already refused both a person with no hunger to put
        // right and a caller offering nothing to eat.
        var unitsNeeded = (int)MathF.Ceiling(person.Needs.Hunger / restoredPerUnit);
        var unitsEaten = Math.Min(availableUnits, unitsNeeded);

        person.Needs.Hunger = Math.Max(0f, person.Needs.Hunger - (unitsEaten * restoredPerUnit));

        person.Skills.Increase(Skill, SkillGainPerMeal);
        if (person.Skills.Get(Skill) >= DiscoveryThreshold)
        {
            person.KnownTechniques.Add(skillDefinition.EfficientTechnique);
        }

        return unitsEaten;
    }
}
