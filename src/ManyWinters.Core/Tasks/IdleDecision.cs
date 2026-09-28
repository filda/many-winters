using ManyWinters.Core.Commands;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

public static class IdleDecision
{
    // An empty queue means "use a known skill, or seek food if hungry and empty-handed", falling
    // back to wandering. IdleGraceUntilTick buys a few ticks of standing still, but never past
    // urgent hunger: the grace is renewed every tick while a person is selected, so a hungry one
    // would otherwise never set off for food.
    public static void Reconsider(WorldState world, Creature creature, long currentTick)
    {
        var idleGraceHolds = currentTick < creature.IdleGraceUntilTick && !NeedsToSeekFoodUrgently(world, creature);
        if (!idleGraceHolds && ShouldReconsiderIdleTask(world, creature))
        {
            var decidedTask = DecideIdleTask(world, creature);
            if (!KeepsCurrentTask(creature.Tasks.Current, decidedTask))
            {
                creature.Tasks.Interrupt(decidedTask);
            }
        }
    }

    // Only autonomous tasks are revisited; a player-issued one (MoveTask from MoveCommand) is
    // left alone. IdleTask always gets a second look. GatherTask only once its target stops
    // being worth working - re-planning every tick would re-approach the same resource forever -
    // or when hunger becomes urgent, so a wood run far from camp can be abandoned for food.
    private static bool ShouldReconsiderIdleTask(WorldState world, Creature creature) => creature.Tasks.Current switch
    {
        null => true,
        IdleTask => true,
        // FollowTask never completes, so this is what notices an infant has been weaned.
        FollowTask => true,
        // Reconsidered every tick like IdleTask/FollowTask, but the flee check below hands back
        // the very same FleeTask while it is still running rather than re-deriving completion
        // from FleeDistance, which would cut the flee short the moment the gap merely passes
        // FleeDistance on the way out to the wider SafeDistance.
        FleeTask => true,
        // A threat closing in is worth dropping a gather order for, same as urgent hunger.
        GatherTask gather => !IsWorthTakingFrom(world, creature, gather.Target) || NeedsToSeekFoodUrgently(world, creature) || NearbyThreatTo(world, creature) is not null,
        // The prey died (to this hunter or anyone else), wandered out of the search radius, or
        // hunger is (still) urgent - mirroring GatherTask's own NeedsToSeekFoodUrgently branch
        // above, which re-derives the best option every tick while hungry rather than committing
        // to one target. Deliberately still HungerSeekFoodThreshold here, not WouldEatIfTheyCould's
        // lower one: re-deriving from the lower threshold thrashes rather than helps, since a
        // hunter closing on one deer keeps getting handed whichever different deer is now
        // nearest (the herd scatters as the hunter's own approach spooks it, so "nearest" keeps
        // changing faster than any one chase can finish), which starves them worse than
        // committing to one target between hunger 25 and 50 would. Below 50 a freshly installed
        // hunt is left to run uninterrupted rather than re-picking a target on every tick; above
        // 50 this re-derives as before, and a hungry hunter who has since come to carry food is
        // dropped here rather than hunting on for more.
        HuntTask hunt => !hunt.Prey.IsAlive
            || WorldState.Distance(creature.Position, hunt.Prey.Position) > world.Configuration.Rules.IdleSearchRadius
            || creature.Needs.Hunger >= world.Configuration.Rules.HungerSeekFoodThreshold,
        // No *meat* left, specifically - not "the carcass is totally empty": a carcass a
        // beginner stripped of meat but left hide and sinew on still has a nonzero inventory,
        // and a hungry butcher parked beside it forever would starve next to something that can
        // no longer feed them. No hunger term here either, for the same reason HuntTask above
        // stays off WouldEatIfTheyCould: a carcass does not move, so there is less to gain from
        // re-deriving mid-walk, and nothing to lose by not doing so.
        ButcherTask butcher => butcher.Carcass.Inventory.Get(ButcherCommand.Meat) <= 0,
        _ => false,
    };

