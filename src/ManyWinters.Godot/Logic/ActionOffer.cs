using ManyWinters.Core.Commands;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// One action put in front of the player: what to call it, the command it would run, and why it
// cannot run right now. The panel renders this and nothing else, so what the menu offers and
// what the world allows are the same question asked once.
internal readonly record struct ActionOffer
{
    private ActionOffer(string label, ICommand command, ActionBlocker blocker, SkillTypeId? teachFirst, Position? target)
    {
        Label = label;
        Command = command;
        Blocker = blocker;
        TeachFirst = teachFirst;
        Target = target;
    }

    public string Label { get; }

    public ICommand Command { get; }

    public ActionBlocker Blocker { get; }

    // The skill whose base technique the player grants by directing this action at all - pointing
    // at the tree is showing them how. Set only for the actions that teach, and the reason those
    // are never blocked as NotLearned.
    public SkillTypeId? TeachFirst { get; }

    // Where in the world the action happens, for an action aimed at something; null for an act on
    // the person themselves, which is never out of reach. It is what turns "too far away" from a
    // dead end into a walk.
    public Position? Target { get; }

    // Refused for distance alone, and the distance is somewhere the person can be sent. That is
    // the one refusal the player need not do anything about: walking over is part of the order,
    // not a chore to carry out first.
    public bool NeedsWalkingTo => Blocker is ActionBlocker.TooFar && Target is not null;

    public bool IsAvailable => Blocker is ActionBlocker.None || NeedsWalkingTo;

    // An action with something to act on. The blocker is the command's own answer, except that a
    // teaching action forgives NotLearned: refusing it would leave the person no way to ever
    // learn, since being directed is how they do.
    public static ActionOffer For(
        string label,
        ICommand command,
        WorldState world,
        SkillTypeId? teachFirst = null,
        Position? target = null)
    {
        var blocker = command.Blocker(world);
        if (teachFirst is not null && blocker is ActionBlocker.NotLearned)
        {
            blocker = ActionBlocker.None;
        }

        return new ActionOffer(label, command, blocker, teachFirst, target);
    }

    // The same offer asked again of a world that has moved on since it was made - for an order
    // given to somebody who had to walk there first, and is only now in a position to carry it
    // out. Goes through For, so a teaching action is forgiven NotLearned on arrival exactly as it
    // was when the order was given.
    public ActionOffer Refreshed(WorldState world) => For(Label, Command, world, TeachFirst, Target);
}
