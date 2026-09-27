using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// A thrown attempt at a deer within reach: always a Person at a living Animal, gated only on
// knowledge - bare hands are allowed at HuntingBaseHitChance, so there is no MissingTool blocker
// the way FellCommand has one. A hit kills the prey outright and fills its carcass with the same
// yield a death from hunger or old age gets; a miss sends it fleeing even from beyond its own
// FleeDistance, because it saw the throw.
public sealed record HuntCommand(Person Hunter, Animal Prey) : ICommand
{
    public static readonly SkillTypeId Skill = new("hunting");

    private const float SkillGainPerAttempt = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // Stated in tries, not as a level: the practice curve is not linear.
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    // Nothing scales past this - even a master hunter with the best tool in the game misses one
    // throw in ten.
    private const float MaxHitChance = 0.9f;

    public ActionBlocker Blocker(WorldState world)
    {
        if (!Hunter.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        // TargetIsGone rather than TargetIsDead: the same "nothing left to act on" refusal
        // GatherCommand gives a depleted node, not the "still alive" refusal ButcherCommand gives
        // a living deer someone tries to butcher.
        if (!Prey.IsAlive)
        {
            return ActionBlocker.TargetIsGone;
        }

        if (WorldState.Distance(Hunter.Position, Prey.Position) > world.Configuration.Rules.HuntingRange)
        {
            return ActionBlocker.TooFar;
        }

        // Never self-taught: a person nobody ever showed how to hunt does not throw at all,
        // however close the deer stands. No tool blocker above this - a bare-handed throw is
        // allowed, just a nearly hopeless one. Asked last, like every knowledge gate.
        var skillDefinition = world.Configuration.SkillCatalog.Get(Skill);
        return Hunter.KnownTechniques.Contains(skillDefinition.BaseTechnique)
            ? ActionBlocker.None
            : ActionBlocker.NotLearned;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        var rules = world.Configuration.Rules;
        var skillDefinition = world.Configuration.SkillCatalog.Get(Skill);
        var hasEfficientTechnique = Hunter.KnownTechniques.Contains(skillDefinition.EfficientTechnique);

        // A sharp stone hafted on a stick is scored the same way an axe is - it is a spear as
        // much as an axe until form recognition can tell the two apart (see
        // docs/materials-and-crafting-architecture.md section 8).
        var toolScore = Hunter.Inventory.BestChoppingScore(world.Configuration.ItemCatalog);
        var chance = rules.HuntingBaseHitChance + (toolScore * rules.HuntingHitChancePerToolScore);
        if (hasEfficientTechnique)
        {
            chance *= rules.HuntingEfficientMultiplier;
        }

        chance = Math.Min(chance, MaxHitChance);

        if (PassesHuntRoll(Hunter, Prey, world.Clock.CurrentTick, chance))
        {
            Prey.IsAlive = false;
            Prey.DeathTick = world.Clock.CurrentTick;
            Prey.CauseOfDeath = DeathCause.Hunted;
            world.FillCarcass(Prey);
        }
        else
        {
            // Seen the throw and bolts, however far the hunter actually stood - fleeing depends
            // on what triggers it, not on how far a miss is felt.
            if (world.Configuration.SpeciesCatalog.Get(Prey.Species).Flee is { } flee)
            {
                Prey.Tasks.Interrupt(new FleeTask(Hunter, flee));
            }
        }

        Hunter.Skills.Increase(Skill, SkillGainPerAttempt);
        if (Hunter.Skills.Get(Skill) >= DiscoveryThreshold)
        {
            Hunter.KnownTechniques.Add(skillDefinition.EfficientTechnique);
        }
    }

    // Deterministic from the pair and the tick, the same construction every roll in the game
    // uses: a throw is a roll over (hunter, prey, tick), not a shared Random, so a replay throws
    // the same way twice.
    private static bool PassesHuntRoll(Person hunter, Animal prey, long tick, float chance)
    {
        var mixed = unchecked((uint)(hunter.Id.Seed * 2654435761u) ^ (uint)(prey.Id.Seed * 40503) ^ ((uint)tick * 73856093u));

        // Stryker disable once Equality: NextDouble() returning exactly the chance has
        // probability zero, so < and <= are the same roll
        return new Random(SeedHash.Avalanche(mixed)).NextDouble() < chance;
    }
}
