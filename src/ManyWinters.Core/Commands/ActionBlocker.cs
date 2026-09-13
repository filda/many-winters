namespace ManyWinters.Core.Commands;

// Why a command cannot run right now, asked before it runs. Every command states its
// preconditions here once; Execute asks first and returns, so the player's menu and the world
// can never disagree about what is possible (docs/todo/todo.md).
//
// A value, not a sentence: how a refusal is worded, and whether it is shown at all, belongs to
// the presentation layer. It draws the line the player feels - a knowledge blocker hides the
// action (discovering it is the game), a circumstance blocker greys it out with the reason.
public enum ActionBlocker
{
    None = 0,

    // Circumstance.
    ActorIsDead,
    TargetIsDead,
    TargetIsAlive,
    TargetIsGone,
    TooFar,
    MissingMaterials,
    InventoryFull,
    StoreIsEmpty,
    NothingLeft,
    NothingToRepair,
    AlreadyBuried,
    NotHungry,
    NotEdible,
    CannotBeFelled,
    SamePerson,
    WrongSex,
    CloseKin,
    TooYoung,
    AlreadyNursing,

    // Knowledge: the actor has never been taught this.
    NotLearned,

    // Teaching only, and not the same as NotLearned: the teacher knows how to teach, just not
    // the thing being asked for.
    TeacherDoesNotKnowIt,
}
