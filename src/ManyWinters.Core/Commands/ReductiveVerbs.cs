using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;

namespace ManyWinters.Core.Commands;

// Which verb a single thing in the pack answers to. The player is never shown a list of verbs to
// choose from and neither is a person idly turning something over (see
// docs/materials-and-crafting-architecture.md section 7): one thing picked is the whole of the
// question, and this is where it is answered.
//
// The item's own transitions decide it (FormTransition), so nothing here knows that grass is
// twisted or stone is knapped. Both the directed path (WorkshopActions) and the autonomous one
// (WorldState.TrialOf) ask this, which is why it lives beside the commands rather than in
// either.
public static class ReductiveVerbs
{
    // What working this one thing down would be, or null when it answers to no reductive verb at
    // all. An item that answered to two would take the first; nothing does yet, and the day one
    // does, which of them a person reaches for is a question worth asking properly.
    public static (SkillTypeId Skill, ICommand Command)? For(Person person, ItemKindId kind, ItemCatalog items)
    {
        if (items.TransitionFor(kind, TwistCommand.Verb) is not null)
        {
            return (TwistCommand.Skill, new TwistCommand(person, kind));
        }

        if (items.TransitionFor(kind, KnapCommand.Verb) is not null)
        {
            return (KnapCommand.Skill, new KnapCommand(person, kind));
        }

        return null;
    }
}
