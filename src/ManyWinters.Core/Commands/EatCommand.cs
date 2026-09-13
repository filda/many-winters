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

    public ActionBlocker Blocker(WorldState world)
    {
        var blocker = EatingBlocker(world, Person, FoodItem);
        if (blocker is not ActionBlocker.None)
        {
            return blocker;
        }

        return Person.Inventory.Get(FoodItem) > 0 ? ActionBlocker.None : ActionBlocker.MissingMaterials;
    }

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

    // Why this person could not eat this food at all, leaving aside where it would come from -
    // shared by the command's own Blocker, which adds "and they are carrying some", and by Eat,
    // which GatherCommand calls with units straight off the node.
    public static ActionBlocker EatingBlocker(WorldState world, Person person, ItemKindId food)
    {
        if (!person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        // Find, not Get - a caller with no "eating" skill registered at all (a minimal test
        // world, say) just means this can never succeed, not a crash.
        if (world.Configuration.SkillCatalog.Find(Skill) is not { } skillDefinition
            || !person.KnownTechniques.Contains(skillDefinition.BaseTechnique))
        {
            return ActionBlocker.NotLearned;
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
        return person.Needs.Hunger > 0f ? ActionBlocker.None : ActionBlocker.NotHungry;
    }

    // The act of eating itself, apart from where the food comes from: EatCommand feeds from
    // the inventory, GatherCommand straight from the source being picked ("into the mouth"),
    // and both have to gate, satisfy and train identically. Eats just enough of the
    // `availableUnits` on offer to reach zero hunger (or all of them if that isn't enough) and
    // returns how many, leaving the caller to take exactly that many from wherever they were.
    public static int Eat(WorldState world, Person person, ItemKindId food, int availableUnits)
    {
        if (EatingBlocker(world, person, food) is not ActionBlocker.None)
        {
            return 0;
        }

        var skillDefinition = world.Configuration.SkillCatalog.Get(Skill);
        var restoredPerUnit = world.Configuration.ItemCatalog.HungerRestoredPerUnitFor(food);
        if (person.KnownTechniques.Contains(skillDefinition.EfficientTechnique))
        {
            restoredPerUnit *= EfficientHungerRestoredMultiplier;
        }

        var unitsNeeded = (int)MathF.Ceiling(person.Needs.Hunger / restoredPerUnit);
        var unitsEaten = Math.Min(availableUnits, unitsNeeded);
        // Not a hunger test - EatingBlocker has already refused a person with none. This is the
        // caller who offered nothing to eat.
        if (unitsEaten <= 0)
        {
            return 0;
        }

        person.Needs.Hunger = Math.Max(0f, person.Needs.Hunger - (unitsEaten * restoredPerUnit));

        person.Skills.Increase(Skill, SkillGainPerMeal);
        if (person.Skills.Get(Skill) >= DiscoveryThreshold)
        {
            person.KnownTechniques.Add(skillDefinition.EfficientTechnique);
        }

        return unitsEaten;
    }
}
