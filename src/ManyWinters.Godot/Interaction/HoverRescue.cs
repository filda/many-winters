using Godot;
using ManyWinters.Godot.Views;

namespace ManyWinters.Godot.Interaction;

// Godot's picking delivers an input event only to the nearest collider on the ray - whichever
// broad-phase box is closest, not necessarily the sprite the cursor is visually over (boxes of
// densely packed ResourceNodes overlap constantly, see MapLoader.ScatterDecorations). When that
// collider's pixel check fails, the cursor may still be over another entity's opaque pixels, so
// this re-casts the same ray, excluding what has been ruled out, until something opaque is found
// or nothing is left. Shared by hover (TryHoverElsewhere) and clicks (TryClickElsewhere) so both
// agree on what is at a given point.
//
// The re-cast runs the full length of the view, not a step past the miss: a tree's box is as
// deep as its canopy is wide, so a short step never reaches what stands behind it. The ground
// stops it - the terrain's StaticBody3D is the only body-type collider, and nothing behind the
// ground can be under the cursor.
public static class HoverRescue
{
    // A safety cap against a pathological stack, not a tuned budget; a handful overlap at most.
    private const int MaxAttempts = 8;

    // True if something beyond the original miss is really under the cursor (and now holds the
    // highlight); false means bare ground, the caller's cue to put out whatever is still lit.
    public static bool TryHoverElsewhere(CollisionObject3D missedCollider, Camera3D camera, Vector3 missedPosition) =>
        TryElsewhere(missedCollider, camera, missedPosition, (view, cam, pos) =>
            view is SpriteEntityView entity && entity.TryHoverAt(cam, pos));

    // True if something beyond the original miss is really there (its click handler has already
    // run); the caller falls back to a ground-click order only on false. Which buttons a view
    // answers is its own business (SpriteEntityView.WantsClick).
    public static bool TryClickElsewhere(CollisionObject3D missedCollider, Camera3D camera, Vector3 missedPosition, MouseButton button) =>
        TryElsewhere(missedCollider, camera, missedPosition, (view, cam, pos) =>
            view is SpriteEntityView entity && entity.TryClickAt(cam, pos, button));

    private static bool TryElsewhere(CollisionObject3D missedCollider, Camera3D camera, Vector3 missedPosition, Func<CollisionObject3D, Camera3D, Vector3, bool> tryHandle)
    {
        var spaceState = missedCollider.GetWorld3D().DirectSpaceState;
        // Re-projected through the miss's screen point, not cast from the camera position toward
        // the miss: that only describes a perspective pick ray (FreeCameraRig.ToggleProjection).
        var screenPosition = camera.UnprojectPosition(missedPosition);
        var origin = camera.ProjectRayOrigin(screenPosition);
        var direction = camera.ProjectRayNormal(screenPosition);
        var excluded = new global::Godot.Collections.Array<Rid> { missedCollider.GetRid() };

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var query = PhysicsRayQueryParameters3D.Create(origin, origin + (direction * camera.Far));
            query.Exclude = excluded;
            query.CollideWithAreas = true;
            query.CollideWithBodies = true;

            var result = spaceState.IntersectRay(query);
            if (result.Count == 0
                || result["collider"].AsGodotObject() is not CollisionObject3D collider
                || collider is StaticBody3D)
            {
                return false;
            }

            excluded.Add(collider.GetRid());
            var hitPosition = result["position"].AsVector3();

            if (tryHandle(collider, camera, hitPosition))
            {
                return true;
            }
        }

        return false;
    }
}
