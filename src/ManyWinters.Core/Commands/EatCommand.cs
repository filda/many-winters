using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Spends food from the inventory. Eats just enough to reach zero hunger, or all there is - not
// a fixed amount, so a UI "Eat" action need not work out how much hunger is left.
public sealed record EatCommand(Person Person, ItemKindId FoodItem) : ICommand
{
    // Eating has to be taught like everything else (see SkillDefinition.BaseTechnique): someone
    // holding a full pack of food can still starve.
    public static readonly SkillTypeId Skill = new("eating");

    // A skilled eater gets more out of the same food - the simplest bonus that gives
    // EfficientTechnique an effect, like ToolHarvestBonus for gathering.
    private const float EfficientHungerRestoredMultiplier = 1.2f;

    private const float SkillGainPerMeal = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // Stated in tries, not as a level: the practice curve is not linear (see Skills.Increase).
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    public void Execute(WorldState world)
    {
        // Unguarded: removing zero units is a no-op.
        Person.Inventory.Remove(FoodItem, Eat(world, Person, FoodItem, Person.Inventory.Get(FoodItem)));
    }

    // Eating apart from where the food comes from: EatCommand feeds from the inventory,
    // GatherCommand straight from the node, and both gate, satisfy and train identically. Eats
    // enough of `availableUnits` to reach zero hunger (or all of them) and returns how many.
    public static int Eat(WorldState world, Person person, ItemKindId food, int availableUnits)
    {
        if (!person.IsAlive)
        {
            return 0;
        }

        // Find, not Get: a catalog without "eating" (a minimal test world) means this never
        // succeeds.
        if (world.Configuration.SkillCatalog.Find(Skill) is not { } skillDefinition
            || !person.KnownTechniques.Contains(skillDefinition.BaseTechnique))
        {
            return 0;
        }

        var restoredPerUnit = world.Configuration.ItemCatalog.HungerRestoredPerUnitFor(food);
        // Guards the division below: an undescribed item restores nothing
        // (see ItemCatalog.HungerRestoredPerUnitFor).
        // Stryker disable once Equality: with < instead, a zero rate divides to infinity, which
        // converts to a negative unit count that the check below then refuses anyway - the same
        // answer by a worse route, and not one worth writing a test around
        if (restoredPerUnit <= 0f)
        {
            return 0;
        }

        if (person.KnownTechniques.Contains(skillDefinition.EfficientTechnique))
        {
            restoredPerUnit *= EfficientHungerRestoredMultiplier;
        }

        var unitsNeeded = (int)MathF.Ceiling(person.Needs.Hunger / restoredPerUnit);
        var unitsEaten = Math.Min(availableUnits, unitsNeeded);
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
