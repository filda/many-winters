using ManyWinters.Presentation.Fog;
using ManyWinters.Presentation.Interaction;
using ManyWinters.Presentation.Ui;

namespace ManyWinters.Presentation.Views;

// Presentation that runs every rendered frame regardless of whether the simulation clock is
// held: camera movement, what stands between the camera and the selected person, the selection
// marker's screen position, hover, and the cloud-fog mask's camera tracking. None of it needs a
// tick to have happened, and none of it decides whether one is due.
public sealed class WorldFrameUpdater(
    FreeCameraRig cameraRig,
    OcclusionFader occlusionFader,
    SelectionController selection,
    WorldPresenter presenter,
    CloudFogMask cloudFogMask)
{
    public void Update(float delta)
    {
        cameraRig.HandleInput(delta);
        // The camera and the selected creature's interpolated position move continuously between
        // ticks, so what stands in the way changes continuously too.
        occlusionFader.Update(selection.SelectedCreature);
        selection.UpdateMarker();
        // Hover is taken on mouse movement but can be lost without any - a person can walk out
        // from under a resting cursor.
        presenter.RevalidateHover();
        // The mask camera tracks the main camera's continuous movement.
        cloudFogMask.Update();
    }
}
