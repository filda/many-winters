using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Everything the reductive verbs do alike (see docs/materials-and-crafting-architecture.md
// section 3): one material in, the same material out in a shape that affords something the raw
// stuff did not. Grass into cord, a stone lump into a wedge - the same act, and telling them
// apart takes only three facts, which is what this holds.
//
// The verbs stay separate commands because that is what the world speaks in: an offer names one,
// and the discovery pass asks each what stands in the way (WorldState.TrialOf). What they do not
// need is a second copy of the machinery, which is all that was different between them.
internal sealed record ReductiveWork(
    TechniqueId Verb,
    SkillTypeId Skill,
    // What a substance has to be like to take this verb at all, asked of the material rather
    // than authored per outcome (see MaterialAffordances).
    Func<MaterialDefinition, bool> Affords,
    // What to say when the substance will not take it - the one refusal that is this verb's own.
    ActionBlocker Refusal)
{
    private const float SkillGainPerAttempt = 1f;

    internal ActionBlocker Blocker(Person person, ItemKindId item, WorldState world)
    {
        if (!person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (Transition(item, world) is not { } transition || person.Inventory.Get(item) < transition.InputAmount)
        {
            return ActionBlocker.MissingMaterials;
        }

        if (!WillTakeIt(item, world))
        {
            return Refusal;
        }

        // Asked last, like every knowledge gate (see ActionBlocker.NotLearned).
        var skill = world.Configuration.SkillCatalog.Find(Skill);
        return skill is not null && person.KnownTechniques.Contains(skill.BaseTechnique)
            ? ActionBlocker.None
            : ActionBlocker.NotLearned;
    }

    internal void Execute(Person person, ItemKindId item, WorldState world)
    {
        if (Blocker(person, item, world) is not ActionBlocker.None)
        {
            return;
        }

        var transition = Transition(item, world)!;
        var definition = world.Configuration.ItemCatalog.Get(item);

        // Whatever comes of it, they learn what the stuff is: they had it in their hands and
        // worked it, and a handful spoiled teaches as much as one twisted well (see Beliefs,
        // SimulationRules.UnderstandingFromWorkingIt). This is how a player reaches past what
        // their band already understands.
        WorkAttempt.TeachesWhatItIs(world, person, definition.Material);

        // Spent either way: a handful mangled in the trying is gone as surely as one twisted
        // well, and a stone shattered by a bad strike is not a stone any more. Practice is
        // earned either way too - a spoiled attempt still taught the hands something.
        person.Inventory.Remove(item, transition.InputAmount);

        if (WorkAttempt.Succeeds(person, Skill, Verb, world.Clock.CurrentTick))
        {
            // Bulk carries over from what went in, so working a thing down neither creates nor
            // destroys weight (see FormTransition).
            person.Inventory.AddAssembly(new Assembly.Part(
                definition.Material,
                transition.Form,
                WorkAttempt.QualityFor(person, Skill),
                definition.Volume * transition.InputAmount));
        }

        person.Skills.Increase(Skill, SkillGainPerAttempt);
    }

    private FormTransition? Transition(ItemKindId item, WorldState world) =>
        world.Configuration.ItemCatalog.TransitionFor(item, Verb);

    private bool WillTakeIt(ItemKindId item, WorldState world)
    {
        var definition = world.Configuration.ItemCatalog.Get(item);
        var material = world.Configuration.MaterialCatalog.Find(definition.Material);

        return material is not null && Affords(material);
    }
}
