using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// The player teaching someone directly - the only way anyone ever learns anything before
// there's at least one other person around who already knows it and also knows how to teach
// (see TeachCommand). Unconditional by design: the player is the sole initial source of every
// technique in the world, not another in-world actor bound by the same rules (proximity,
// knowing "teaching" themselves, ...) real people are - this is how that very first "teaching"
// base technique itself gets into the world at all. Not exposed as its own UI action - Main.cs
// triggers this implicitly (TeachBaseTechniqueIfNeeded) the moment the player directs a person
// to gather/fell/eat/teach something they don't already know how to: pointing at the resource
// (or the student) *is* showing them how, not a separate step beforehand.
public sealed record GrantTechniqueCommand(PersonId PersonId, TechniqueId Technique) : ICommand
{
    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        person?.KnownTechniques.Add(Technique);
    }
}
