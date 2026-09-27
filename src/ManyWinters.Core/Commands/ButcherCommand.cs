using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Taking apart a dead carcass, in the exact pattern of GatherCommand (docs/todo/fauna-plan.md,
// phase 3, "Rozhodnutí předem" item 4): a skill with a base technique that must be taught before
// anything can be taken at all, and an efficient one earned through practice that gets more out
// of the same source - here, more of the carcass rather than more per gather. Butcher is always
// a Person: an animal never butchers anything (no species grants basic_butchering innately).
public sealed record ButcherCommand(Person Butcher, Animal Carcass) : ICommand
{
    public static readonly SkillTypeId Skill = new("butchering");

    // Public: HuntCommand and WorldState.DecideIdleTask both ask "does this carcass hold meat"
    // (docs/todo/fauna-plan.md phase 3) without needing a second, private copy of the id.
    public static readonly ItemKindId Meat = new("meat");
    private static readonly ItemKindId Hide = new("hide");
    private static readonly ItemKindId Sinew = new("sinew");
    private static readonly ItemKindId Bone = new("bone");

    private const float SkillGainPerButchering = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // Stated in tries, not as a level: the practice curve is not linear (see Skills.Increase).
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Butcher.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        // "Still alive" reads as the same refusal LootCommand gives a looter standing over
        // somebody who is not dead yet.
        if (Carcass.IsAlive)
        {
            return ActionBlocker.TargetIsAlive;
        }

        if (Carcass.Inventory.Counts.Count == 0)
        {
            return ActionBlocker.NothingLeft;
        }

        // A carcass lies on the ground like a pile, not a node or a building - the tighter reach
        // (EatFromPileCommand, LootCommand does not apply here since a corpse without hands is
        // never the actor).
        if (WorldState.Distance(Butcher.Position, Carcass.Position) > world.Configuration.Rules.PileReachDistance)
        {
            return ActionBlocker.TooFar;
        }

        var hasEfficientTechnique = Butcher.KnownTechniques.Contains(world.Configuration.SkillCatalog.Get(Skill).EfficientTechnique);
        if (!CanTakeAnythingFrom(world, Butcher, Carcass, hasEfficientTechnique))
        {
            return ActionBlocker.InventoryFull;
        }

        // Never self-taught (see SkillDefinition.BaseTechnique): a person who was never shown
        // how to butcher takes nothing off a dead deer, however hungry they are. Asked last, like
        // every knowledge gate (see ActionBlocker.NotLearned).
        var skillDefinition = world.Configuration.SkillCatalog.Get(Skill);
        return Butcher.KnownTechniques.Contains(skillDefinition.BaseTechnique)
            ? ActionBlocker.None
            : ActionBlocker.NotLearned;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        var skillDefinition = world.Configuration.SkillCatalog.Get(Skill);
        var hasEfficientTechnique = Butcher.KnownTechniques.Contains(skillDefinition.EfficientTechnique);

        foreach (var item in ItemsInOrder(hasEfficientTechnique))
        {
            var available = Carcass.Inventory.Get(item);
            if (available <= 0)
            {
                continue;
            }

            var taken = Butcher.Inventory.AddUpToCapacity(item, available, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Butcher));
            // Stryker disable once Equality: removing zero units leaves the count exactly as it
            // was, so skipping the call and making it are indistinguishable
            if (taken > 0)
            {
                Carcass.Inventory.Remove(item, taken);
            }
        }

        Butcher.Skills.Increase(Skill, SkillGainPerButchering);
        if (Butcher.Skills.Get(Skill) >= DiscoveryThreshold)
        {
            Butcher.KnownTechniques.Add(skillDefinition.EfficientTechnique);
        }
    }

    // Whether anything at all would come off this carcass into this butcher's pack - asked
    // before WorldState would send someone walking to one, the same question GatherCommand asks
    // of a resource node.
    private static bool CanTakeAnythingFrom(WorldState world, Person butcher, Animal carcass, bool hasEfficientTechnique) =>
        ItemsInOrder(hasEfficientTechnique).Any(item =>
            carcass.Inventory.Get(item) > 0
            && butcher.Inventory.HasRoomFor(item, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(butcher)));

    // Meat and bone come off any carcass a taught butcher touches; hide and sinew are worth
    // ruining in untrained hands, so only efficient_butchering's practiced grip takes them
    // (docs/todo/fauna-plan.md: "beginner ruins them - this is what the efficient technique
    // buys").
    private static IReadOnlyList<ItemKindId> ItemsInOrder(bool hasEfficientTechnique) =>
        hasEfficientTechnique ? [Meat, Hide, Sinew, Bone] : [Meat, Bone];
}
