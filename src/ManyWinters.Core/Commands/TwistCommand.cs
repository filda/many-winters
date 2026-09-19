using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// The first of the reductive verbs (see docs/materials-and-crafting-architecture.md section 3):
// one material in, the same material out in a shape that affords something the raw stuff did
// not - grass into cord. What it turns into is the item's own business (FormTransition), and
// whether it can be twisted at all is the material's (MaterialAffordances.CanTwist), so neither
// answer is authored per outcome.
//
// The worked piece is the first thing in the game to be held as itself rather than as a count:
// its quality is this person's, earned at this moment, and two cords are not interchangeable.
public sealed record TwistCommand(Person Person, ItemKindId Item) : ICommand
{
    // Directing somebody to twist is how they learn to twist, the same way pointing at a tree
    // teaches gathering (see SkillDefinition.BaseTechnique, ActionOffer.TeachFirst).
    public static readonly SkillTypeId Skill = new("twisting");

    // The verb itself, as content names it in an item's transitions (see FormTransition).
    public static readonly TechniqueId Verb = new("twist");

    private const float SkillGainPerTwist = 1f;

    // A first cord is poor but real, and a practiced hand's is sound: quality walks from the one
    // to the other, and never starts at zero, or a beginner's work would be worth nothing at all
    // (Assembly.Durability multiplies by it).
    private const float NoviceQuality = 0.2f;
    private const int PracticesForMastery = 50;

    private static readonly float MasteryLevel = Skills.LevelAfter(PracticesForMastery);

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (Transition(world) is not { } transition || Person.Inventory.Get(Item) < transition.InputAmount)
        {
            return ActionBlocker.MissingMaterials;
        }

        if (!CanBeTwisted(world))
        {
            return ActionBlocker.NotTwistable;
        }

        // Asked last, like every knowledge gate (see ActionBlocker.NotLearned).
        var skill = world.Configuration.SkillCatalog.Find(Skill);
        return skill is not null && Person.KnownTechniques.Contains(skill.BaseTechnique)
            ? ActionBlocker.None
            : ActionBlocker.NotLearned;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        var transition = Transition(world)!;
        var definition = world.Configuration.ItemCatalog.Get(Item);

        Person.Inventory.Remove(Item, transition.InputAmount);

        // Bulk carries over from what went in, so twisting neither creates nor destroys weight.
        Person.Inventory.AddAssembly(new Assembly.Part(
            definition.Material,
            transition.Form,
            QualityFor(Person),
            definition.Volume * transition.InputAmount));

        Person.Skills.Increase(Skill, SkillGainPerTwist);
    }

    // How good a piece this person turns out right now: their practice at twisting, read off the
    // same curve every other skill uses (Skills.Increase).
    public static float QualityFor(Person person)
    {
        var mastery = Math.Clamp(person.Skills.Get(Skill) / MasteryLevel, 0f, 1f);
        return NoviceQuality + ((1f - NoviceQuality) * mastery);
    }

    private FormTransition? Transition(WorldState world) =>
        world.Configuration.ItemCatalog.TransitionFor(Item, Verb);

    private bool CanBeTwisted(WorldState world)
    {
        var definition = world.Configuration.ItemCatalog.Get(Item);
        var material = world.Configuration.MaterialCatalog.Find(definition.Material);

        return material is not null && MaterialAffordances.CanTwist(material);
    }
}
