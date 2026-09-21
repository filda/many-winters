using Godot;

namespace ManyWinters.Godot.Interaction;

// Where on the ground a screen position points, ignoring every entity's pickable collision box.
// Godot's picking delivers a click to the nearest collider on the ray, and every view's box is
// far bigger than its drawn silhouette: a click into a conifer box's transparent corner arrives
// with the hit point on that box, meters up and shifted toward the camera - tens of meters off
// the ground under the cursor at a shallow tilt. Every view is an Area3D and the only body is
// TerrainRenderer's ground StaticBody3D, so a ray that skips areas can only land on the ground.
//
// ProjectRayOrigin/ProjectRayNormal rather than camera position plus a direction: that
// reconstruction only fits a perspective camera and breaks in orthographic projection.
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
