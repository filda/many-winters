namespace ManyWinters.Core.Population;

// The bands of a life, in winters. Behaviour hangs on them: an infant is fed by its mother and
// never sent off alone (WorldState.Advance), only an adult can have a child (BirthCommand), and
// CarryCapacity's curve turns at the same ages.
public enum LifeStage
{
    Infant,
    Child,
    Adult,
    Elder,
}
