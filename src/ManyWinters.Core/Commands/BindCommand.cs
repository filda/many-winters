using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// One of the two things a binding holds together. A person's pack has two tiers (see
// Inventory), so a thing to be bound is either a unit out of the raw stock or one of the worked
// objects they are carrying - and because the second case is what lets a bound thing be bound
// again, depth needs no special case anywhere (see
// docs/materials-and-crafting-architecture.md section 6).
public abstract record BindTarget
{
    private BindTarget()
    {
    }

    public sealed record Stock(ItemKindId Kind) : BindTarget;

    public sealed record Worked(Assembly Thing) : BindTarget;
}

// The first of the combinative verbs (section 3): two objects in, one object out, held together
// by a lashing that is itself consumed. Binary in its operands even though three things go in,
// because the cordage is the medium rather than a third thing being joined - which is what
// keeps assembly depth emergent instead of capped.
//
// Which cordage is not asked of the player: the simulation reaches for the best binding they
// carry, the same way the workshop panel described in section 7 asks for one or two things and
// nothing else.
public sealed record BindCommand(Person Person, BindTarget Left, BindTarget Right) : ICommand
{
    // Directing somebody to bind is how they learn to bind (see ActionOffer.TeachFirst).
    public static readonly SkillTypeId Skill = new("binding");

    private const float SkillGainPerBind = 1f;

    // A first lashing holds, badly; a practised one holds as well as the cordage itself allows.
    private const float NoviceStrength = 0.2f;
    private const int PracticesForMastery = 50;

    private static readonly float MasteryLevel = Skills.LevelAfter(PracticesForMastery);

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        // Two of the same stock entry need two units of it, so the pair is checked together
        // rather than one at a time.
        if (!Holds(Left, Right) || BestBinding(world) is null)
        {
            return ActionBlocker.MissingMaterials;
        }

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

        var binding = BestBinding(world)!;
        var left = TakeFromPack(Left, world);
        var right = TakeFromPack(Right, world);
        Person.Inventory.RemoveAssembly(binding);

        Person.Inventory.AddAssembly(new Assembly.Joined(
            StrengthOf(binding, world),
            world.Configuration.ItemCatalog.WeightOf(binding),
            left,
            right));

        Person.Skills.Increase(Skill, SkillGainPerBind);
    }

    // How well this lashing holds: the shape's own fitness for binding, what the cordage itself
    // is worth, and the hand that tied it. A joint is never better than the cord it is made of.
    private float StrengthOf(Assembly binding, WorldState world)
    {
        var lashing = binding is Assembly.Part part
            ? world.Configuration.FormCatalog.Find(part.Form)?.LashingStrength ?? 0f
            : 0f;

        var hand = NoviceStrength + ((1f - NoviceStrength) * Math.Clamp(Person.Skills.Get(Skill) / MasteryLevel, 0f, 1f));

        return lashing * binding.Durability(world.Configuration.MaterialCatalog) * hand;
    }

    // The soundest cordage in the pack, so a person who has made a better cord uses it without
    // being told to. Anything the content says cannot lash is not cordage at all.
    private Assembly? BestBinding(WorldState world) =>
        Person.Inventory.Assemblies
            .Where(held => !IsChosenAsTarget(held))
            .Where(held => held is Assembly.Part part && world.Configuration.FormCatalog.Find(part.Form)?.LashingStrength > 0f)
            .OrderByDescending(held => held.Durability(world.Configuration.MaterialCatalog))
            .FirstOrDefault();

    // A cord the player picked out to be bound is not also the thing doing the binding.
    private bool IsChosenAsTarget(Assembly held) =>
        (Left is BindTarget.Worked left && left.Thing == held) || (Right is BindTarget.Worked right && right.Thing == held);

    private bool Holds(BindTarget left, BindTarget right) =>
        (left, right) switch
        {
            // The same stock entry twice - two sticks lashed together - needs two of it.
            (BindTarget.Stock a, BindTarget.Stock b) when a.Kind == b.Kind => Person.Inventory.Get(a.Kind) >= 2,
            _ => Holds(left) && Holds(right),
        };

    private bool Holds(BindTarget target) => target switch
    {
        BindTarget.Stock stock => Person.Inventory.Get(stock.Kind) > 0,
        BindTarget.Worked worked => Person.Inventory.Assemblies.Contains(worked.Thing),
        _ => false,
    };

    // Raw stock becomes a part of the new object as it is taken: unworked, so its soundness is
    // its substance's and nothing more (Assembly.Durability multiplies quality in).
    private Assembly TakeFromPack(BindTarget target, WorldState world)
    {
        switch (target)
        {
            case BindTarget.Stock stock:
                var definition = world.Configuration.ItemCatalog.Get(stock.Kind);
                Person.Inventory.Remove(stock.Kind, 1);
                return new Assembly.Part(definition.Material, definition.Form, UnworkedQuality, definition.Volume);

            case BindTarget.Worked worked:
                Person.Inventory.RemoveAssembly(worked.Thing);
                return worked.Thing;

            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown kind of thing to bind.");
        }
    }

    // Nothing was done to it, so nothing was gained or spoiled: a raw stick is exactly as sound
    // as wood is.
    private const float UnworkedQuality = 1f;
}
