using System.Runtime.CompilerServices;

// The presentation layer's own calculations are internal - exposed to its test project rather
// than made public, since nothing else has any business calling them.
[assembly: InternalsVisibleTo("ManyWinters.Godot.Tests")]
