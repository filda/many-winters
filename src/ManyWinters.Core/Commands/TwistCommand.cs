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

        // Spent either way: a handful mangled in the trying is gone as surely as one twisted
        // well. Practice is earned either way too - a spoiled attempt still taught the hands
        // something.
        Person.Inventory.Remove(Item, transition.InputAmount);

        if (WorkAttempt.Succeeds(Person, Skill, Verb, world.Clock.CurrentTick))
        {
            // Bulk carries over from what went in, so twisting neither creates nor destroys
            // weight.
            Person.Inventory.AddAssembly(new Assembly.Part(
                definition.Material,
                transition.Form,
                WorkAttempt.QualityFor(Person, Skill),
                definition.Volume * transition.InputAmount));
        }

        Person.Skills.Increase(Skill, SkillGainPerTwist);
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
