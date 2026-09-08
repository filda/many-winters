using Godot;
using ManyWinters.Godot.Views;

namespace ManyWinters.Godot.Interaction;

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
//
// The re-cast runs the full length of the camera's view, not just a step past the original
// miss - it used to end 1 m behind the first box's front face, and a tree's box is as deep
// as its canopy is wide, so anything standing behind it (and the ground itself) was out of
// reach and the whole rescue silently came back empty. The ground is where it stops: the
// terrain's StaticBody3D is the only body-type collider in the scene, and nothing behind the
// ground can be what the cursor is over.
public static class HoverRescue
{
    // Only ever a handful of real candidates plausibly overlap at one exact screen point -
    // this is just a safety cap against an unexpected pathological stack, not a tuned budget.
    private const int MaxAttempts = 8;

    // Returns whether anything along the ray beyond the original miss turned out to be under
    // the cursor for real (and has taken the highlight). False means there is nothing here at
    // all - bare ground - which is the caller's cue to put out whatever was still lit.
    public static bool TryHoverElsewhere(CollisionObject3D missedCollider, Camera3D camera, Vector3 missedPosition) =>
        TryElsewhere(missedCollider, camera, missedPosition, (view, cam, pos) =>
            view is SpriteEntityView entity && entity.TryHoverAt(cam, pos));

    // Returns true if something along the ray beyond the original miss turned out to actually
    // be there (and has already had its own click handler invoked) - the caller only needs to
    // fall back to a plain ground-click order when this comes back false.
    // Which buttons a given kind of entity answers to is its own business (SpriteEntityView's
    // WantsClick), not something this has to know per view type.
    public static bool TryClickElsewhere(CollisionObject3D missedCollider, Camera3D camera, Vector3 missedPosition, MouseButton button) =>
        TryElsewhere(missedCollider, camera, missedPosition, (view, cam, pos) =>
            view is SpriteEntityView entity && entity.TryClickAt(cam, pos, button));

    private static bool TryElsewhere(CollisionObject3D missedCollider, Camera3D camera, Vector3 missedPosition, Func<CollisionObject3D, Camera3D, Vector3, bool> tryHandle)
    {
        var spaceState = missedCollider.GetWorld3D().DirectSpaceState;
        // Back through the screen, not straight from the camera's own position toward the
        // miss - that only describes a perspective camera's pick ray. Projecting from the
        // miss's screen point stays right after FreeCameraRig.ToggleProjection too.
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
