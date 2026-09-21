using Godot;
using ManyWinters.Core.Population;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;
using ManyWinters.Godot.Views;

namespace ManyWinters.Godot.Interaction;

// A decoration sprite between the camera and the selected person would otherwise hide them
// with no way to tell where they went.
internal sealed class OcclusionFader(
    FreeCameraRig cameraRig,
    WorldPresenter presenter,
    PresentationSettings presentation)
{
    // Skips ComputeOccludingSprites (an O(sprite count) scan) on frames where neither the camera
    // nor the occlusion target moved: with the camera at rest and nobody selected walking, the
    // sight line - and so the occluding set - cannot have changed since last frame.
    private Vector3? _lastCameraPosition;
    private Vector3? _lastTargetPosition;
    private const float RecomputeDistanceSquaredThreshold = 0.0001f;

    public void Update(Person? selectedPerson)
    {
        var (targetPosition, selectedPersonNode) = ResolveOcclusionTarget(selectedPerson);
        var cameraPosition = cameraRig.CameraGlobalPosition;

        if (_lastCameraPosition is { } lastCameraPosition
            && _lastTargetPosition is { } lastTargetPosition
            && cameraPosition.DistanceSquaredTo(lastCameraPosition) < RecomputeDistanceSquaredThreshold
            && targetPosition.DistanceSquaredTo(lastTargetPosition) < RecomputeDistanceSquaredThreshold)
        {
            return;
        }

        _lastCameraPosition = cameraPosition;
        _lastTargetPosition = targetPosition;

        var occluding = ComputeOccludingSprites(cameraPosition, targetPosition, selectedPersonNode);

        // Re-applied every frame, not only on entering the set: the hover highlight rewrites the
        // same sprite's Modulate on every hover-state change and would undo the fade whenever the
        // cursor sits on an occluding canopy. Cheap - the set is a handful of sprites.
        // The faded set lives in BillboardSprite because picking consults it too (see
        // BillboardSprite.OcclusionFadedSprites); this is still the only place deciding membership.
        foreach (var sprite in occluding)
        {
            BillboardSprite.SetOcclusionFaded(sprite, true);
            SetSpriteAlpha(sprite, presentation.OcclusionFadedAlpha);
        }

        // Materialized first: un-fading mutates the very set being walked.
        var noLongerOccluding = BillboardSprite.OcclusionFadedSprites.Where(sprite => !occluding.Contains(sprite)).ToList();
        foreach (var sprite in noLongerOccluding)
        {
            BillboardSprite.SetOcclusionFaded(sprite, false);

            // The owning view can be freed between frames (a corpse mid-fade gets buried and its
            // PersonView queued free); touching a freed sprite throws ObjectDisposedException,
            // which breaks out of _Process every frame and silently stalls ticks.
            if (GodotObject.IsInstanceValid(sprite))
            {
                SetSpriteAlpha(sprite, 1f);
            }
        }
    }

    // What the occlusion sight line runs to: the selected person if any (and the node to exclude,
    // since it sits at the target itself), else the camera's own orbit/pan target so nothing gets
    // to block the view indefinitely just because no one is selected.
    private (Vector3 TargetPosition, Node? SelectedPersonNode) ResolveOcclusionTarget(Person? selectedPerson)
    {
        if (selectedPerson is { } person && presenter.GetPersonGlobalPosition(person.Id) is { } personPosition)
        {
            return (personPosition, presenter.GetPersonNode(person.Id));
        }

        return (cameraRig.RigGlobalPosition, null);
    }

    // Walks BillboardSprite.LiveSprites rather than the scene tree: a per-frame FindChildren over
    // every ResourceNode's Area3D subtree stuttered the whole frame, camera included. Ground
    // shadows are plain Sprite3Ds (GroundShadow), never billboards, so need no exclusion; only
    // the selection's own sprites do, since they sit at the target itself.
    private HashSet<Sprite3D> ComputeOccludingSprites(Vector3 cameraPosition, Vector3 targetPosition, Node? selectedPersonNode)
    {
        var result = new HashSet<Sprite3D>();

        if (SightLine.From(cameraPosition, targetPosition) is not { } sightLine)
        {
            return result;
        }

        foreach (var sprite in BillboardSprite.LiveSprites)
        {
            if (selectedPersonNode is not null && selectedPersonNode.IsAncestorOf(sprite))
            {
                continue;
            }

            // A tree's trunk layer (see ResourceNodeView): Camera.png's "trunks stay solid" rule,
            // even when the trunk geometrically sits in the way itself.
            if (BillboardSprite.IsExcludedFromOcclusionFade(sprite))
            {
                continue;
            }

            // Rendered width, scale included - the same answer pixel-accurate picking uses
            // (BillboardUv.RenderedSize), not the authored canvas size.
            var texture = sprite.Texture!;
            var scale = sprite.GlobalTransform.Basis.Scale;
            var renderedWidth = BillboardUv.RenderedSize(sprite.PixelSize, texture.GetWidth(), texture.GetHeight(), scale.X, scale.Y).X;

            if (sightLine.IsBlockedBy(
                    sprite.GlobalPosition,
                    renderedWidth / 2f,
                    presentation.OcclusionMargin,
                    presentation.OcclusionDistanceTolerance))
            {
                result.Add(sprite);
            }
        }

        return result;
    }

    private static void SetSpriteAlpha(Sprite3D sprite, float alpha)
    {
        var color = sprite.Modulate;
        color.A = alpha;
        sprite.Modulate = color;
    }
}
