using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Working over something already made, rather than working a raw material down: the first verb
// that takes a worked object as its subject (see docs/materials-and-crafting-architecture.md
// section 3, `Grind`/`Sharpen`).
//
// It is not a reductive verb in the shape of the others and does not use ReductiveWork: nothing
// changes form. A wedge comes out a wedge - keener, and smaller, because an edge is renewed by
// taking material off it. Quality up and mass down is the whole of the trade, and both are read
// by the same score (ItemCatalog.ChoppingScoreOf), so grinding away at an already-good edge
// makes a worse tool without any rule saying so.
//
// What it is worth is bounded by the hand doing it: an edge can be brought up to what this
// person could have made themselves and no further. So a practised knapper can rescue the poor
// wedge somebody else struck, a beginner cannot improve a master's, and going back to a tool
// made in one's first winter is worth doing once the hands have learned something.
public sealed record SharpenCommand(Person Person, Assembly Thing) : ICommand
{
    // Directing somebody to sharpen is how they learn to sharpen (see ActionOffer.TeachFirst).
    public static readonly SkillTypeId Skill = new("sharpening");

    // Private, unlike the reductive verbs': nothing changes form, so no content names this verb
    // in an item's transitions and the only thing that reads it is the roll's own seed.
    private static readonly TechniqueId Verb = new("sharpen");

    private const float SkillGainPerAttempt = 1f;

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (!Person.Inventory.Assemblies.Contains(Thing))
        {
            return ActionBlocker.MissingMaterials;
        }

        if (EdgeOf(Thing, world) is not { } edge)
        {
            return ActionBlocker.NothingToSharpen;
        }

        // Renewing a stone edge is striking flakes off it, so it asks of the substance exactly
        // what knapping one in the first place asked (see MaterialAffordances.CanKnap).
        if (world.Configuration.MaterialCatalog.Find(edge.Material) is not { } material || !MaterialAffordances.CanKnap(material))
        {
            return ActionBlocker.NotKnappable;
        }

        // Asked last, like every knowledge gate (see ActionBlocker.NotLearned).
        var skill = world.Configuration.SkillCatalog.Find(Skill);
        return skill is not null && Person.KnownTechniques.Contains(skill.BaseTechnique)
            ? ActionBlocker.None
            : ActionBlocker.NotLearned;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        var edge = EdgeOf(Thing, world)!;

        // Working a thing is how somebody comes to know what it is made of, whether or not the
        // working came off (see WorkAttempt.TeachesWhatItIs).
        WorkAttempt.TeachesWhatItIs(world, Person, edge.Material);

        // Material comes off the edge either way: a botched strike takes as much of it as a good
        // one, and only a good one leaves the edge keener for it.
        var worn = edge with { Volume = edge.Volume * (1f - world.Configuration.Rules.VolumeLostPerSharpening) };
        var reworked = WorkAttempt.Succeeds(Person, Skill, Verb, world.Clock.CurrentTick)
            ? worn with { Quality = Math.Max(edge.Quality, WorkAttempt.QualityFor(Person, Skill)) }
            : worn;

        Person.Inventory.RemoveAssembly(Thing);
        Person.Inventory.AddAssembly(WithReplaced(Thing, edge, reworked));

        Person.Skills.Increase(Skill, SkillGainPerAttempt);
    }

    // Whether there is anything on this object to sharpen at all, which is what the workbench
    // asks before offering the attempt: a pick that leads nowhere is not an offer (see
    // WorkshopActions).
    public static bool HasAnEdge(Assembly thing, WorldState world) => EdgeOf(thing, world) is not null;

    // The piece this object cuts with, wherever it sits inside it: the one whose shape presents
    // an edge and whose substance is hard enough to hold it. Mass and workmanship are left out
    // on purpose - they decide how well the thing chops (ItemCatalog.ChoppingScoreOf), not which
    // part of it is the blade.
    private static Assembly.Part? EdgeOf(Assembly assembly, WorldState world) => assembly switch
    {
        Assembly.Part part => Keenness(part, world) > 0f ? part : null,
        Assembly.Joined joined => Keener(EdgeOf(joined.Left, world), EdgeOf(joined.Right, world), world),
        _ => null,
    };

    private static Assembly.Part? Keener(Assembly.Part? left, Assembly.Part? right, WorldState world)
    {
        if (left is null || right is null)
        {
            return left ?? right;
        }

        return Keenness(left, world) >= Keenness(right, world) ? left : right;
    }

    private static float Keenness(Assembly.Part part, WorldState world) =>
        (world.Configuration.FormCatalog.Find(part.Form)?.EdgeSharpness ?? 0f)
        * (world.Configuration.MaterialCatalog.Find(part.Material)?.Hardness ?? 0f);

    // The object rebuilt with one piece swapped out. The first matching piece only: two pieces
    // that match in every particular are the same thing to anyone who could tell them apart (see
    // Inventory.RemoveAssembly), but one attempt sharpens one edge.
    private static Assembly WithReplaced(Assembly assembly, Assembly.Part target, Assembly.Part replacement)
    {
        var done = false;

        return Rewrite(assembly);

        Assembly Rewrite(Assembly current)
        {
            switch (current)
            {
                case Assembly.Part part when !done && part == target:
                    done = true;
                    return replacement;

                case Assembly.Joined joined:
                    var left = Rewrite(joined.Left);
                    var right = Rewrite(joined.Right);
                    return joined with { Left = left, Right = right };

                default:
                    return current;
            }
        }
    }
}
