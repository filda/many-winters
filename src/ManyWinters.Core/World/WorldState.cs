using ManyWinters.Core.Population;
using ManyWinters.Core.Time;

namespace ManyWinters.Core.World;

public sealed class WorldState
{
    private readonly List<Person> _people = new();
    private int _nextPersonId = 1;

    public SimulationClock Clock { get; } = new();

    public IReadOnlyList<Person> People => _people;

    public int NextPersonId => _nextPersonId;

    public Person AddPerson(string name, Position position)
    {
        var person = new Person
        {
            Id = new PersonId(_nextPersonId++),
            Name = name,
            Position = position,
        };

        _people.Add(person);
        return person;
    }

    internal void RestorePerson(Person person) => _people.Add(person);

    internal void SetNextPersonId(int value) => _nextPersonId = value;
}
