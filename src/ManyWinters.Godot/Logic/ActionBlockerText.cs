using ManyWinters.Core.Commands;

namespace ManyWinters.Godot.Logic;

// An ActionBlocker put into words for the player - the counterpart of InspectorText for
// refusals. Core states why an action cannot run as a value; the wording is ours, and lives
// here rather than in the panel so it is a plain function of the blocker.
//
// Written as what is missing rather than as a scolding ("Too far away", not "You are too far
// away"): it sits under a greyed-out button, where it reads as a label on the obstacle.
internal static class ActionBlockerText
{
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
