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
    private const float DiscoveryThreshold = 5f;

    public void Execute(WorldState world)
    {
        if (!Person.IsAlive || Person.Needs.Hunger <= 0f)
        {
            return;
        }

        // Find, not Get - a caller with no "eating" skill registered at all (a minimal test
        // world, say) just means this can never succeed, not a crash.
        if (world.Configuration.SkillCatalog.Find(Skill) is not { } skillDefinition
            || !Person.KnownTechniques.Contains(skillDefinition.BaseTechnique))
        {
            return;
        }

        var restoredPerUnit = world.Configuration.ItemCatalog.HungerRestoredPerUnitFor(FoodItem);
        if (restoredPerUnit <= 0f)
        {
            return;
        }

        if (Person.KnownTechniques.Contains(skillDefinition.EfficientTechnique))
        {
            restoredPerUnit *= EfficientHungerRestoredMultiplier;
        }

        var available = Person.Inventory.Get(FoodItem);
        var unitsNeeded = (int)MathF.Ceiling(Person.Needs.Hunger / restoredPerUnit);
        var unitsEaten = Math.Min(available, unitsNeeded);
        if (unitsEaten <= 0)
        {
            return;
        }

        Person.Inventory.Remove(FoodItem, unitsEaten);
        Person.Needs.Hunger = Math.Max(0f, Person.Needs.Hunger - (unitsEaten * restoredPerUnit));

        Person.Skills.Increase(Skill, SkillGainPerMeal);
        if (Person.Skills.Get(Skill) >= DiscoveryThreshold)
        {
            Person.KnownTechniques.Add(skillDefinition.EfficientTechnique);
        }
    }
}
