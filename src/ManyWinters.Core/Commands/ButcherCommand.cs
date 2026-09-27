using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Taking apart a dead carcass, in the same pattern as GatherCommand: a skill with a base
// technique that must be taught before anything can be taken at all, and an efficient one earned
// through practice that gets more out of the same source - here, more of the carcass rather than
// more per gather. Butcher is always a Person: no species grants basic_butchering innately, so an
// animal never butchers anything.
public sealed record ButcherCommand(Person Butcher, Animal Carcass) : ICommand
{
    public static readonly SkillTypeId Skill = new("butchering");

    // Public: HuntCommand and WorldState.DecideIdleTask both ask "does this carcass hold meat"
    // without needing a second, private copy of the id.
    public static readonly ItemKindId Meat = new("meat");
    // Raw off the animal, not the tanned hide warm_clothing is made from - only rawhide spoils;
    // TanCommand turns one into the other.
    private static readonly ItemKindId Rawhide = new("rawhide");
    private static readonly ItemKindId Sinew = new("sinew");
    private static readonly ItemKindId Bone = new("bone");

    private const float SkillGainPerButchering = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // Stated in tries, not as a level: the practice curve is not linear.
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

        // A carcass lies on the ground like a pile, not a node or a building, so it uses the
        // same tighter reach - a corpse without hands is never the one acting.
        if (WorldState.Distance(Butcher.Position, Carcass.Position) > world.Configuration.Rules.PileReachDistance)
        {
            return ActionBlocker.TooFar;
        }

        var hasEfficientTechnique = Butcher.KnownTechniques.Contains(world.Configuration.SkillCatalog.Get(Skill).EfficientTechnique);
        if (!CanTakeAnythingFrom(world, Butcher, Carcass, hasEfficientTechnique))
        {
            return ActionBlocker.InventoryFull;
        }

        // Never self-taught: a person who was never shown how to butcher takes nothing off a
        // dead deer, however hungry they are. Asked last, like every knowledge gate.
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

            // A transfer, not a fresh Add: what was already rotting in the carcass keeps rotting
            // on the same clock in the butcher's pack.
            Carcass.Inventory.TransferUpToCapacity(item, available, Butcher.Inventory, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(Butcher));
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

    // Meat and bone come off any carcass a taught butcher touches; rawhide and sinew are worth
    // ruining in untrained hands, so only efficient_butchering's practiced grip takes them - a
    // beginner ruins them, which is what the efficient technique buys.
    private static IReadOnlyList<ItemKindId> ItemsInOrder(bool hasEfficientTechnique) =>
        hasEfficientTechnique ? [Meat, Rawhide, Sinew, Bone] : [Meat, Bone];
}
