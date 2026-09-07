using Godot;

namespace ManyWinters.Godot.Interaction;

// Where on the actual ground a screen position points at, ignoring every entity's pickable
// collision box along the way. Godot's own physics picking delivers a click only to the
// nearest collider on the ray, and every tree/person/grave carries an invisible bounding
// box far bigger than its drawn silhouette (see SpriteVisibleExtent) - a click into the
// transparent corner of a conifer's box used to fall through to a ground-click order with
// the ray's hit point *on that box*, several meters up in the air and shifted toward the
// camera, so the derived X/Z landed nowhere near the ground the cursor was visibly over
// (at a shallow camera tilt the miss was tens of meters). The only body-type collider in
// the scene is TerrainRenderer's ground StaticBody3D; every view is an Area3D, so a ray
// that skips areas can only ever land on the ground itself.
//
// ProjectRayOrigin/ProjectRayNormal rather than the camera's own position and a direction
// toward some world point - that reconstruction is only right for a perspective camera,
// while these stay correct after FreeCameraRig.ToggleProjection switches to orthographic.
public static class GroundPick
{
    public static Vector3? FindGround(Camera3D camera, Vector2 screenPosition)
    {
        var origin = camera.ProjectRayOrigin(screenPosition);
        var direction = camera.ProjectRayNormal(screenPosition);
        var query = PhysicsRayQueryParameters3D.Create(origin, origin + (direction * camera.Far));
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = camera.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return result.Count == 0 ? null : result["position"].AsVector3();
    }
}
