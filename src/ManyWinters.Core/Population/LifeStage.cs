namespace ManyWinters.Core.Population;

// The bands of a life, measured in winters. Not a cosmetic label on the inspector: an infant
// is fed by its mother and never sent off on its own (WorldState.Advance), and only an adult
// can have a child (BirthCommand). CarryCapacity's curve turns at the same ages rather than
// at private numbers of its own, because it is one body growing up and then tiring, not two
// unrelated curves that happen to look alike.
public enum LifeStage
{
    Infant,
    Child,
    Adult,
    Elder,
}
