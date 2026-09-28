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
// and the discovery pass asks each what stands in the way. What they do not need is a second
// copy of the machinery, which is all that was different between them.
internal sealed record ReductiveWork(
    TechniqueId Verb,
    SkillTypeId Skill,
    // What a substance has to be like to take this verb at all, asked of the material rather
    // than authored per outcome.
    Func<MaterialDefinition, bool> Affords,
    // What to say when the substance will not take it - the one refusal specific to this verb.
    ActionBlocker Refusal)
{
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

        // Asked last, like every knowledge gate.
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
        // worked it, and a handful spoiled teaches as much as one twisted well. This is how a
        // player reaches past what their band already understands.
        WorkAttempt.TeachesWhatItIs(world, person, definition.Material);

        // Spent either way: a handful mangled in the trying is gone as surely as one twisted
        // well, and a stone shattered by a bad strike is not a stone any more. Practice is
        // earned either way too - a spoiled attempt still taught the hands something.
        person.Inventory.Remove(item, transition.InputAmount);

        if (WorkAttempt.Succeeds(person, Skill, Verb, world.Clock.CurrentTick))
        {
            if (transition.Material is { } curedMaterial)
            {
                // Curing exchanges one already-known substance for another rather than
                // fashioning a new individual object - rawhide and hide are both stock, the same
                // tier meat and bone come off a carcass as, so what comes out lands back there
                // too rather than becoming a worked object with a quality nobody reads. The stock
                // item is found by the very (material, form) pair the transition names.
                var cured = world.Configuration.ItemCatalog.KindFor(curedMaterial, transition.Form)!.Value;
                person.Inventory.Add(cured, transition.InputAmount, world.Clock.CurrentTick, world.Configuration.ItemCatalog);
            }
            else
            {
                // Bulk carries over from what went in, so working a thing down neither creates
                // nor destroys weight.
                person.Inventory.AddAssembly(new Assembly.Part(
                    definition.Material,
                    transition.Form,
                    WorkAttempt.Practised(person, Skill),
                    definition.Volume * transition.InputAmount)
                {
                    MadeTick = world.Clock.CurrentTick,
                });
            }
        }

        person.Skills.Increase(Skill, world.Configuration.Rules.SkillGainPerAttempt);
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