    // Whether the freshly decided autonomous task is the one already running, so it is dropped
    // rather than installed. IdleTask carries per-instance state (anchor, leg, pause) that
    // replacing it every tick would throw away; churning a FollowTask for the same mother is
    // pointless. A change of task type always interrupts. FleeTask keeps running against the same
    // threat rather than restarting every tick - restarting would not change anything (Advance
    // recomputes the direction every tick regardless), but it would be needless churn.
    private static bool KeepsCurrentTask(CreatureTask? current, CreatureTask decided) => (current, decided) switch
    {
        (IdleTask, IdleTask) => true,
        (FollowTask running, FollowTask fresh) => ReferenceEquals(running.Target, fresh.Target),
        (FleeTask running, FleeTask fresh) => ReferenceEquals(running.Threat, fresh.Threat),
        (HuntTask running, HuntTask fresh) => ReferenceEquals(running.Prey, fresh.Prey),
        (ButcherTask running, ButcherTask fresh) => ReferenceEquals(running.Carcass, fresh.Carcass),
        _ => false,
    };

    private static bool NeedsToSeekFoodUrgently(WorldState world, Creature creature) =>
        creature.Needs.Hunger >= world.Configuration.Rules.HungerSeekFoodThreshold
        && KnowsHowToEat(world, creature)
        && !HasEdibleFood(world, creature);

    // The same "hungry, knows how to eat, empty-handed" test as NeedsToSeekFoodUrgently, at
    // HungerEatThreshold rather than HungerSeekFoodThreshold - "would eat if they had something"
    // rather than "must go find something now". Used only for the two animal food steps below: a
    // hunt is a long trip - closing on prey, then walking to and butchering the carcass - that a
    // person already committed to gathering nearby plant food or a pile would not be worth
    // interrupting for, but is worth setting out on well before hunger becomes urgent, unlike a
    // two-step trip to a nearby tree. Gathering plant food and piles keep
    // NeedsToSeekFoodUrgently's higher threshold unchanged - that pacing is a separate decision
    // the family milestone depends on.
    private static bool WouldEatIfTheyCould(WorldState world, Creature creature) =>
        creature.Needs.Hunger >= world.Configuration.Rules.HungerEatThreshold
        && KnowsHowToEat(world, creature)
        && !HasEdibleFood(world, creature);

    private static bool KnowsHowToEat(WorldState world, Creature creature) =>
        world.Configuration.SkillCatalog.Find(EatCommand.Skill) is { } eating && creature.KnownTechniques.Contains(eating.BaseTechnique);

    private static bool IsWorthGathering(WorldState world, Creature creature, Entity entity) =>
        entity.Growth is { IsAlive: true, RemainingAmount: > 0f } growth
        && GatherCommand.CanTakeAnythingFrom(world, creature, world.Configuration.ResourceCatalog.Get(entity.Kind), growth.RemainingAmount);

    // A pile is only ever a GatherTask target for a meal, so once the meal is eaten there is
    // nothing left to stand beside it for.
    private static bool IsWorthTakingFrom(WorldState world, Creature creature, Entity entity) =>
        entity.Category == EntityCategory.Pile
            ? IsFoodPile(world, creature, entity) && world.IsHungryEnoughToEat(creature)
            : IsWorthGathering(world, creature, entity);

    // Food for a given creature about to be sent there, not food in general: a pile of the same
    // material that creature's species cannot digest is not worth the walk.
    private static bool IsFoodPile(WorldState world, Creature creature, Entity entity) =>
        entity is { Category: EntityCategory.Pile, StaticAmount: > 0 }
        && world.HungerRestoredPerUnitFor(creature, EatFromPileCommand.FoodOf(entity)) > 0f;

