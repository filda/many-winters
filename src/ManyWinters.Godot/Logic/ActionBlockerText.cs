using ManyWinters.Core.Commands;

namespace ManyWinters.Godot.Logic;

// The line under an action, put into words for the player - the counterpart of InspectorText for
// refusals. Core states why an action cannot run as a value; the wording is ours, and lives
// here rather than in the panel so it is a plain function of the blocker.
//
// Written as what is missing rather than as a scolding ("Too far away", not "You are too far
// away"): it sits under a greyed-out button, where it reads as a label on the obstacle.
internal static class ActionBlockerText
{
    // What an offer says about itself, which is not always a refusal: the one distance the person
    // can be sent to close themselves reads as part of the order rather than as an obstacle (see
    // ActionOffer.NeedsWalkingTo), and its button is pressable.
    internal static string For(ActionOffer offer) =>
        offer.NeedsWalkingTo ? "Will walk over first" : For(offer.Blocker);

    internal static string For(ActionBlocker blocker) => blocker switch
    {
        ActionBlocker.None => string.Empty,
        ActionBlocker.ActorIsDead => "The dead do nothing",
        ActionBlocker.TargetIsDead => "They are dead",
        ActionBlocker.TargetIsAlive => "They are still alive",
        ActionBlocker.TargetIsGone => "It is gone",
        ActionBlocker.TooFar => "Too far away",
        ActionBlocker.MissingMaterials => "Not carrying enough",
        ActionBlocker.InventoryFull => "Carrying too much already",
        ActionBlocker.StoreIsEmpty => "The store has none",
        ActionBlocker.NothingLeft => "Nothing left to take",
        ActionBlocker.NothingToRepair => "Nothing to mend",
        ActionBlocker.AlreadyBuried => "Already buried",
        ActionBlocker.NotHungry => "Not hungry",
        ActionBlocker.NotEdible => "Not food",
        ActionBlocker.CannotBeFelled => "Nothing to fell",
        ActionBlocker.MissingTool => "Needs the right tool",
        ActionBlocker.NotTwistable => "Will not take a twist",
        ActionBlocker.NotKnappable => "Will not break to an edge",
        ActionBlocker.SamePerson => "Needs two people",
        ActionBlocker.WrongSex => "Needs a man and a woman",
        ActionBlocker.CloseKin => "Too closely related",
        ActionBlocker.TooYoung => "Still a child",
        ActionBlocker.AlreadyNursing => "Still nursing",
        ActionBlocker.NotLearned => "Never learned how",
        ActionBlocker.TeacherDoesNotKnowIt => "Does not know it",
        _ => string.Empty,
    };
}
