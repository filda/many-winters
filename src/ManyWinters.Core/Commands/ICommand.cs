using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public interface ICommand
{
    void Execute(WorldState world);

    // Why this cannot run right now, or None. Execute asks it first, so a precondition is
    // written in exactly one place and the UI can ask the same question before offering the
    // action at all (see ActionBlocker).
    ActionBlocker Blocker(WorldState world);
}
