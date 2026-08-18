using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public interface ICommand
{
    void Execute(WorldState world);
}
