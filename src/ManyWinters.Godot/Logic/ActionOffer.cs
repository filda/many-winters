using ManyWinters.Core.Commands;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// One action put in front of the player: what to call it, the command it would run, and why it
// cannot run right now. The panel renders this and nothing else, so what the menu offers and
// what the world allows are the same question asked once (see ActionBlocker).
internal readonly record struct ActionOffer
{
    private ActionOffer(string label, ICommand command, ActionBlocker blocker, SkillTypeId? teachFirst)
    {
        Label = label;
        Command = command;
        Blocker = blocker;
        TeachFirst = teachFirst;
    }

    public string Label { get; }

    public ICommand Command { get; }

    public ActionBlocker Blocker { get; }

    // The skill whose base technique the player grants by directing this action at all - pointing
    // at the tree is showing them how (see SkillDefinition.BaseTechnique). Set only for the
    // actions that teach, and the reason those are never blocked as NotLearned.
    public SkillTypeId? TeachFirst { get; }

    public bool IsAvailable => Blocker is ActionBlocker.None;

    // An action with something to act on. The blocker is the command's own answer, except that a
    // teaching action forgives NotLearned: refusing it would leave the person no way to ever
    // learn, since being directed is how they do.
    public static ActionOffer For(string label, ICommand command, WorldState world, SkillTypeId? teachFirst = null)
    {
        var blocker = command.Blocker(world);
        if (teachFirst is not null && blocker is ActionBlocker.NotLearned)
        {
            blocker = ActionBlocker.None;
        }

        return new ActionOffer(label, command, blocker, teachFirst);
    }
}
