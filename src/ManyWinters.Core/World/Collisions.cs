using ManyWinters.Core.Population;

namespace ManyWinters.Core.World;

public static class Collisions
{
    // MoveTask/IdleTask aim at a destination with no awareness of what else is there, so this
    // untangles the overlap afterwards, every tick (O(n^2)). Every
    // living creature, person or animal - a deer and a person are pushed apart using their own
    // species' radii rather than one shared constant. Separations are computed against
    // start-of-tick positions and summed into one clamped push per creature, so discovery order
    // cannot bias the result.
    public static void Resolve(WorldState world)
    {
        var speciesCatalog = world.Configuration.SpeciesCatalog;
        var maxPushPerTick = world.Configuration.Rules.MaxCollisionPushPerTick;
        var resourceCatalog = world.Configuration.ResourceCatalog;
        var creatures = world.People.Cast<Creature>().Concat(world.Animals).Where(creature => creature.IsAlive).ToList();
        var radii = creatures.Select(creature => speciesCatalog.Get(creature.Species).CollisionRadius).ToList();
        var pushes = new (double X, double Y)[creatures.Count];

        for (var i = 0; i < creatures.Count; i++)
        {
            for (var j = i + 1; j < creatures.Count; j++)
            {
                if (!TrySeparation(creatures[i].Position, creatures[j].Position, radii[i] + radii[j], out var pushX, out var pushY))
                {
                    continue;
                }

                pushes[i] = (pushes[i].X + (pushX / 2), pushes[i].Y + (pushY / 2));
                pushes[j] = (pushes[j].X - (pushX / 2), pushes[j].Y - (pushY / 2));
            }
        }

        for (var i = 0; i < creatures.Count; i++)
        {
            var creature = creatures[i];
            var (pushX, pushY) = pushes[i];
            // Only what stands close enough to touch: nothing further than both radii at their
            // widest can push.
            foreach (var entity in world.EntitiesWithin(creature.Position, radii[i] + resourceCatalog.MaxCollisionRadius))
            {
                if (entity.Growth is not { IsAlive: true })
                {
                    continue;
                }

                var collisionRadius = resourceCatalog.Get(entity.Kind).CollisionRadius;
                if (collisionRadius <= 0f
                    || !TrySeparation(creature.Position, entity.Position, radii[i] + collisionRadius, out var nodePushX, out var nodePushY))
                {
                    continue;
                }

                pushX += nodePushX;
                pushY += nodePushY;
            }

            ApplyClampedPush(creature, pushX, pushY, maxPushPerTick);
        }
    }

    private static void ApplyClampedPush(Creature creature, double pushX, double pushY, float maxPushPerTick)
    {
        var magnitude = Math.Sqrt((pushX * pushX) + (pushY * pushY));
        // Stryker disable once Equality,Statement,Block: falling through adds a zero push and lands on the same spot
        if (magnitude <= 0.0)
        {
            return;
        }
        // Stryker disable once Equality: at exactly the cap the scale is 1, so clamping changes nothing
        if (magnitude > maxPushPerTick)
        {
            var scale = maxPushPerTick / magnitude;
            pushX *= scale;
            pushY *= scale;
        }

        creature.Position = new Position(creature.Position.X + pushX, creature.Position.Y + pushY);
    }

    // A true result moves `a` away from `b` by (pushX, pushY); `b` gets the negation, wherever
    // the caller applies it. False once far enough apart. Coincident positions fall back to a
    // fixed direction rather than staying stuck together.
    private static bool TrySeparation(Position a, Position b, float minDistance, out double pushX, out double pushY)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        // Stryker disable once Equality: at exactly the minimum the overlap is zero, so either branch stands still
        if (distance >= minDistance)
        {
            pushX = 0;
            pushY = 0;
            // Stryker disable once Boolean: both out parameters are zero here, so either answer leaves every position as it was
            return false;
        }

        var overlap = minDistance - distance;
        // Stryker disable once Equality: two positions exactly this far apart has probability zero
        if (distance < 0.0001)
        {
            pushX = overlap;
            pushY = 0;
            return true;
        }

        pushX = dx / distance * overlap;
        pushY = dy / distance * overlap;
        return true;
    }
}
