using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record GatherCommand(Person Person, ResourceNode Node) : ICommand
{
    private const float BaseHarvestAmount = 20f;
    private const float EfficientHarvestAmount = 40f;
    private const float SkillGainPerGather = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // The practice curve is not linear any more (see Skills.Increase), so the threshold is
    // stated as the number of tries it stands for rather than as a level.
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    public void Execute(WorldState world)
    {
        // Stryker disable once Equality: RemainingAmount never goes negative, and consuming zero is already a no-op below, so > 0 and >= 0 are indistinguishable here
        if (!Person.IsAlive || !Node.IsAlive || Node.RemainingAmount <= 0 || !world.IsWithinReach(Person.Position, Node.Position))
        {
            return;
        }

        var resource = world.Configuration.ResourceCatalog.Get(Node.Kind);
        var skill = resource.Skill;
        var skillDefinition = world.Configuration.SkillCatalog.Get(skill);
        // Never self-taught, unlike the efficient technique below - has to come from the
        // player or another person first (see SkillDefinition.BaseTechnique's own doc comment).
        if (!Person.KnownTechniques.Contains(skillDefinition.BaseTechnique))
        {
            return;
        }

        var technique = skillDefinition.EfficientTechnique;

        var harvestAmount = Person.KnownTechniques.Contains(technique) ? EfficientHarvestAmount : BaseHarvestAmount;
        if (skillDefinition.Tool is { } tool && Person.Inventory.Get(tool) > 0)
        {
            harvestAmount += skillDefinition.ToolHarvestBonus;
        }

        var climate = world.Configuration.SeasonParameters.ClimateFor(world.CurrentSeason);
        harvestAmount *= resource.YieldMultiplierFor(climate);

        var potentialConsumed = Math.Min(Node.RemainingAmount, harvestAmount);

        if (resource.YieldsItem is { } item)
        {
            // A hungry picker eats as they go ("straight into the mouth") before pocketing
            // anything - the one way someone whose backpack is already full of something else
            // still gets fed at a food source. Only what actually got eaten or fits in the
            // inventory comes off the node - a full backpack leaves the rest standing to
            // gather later, rather than the excess vanishing.
            // Same "hungry enough to bother" test the autonomous pass uses (see
            // WorldState.IsHungryEnoughToEat) - otherwise a picker standing at a food source
            // eats one unit off it every tick, which is both an odd way to eat and a way to
            // practice gathering and eating forever without moving.
            var eaten = world.IsHungryEnoughToEat(Person) ? EatCommand.Eat(world, Person, item, (int)potentialConsumed) : 0;
            var added = Person.Inventory.AddUpToCapacity(item, (int)potentialConsumed - eaten, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Person));
            var taken = eaten + added;
            // Coming away from a node with nothing is not gathering - it earns no practice, so
            // a full backpack can't grind a technique out of thin air.
            if (taken <= 0)
            {
                return;
            }

            Node.RemainingAmount -= taken;
        }
        else
        {
            Node.RemainingAmount -= potentialConsumed;
            Person.Needs.Hunger = Math.Max(0f, Person.Needs.Hunger - potentialConsumed);
        }

        Person.Skills.Increase(skill, SkillGainPerGather);
        if (Person.Skills.Get(skill) >= DiscoveryThreshold)
        {
            Person.KnownTechniques.Add(technique);
        }
    }
}