    // "Idle" means "use a known skill, or seek food if hungry and empty-handed"; plain wandering
    // (IdleTask) is the fallback. Hunger wins over a known skill. A search centres on the
    // creature's own HomeRange anchor when it has one (an animal) rather than on where it
    // happens to be standing (a person, today).
    private static CreatureTask DecideIdleTask(WorldState world, Creature creature)
    {
        var reachDistance = world.Configuration.Rules.MaxInteractionDistance;
        var searchOrigin = creature.Home?.Anchor ?? creature.Position;

        // A threat wins over everything else, including an infant's own mother and hunger: a
        // species that never notices a person standing next to it reads as broken. Species data,
        // not a type check on the creature - a human never has one.
        if (world.Configuration.SpeciesCatalog.Get(creature.Species).Flee is { } flee)
        {
            // Already running from something and not yet clear of it - hand back the same instance
            // rather than re-deriving from FleeDistance, or a flee already under way would be cut
            // short the moment the gap merely passes FleeDistance on the way out to the wider
            // SafeDistance.
            if (creature.Tasks.Current is FleeTask activeFlee)
            {
                return activeFlee;
            }

            if (NearbyThreatTo(world, creature) is { } threat)
            {
                return new FleeTask(threat, flee);
            }
        }

        // An infant has no skill and nothing to gather, so it keeps up with its mother instead -
        // that is what feeds it and what keeps it within teaching reach. An orphan falls through
        // and wanders like anybody else; nothing here saves it, and nothing should.
        if (world.LifeStageOf(creature) == LifeStage.Infant && creature.NursingMother is { IsAlive: true } mother)
        {
            return new FollowTask(mother, reachDistance, world.Configuration.Rules.InfantFollowSpeedPerTick);
        }
        // Without knowing how to eat, gathering food would not help, so this falls through to
        // the general search below.
        if (NeedsToSeekFoodUrgently(world, creature))
        {
            // A food resource this creature never learned to gather is as unreachable as none,
            // but food somebody put down needs no skill to take. Nearest wins.
            var foodNode = FindNearestGatherableEntity(world, creature, searchOrigin, definition => IsFoodResource(world, creature, definition) && IsKnownSkill(world, creature, definition.Skill));
            var food = NearerOf(searchOrigin, foodNode, FindNearestFoodPile(world, creature, searchOrigin));
            if (food is not null)
            {
                // A pile is taken from at the tighter PileReachDistance, so the walk has to end
                // there too, or the creature would stop at the wider tree/building reach and
                // never get close enough to take anything.
                var reach = food.Category == EntityCategory.Pile ? world.Configuration.Rules.PileReachDistance : reachDistance;
                return new GatherTask(food, reach);
            }
        }

        // The two animal food steps trigger earlier, at WouldEatIfTheyCould's lower threshold: a
        // hunt is a long trip, worth setting out on well before hunger turns urgent, unlike the
        // plant food/pile search just above (which keeps the higher threshold on purpose). Still
        // tried only once the plant food/pile search above has come up with nothing, and
        // butchering before hunting when both are known: a carcass already on the ground is a
        // meal without the risk of a miss, and wiping out a whole hunt's worth of throws over a
        // herd that already has food lying around would be busywork.
        if (WouldEatIfTheyCould(world, creature))
        {
            if (IsKnownSkill(world, creature, ButcherCommand.Skill) && FindNearestDeadAnimalWithMeat(world, searchOrigin) is { } carcass)
            {
                return new ButcherTask(carcass, world.Configuration.Rules.PileReachDistance, GatherTask.SpeedPerTick, world.Configuration.Rules.ApproachFractionOfReach);
            }

            if (IsKnownSkill(world, creature, HuntCommand.Skill) && FindNearestHuntablePrey(world, searchOrigin) is { } prey)
            {
                return new HuntTask(prey, world.Configuration.Rules.HuntingRange, GatherTask.SpeedPerTick);
            }
        }

        // Nearest wins regardless of which known skill it needs. IsKnownSkill checks the skill's
        // BaseTechnique, since KnownTechniques holds arbitrary techniques rather than skills.
        var node = FindNearestGatherableEntity(world, creature, searchOrigin, definition => IsKnownSkill(world, creature, definition.Skill));
        if (node is not null)
        {
            return new GatherTask(node, reachDistance);
        }

        // Null for a creature with no home (every Person today), exactly IdleTask's own default;
        // an Animal's home range is what its wander legs and radius come from instead.
        return new IdleTask(creature.Home);
    }

