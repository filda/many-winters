using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// The player teaching someone directly - the only source of knowledge before another person
// knows it and can teach (see TeachCommand). Unconditional by design: the player is not an
// in-world actor bound by proximity or knowing "teaching". Not a UI action of its own: Main.cs
// (TeachBaseTechniqueIfNeeded) fires it when the player directs a person to do something they
// do not yet know how to - pointing at the resource is showing them how.
public sealed record GrantTechniqueCommand(Person Person, TechniqueId Technique) : ICommand
{
    public ActionBlocker Blocker(WorldState world) =>
        Person.IsAlive ? ActionBlocker.None : ActionBlocker.ActorIsDead;

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        Person.KnownTechniques.Add(Technique);
    }
}
