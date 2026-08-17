using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

public sealed class Person
{
    public required PersonId Id { get; init; }

    public required string Name { get; init; }

    public Position Position { get; set; }

    public Needs Needs { get; init; } = new();

    public PersonTaskQueue Tasks { get; init; } = new();
}