    private static bool IsKnownSkill(WorldState world, Creature creature, SkillTypeId skill)
    {
        var definition = world.Configuration.SkillCatalog.Find(skill);
        return definition is not null && creature.KnownTechniques.Contains(definition.BaseTechnique);
    }

    // Null for a species with no Flee (every human), or one with Flee but nobody living within
    // FleeDistance right now. The nearest living person, not merely "any" - so a herd scattered
    // beside several people always flees the closer one, which is also who KeepsCurrentTask
    // compares against to avoid restarting a flee that is already running from the same threat.
    private static Person? NearbyThreatTo(WorldState world, Creature creature)
    {
        if (world.Configuration.SpeciesCatalog.Get(creature.Species).Flee is not { } flee)
        {
            return null;
        }

        var nearest = world.People.Where(p => p.IsAlive).MinBy(p => WorldState.Distance(creature.Position, p.Position));
        return nearest is not null && WorldState.Distance(creature.Position, nearest.Position) < flee.FleeDistance ? nearest : null;
    }

    // Food for a given creature, not food in general, the same distinction IsFoodPile makes.
    private static bool IsFoodResource(WorldState world, Creature creature, ResourceDefinition definition) =>
        definition.YieldsItem is { } item && world.HungerRestoredPerUnitFor(creature, item) > 0f;

    private static bool HasEdibleFood(WorldState world, Creature creature) =>
        creature.Inventory.Counts.Any(kv => kv.Value > 0 && world.HungerRestoredPerUnitFor(creature, kv.Key) > 0f);

    // Depleted-but-alive nodes (RemainingAmount 0, regenerating) are skipped - a fuller one of
    // the same kind is normally nearby - and so is anything this creature could not take from:
    // nobody walks to a source to gather nothing. `origin` only bounds the *fallback* search's
    // reach - the creature's own position for a Person, its HomeRange anchor for an Animal with
    // nothing matching inside its home - the *nearest* pick in both tiers is always
    // nearest-to-the-creature-itself, not to the anchor.
    //
    // An animal with a Home searches nearest-to-itself among nodes bounded by its Home (radius
    // plus a small margin for its own footprint), not nearest-to-the-shared-anchor: the anchor
    // bounds where the herd may graze, it is not everybody's common destination. Picking nearest
    // to the anchor instead sent every member of a herd at the single node nearest that one point,
    // where collision resolution then kept most of them outside MaxInteractionDistance and
    // nobody ate.
    //
    // The in-home tier also only counts a node that can still give this creature a full harvest,
    // not merely IsWorthGathering's "more than zero left": a home tuft that regrew to a sliver
    // still "matched" the plain rule, so a herd converged on its own barely-regrown patch and
    // nibbled it at regen speed forever rather than falling through to fuller grass a little
    // further out. Only when nothing at home clears that bar does the search widen to the wider
    // IdleSearchRadius tier, at the any-amount-left rule - still picking whichever match is
    // nearest to the creature itself (not to the anchor), or a herd already scattered across its
    // own ground by the first tier would regroup on a single depleted tuft nearest the anchor the
    // moment it fell through to this one. For a Person (no Home) `origin` is its own position
    // anyway, so both tiers agree.
    private static Entity? FindNearestGatherableEntity(WorldState world, Creature creature, Position origin, Func<ResourceDefinition, bool> matches)
    {
        if (creature.Home is { } home)
        {
            var margin = world.Configuration.SpeciesCatalog.Get(creature.Species).CollisionRadius * 2f;
            var nearestAtHome = NearestGatherableEntity(
                world,
                creature,
                nearestTo: creature.Position,
                matches,
                inBounds: entity => WorldState.Distance(home.Anchor, entity.Position) <= home.Radius + margin && HasAFullHarvestFor(world, creature, entity));
            if (nearestAtHome is not null)
            {
                return nearestAtHome;
            }
        }

        return NearestGatherableEntity(world, creature, nearestTo: creature.Position, matches, inBounds: entity => WorldState.Distance(origin, entity.Position) <= world.Configuration.Rules.IdleSearchRadius);
    }

