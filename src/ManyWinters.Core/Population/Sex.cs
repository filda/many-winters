namespace ManyWinters.Core.Population;

// Which half of a pair someone can be when a child is born. Deliberately the whole of what
// this models: nothing else in the simulation reads it, and nothing about a person's work,
// strength, knowledge or standing depends on it.
public enum Sex
{
    Female,
    Male,
}
