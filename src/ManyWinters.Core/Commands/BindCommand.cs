using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// The first of the combinative verbs (section 3): two objects in, one object out, held together
// by a lashing that is itself consumed. Binary in its operands even though three things go in,
// because the cordage is the medium rather than a third thing being joined - which is what
// keeps assembly depth emergent instead of capped.
//
// Which cordage is not asked of the player: the simulation reaches for the best binding they
// carry, the same way the workshop panel described in section 7 asks for one or two things and
// nothing else.
public sealed record BindCommand(Person Person, CarriedThing Left, CarriedThing Right) : ICommand
{
    // Directing somebody to bind is how they learn to bind (see ActionOffer.TeachFirst).
    public static readonly SkillTypeId Skill = new("binding");

    // The verb itself, as a joint made by it would be named.
    public static readonly TechniqueId Verb = new("bind");

    private const float SkillGainPerBind = 1f;

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

        // The cordage is spent whether or not the lashing holds - a slipped lashing is not cord
        // any more. What it joined is not: two things that came apart are still two things, so a
        // failed try costs the binding and the time, not the work already done.
        var binding = BestBinding(world)!;
        Person.Inventory.RemoveAssembly(binding);

        // Everything they had their hands on, whether or not the lashing held.
        foreach (var material in MaterialsWorked(world, binding))
        {
            WorkAttempt.TeachesWhatItIs(world, Person, material);
        }

        if (WorkAttempt.Succeeds(Person, Skill, Verb, world.Clock.CurrentTick))
        {
            var left = TakeFromPack(Left, world);
            var right = TakeFromPack(Right, world);

            Person.Inventory.AddAssembly(new Assembly.Joined(
                StrengthOf(binding, world),
                world.Configuration.ItemCatalog.WeightOf(binding),
                left,
                right));
        }

        Person.Skills.Increase(Skill, SkillGainPerBind);
    }

    // A joint is never better than the cord it is made of.
    private float StrengthOf(Assembly binding, WorldState world)
    {
        var lashing = binding is Assembly.Part part
            ? world.Configuration.FormCatalog.Find(part.Form)?.LashingStrength ?? 0f
            : 0f;

        return lashing * binding.Durability(world.Configuration.MaterialCatalog) * WorkAttempt.QualityFor(Person, Skill);
    }

    private IEnumerable<MaterialId> MaterialsWorked(WorldState world, Assembly binding) =>
        new[] { Left, Right }
            .Select(target => MaterialOf(world, target))
            .OfType<MaterialId>()
            .Concat(binding is Assembly.Part part ? [part.Material] : Array.Empty<MaterialId>());

    private static MaterialId? MaterialOf(WorldState world, CarriedThing target) => target switch
    {
        CarriedThing.Stock stock => world.Configuration.ItemCatalog.Get(stock.Kind).Material,
        CarriedThing.Worked { Thing: Assembly.Part part } => part.Material,
        _ => null,
    };

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
        (Left is CarriedThing.Worked left && left.Thing == held) || (Right is CarriedThing.Worked right && right.Thing == held);

    private bool Holds(CarriedThing left, CarriedThing right) =>
        (left, right) switch
        {
            // The same stock entry twice - two sticks lashed together - needs enough for both.
            (CarriedThing.Stock a, CarriedThing.Stock b) when a.Kind == b.Kind => Person.Inventory.Get(a.Kind) >= a.Amount + b.Amount,
            _ => Holds(left) && Holds(right),
        };

    private bool Holds(CarriedThing target) => target switch
    {
        CarriedThing.Stock stock => Person.Inventory.Get(stock.Kind) >= stock.Amount,
        CarriedThing.Worked worked => Person.Inventory.Assemblies.Contains(worked.Thing),
        _ => false,
    };

    // Raw stock becomes a part of the new object as it is taken: unworked, so its soundness is
    // its substance's and nothing more (Assembly.Durability multiplies quality in).
    private Assembly TakeFromPack(CarriedThing target, WorldState world)
    {
        switch (target)
        {
            case CarriedThing.Stock stock:
                var definition = world.Configuration.ItemCatalog.Get(stock.Kind);
                Person.Inventory.Remove(stock.Kind, stock.Amount);
                return new Assembly.Part(definition.Material, definition.Form, UnworkedQuality, definition.Volume * stock.Amount);

            case CarriedThing.Worked worked:
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