    // IsWorthGathering has already confirmed entity.Growth is alive by the time this runs
    // (NearestGatherableEntity checks it first), so Growth here is never null.
    private static bool HasAFullHarvestFor(WorldState world, Creature creature, Entity entity) =>
        GatherCommand.WouldYieldAFullHarvest(world, creature, world.Configuration.ResourceCatalog.Get(entity.Kind), entity.Growth!.RemainingAmount);

    private static Entity? NearestGatherableEntity(WorldState world, Creature creature, Position nearestTo, Func<ResourceDefinition, bool> matches, Func<Entity, bool> inBounds)
    {
        Entity? nearest = null;
        var nearestDistance = double.MaxValue;
        foreach (var entity in world.Entities)
        {
            if (!IsWorthGathering(world, creature, entity) || !matches(world.Configuration.ResourceCatalog.Get(entity.Kind)) || !inBounds(entity))
            {
                continue;
            }

            var distance = WorldState.Distance(nearestTo, entity.Position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = entity;
            }
        }

        return nearest;
    }

    private static Entity? FindNearestFoodPile(WorldState world, Creature creature, Position origin) =>
        world.Entities
            .Where(pile => IsFoodPile(world, creature, pile))
            .Where(pile => WorldState.Distance(origin, pile.Position) <= world.Configuration.Rules.IdleSearchRadius)
            .MinBy(pile => WorldState.Distance(origin, pile.Position));

    // A carcass worth walking to for its meat, nearest to `origin` first - "worth" meaning
    // ButcherCommand would actually find something, not merely that an animal died here once.
    // Only meat is asked about: a picked-clean carcass still holding hide or bone but no meat is
    // not a meal.
    private static Animal? FindNearestDeadAnimalWithMeat(WorldState world, Position origin) =>
        world.Animals
            .Where(animal => !animal.IsAlive && animal.Inventory.Get(ButcherCommand.Meat) > 0)
            .Where(animal => WorldState.Distance(origin, animal.Position) <= world.Configuration.Rules.IdleSearchRadius)
            .MinBy(animal => WorldState.Distance(origin, animal.Position));

    // Living prey worth a throw, nearest to `origin` first: a species whose carcass would hold
    // no meat at all is not worth hunting - nothing here checks FleeDistance or HuntingRange,
    // since HuntTask itself closes whatever gap remains.
    private static Animal? FindNearestHuntablePrey(WorldState world, Position origin) =>
        world.Animals
            .Where(animal => animal.IsAlive && world.Configuration.SpeciesCatalog.Get(animal.Species).Carcass.Any(yield => yield.Item == ButcherCommand.Meat && yield.Amount > 0))
            .Where(animal => WorldState.Distance(origin, animal.Position) <= world.Configuration.Rules.IdleSearchRadius)
            .MinBy(animal => WorldState.Distance(origin, animal.Position));

    private static Entity? NearerOf(Position origin, Entity? a, Entity? b) => (a, b) switch
    {
        (null, _) => b,
        (_, null) => a,
        _ => WorldState.Distance(origin, a.Position) <= WorldState.Distance(origin, b.Position) ? a : b,
    };
}
