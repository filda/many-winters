using System;
using Godot;

namespace ManyWinters.Godot;

// Godot's physics picking only ever delivers an input event to the single nearest collider
// along the ray - whichever entity's broad-phase bounding box happens to be closest to the
// camera there, not necessarily whichever sprite the cursor visually looks like it's over (two
// nearby entities' boxes overlapping is common now that decorations are real, densely-packed
// ResourceNodes - see MapLoader.ScatterDecorations). When that nearest collider's own
// pixel-perfect check comes back negative, the cursor might still genuinely be over some
// *other* nearby entity's opaque pixels - a person standing right behind a mushroom, or a
// resource behind another resource - so this re-casts the same ray, excluding whatever's
// already been ruled out, until something actually opaque is found or there's nothing left to
// check. Used for both hover (TryHoverElsewhere) and clicks (TryClickElsewhere) - they used to
// have two different, inconsistent fallbacks (this one for hover, a plain world-space nearby
// search in Main.OnMissedClick for clicks); one shared mechanism means a click and a hover at
// the exact same point always agree on what's actually there.
public static class HoverRescue
{
    // Only ever a handful of real candidates plausibly overlap at one exact screen point -
    // this is just a safety cap against an unexpected pathological stack, not a tuned budget.
    private const int MaxAttempts = 8;

    public static void TryHoverElsewhere(CollisionObject3D missedCollider, Camera3D camera, Vector3 missedPosition) =>
        TryElsewhere(missedCollider, camera, missedPosition, (view, cam, pos) => view switch
        {
            PersonView personView => personView.TryHoverAt(cam, pos),
            ResourceNodeView resourceView => resourceView.TryHoverAt(cam, pos),
            _ => false,
        });

    // Returns true if something along the ray beyond the original miss turned out to actually
    // be there (and has already had its own click handler invoked) - the caller only needs to
    // fall back to a plain ground-click order when this comes back false.
    public static bool TryClickElsewhere(CollisionObject3D missedCollider, Camera3D camera, Vector3 missedPosition, MouseButton button) =>
        TryElsewhere(missedCollider, camera, missedPosition, (view, cam, pos) => view switch
        {
            PersonView personView => personView.TryClickAt(cam, pos, button),
            ResourceNodeView resourceView when button == MouseButton.Left => resourceView.TryClickAt(cam, pos),
            GraveView graveView when button == MouseButton.Left => graveView.TryClickAt(cam, pos),
            _ => false,
        });

    private static bool TryElsewhere(CollisionObject3D missedCollider, Camera3D camera, Vector3 missedPosition, Func<CollisionObject3D, Camera3D, Vector3, bool> tryHandle)
    {
        var spaceState = missedCollider.GetWorld3D().DirectSpaceState;
        var origin = camera.GlobalPosition;
        var excluded = new global::Godot.Collections.Array<Rid> { missedCollider.GetRid() };

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var direction = (missedPosition - origin).Normalized();
            var query = PhysicsRayQueryParameters3D.Create(origin, origin + (direction * (origin.DistanceTo(missedPosition) + 1f)));
            query.Exclude = excluded;
            query.CollideWithAreas = true;
            query.CollideWithBodies = true;

            var result = spaceState.IntersectRay(query);
            if (result.Count == 0 || result["collider"].AsGodotObject() is not CollisionObject3D collider)
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
