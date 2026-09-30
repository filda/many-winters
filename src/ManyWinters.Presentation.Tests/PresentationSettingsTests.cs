namespace ManyWinters.Presentation.Tests;

public class PresentationSettingsTests
{
    private static readonly PresentationSettings Settings = PresentationSettings.Default;

    [Fact]
    public void DefaultTiltSitsInsideTheTiltClamp()
    {
        // A default outside the clamp would snap on the first tilt key press.
        Assert.True(Settings.MinTiltDegrees < Settings.DefaultTiltDegrees);
        Assert.True(Settings.DefaultTiltDegrees < Settings.MaxTiltDegrees);
    }

    [Fact]
    public void TiltClampStaysOffBothDegenerateViews()
    {
        // Edge-on (0) and overhead (90) both break the cutout illusion; see the settings' comment.
        Assert.True(Settings.MinTiltDegrees > 0f);
        Assert.True(Settings.MaxTiltDegrees < 90f);
    }

    [Fact]
    public void InitialZoomSitsInsideTheZoomRange()
    {
        Assert.True(Settings.MinZoom < Settings.InitialZoomDistance);
        Assert.True(Settings.InitialZoomDistance < Settings.MaxZoom);
    }

    [Fact]
    public void FarPlaneReachesPastTheFurthestZoom()
    {
        // Zoomed all the way out, the ground under the rig must still be inside the frustum.
        Assert.True(Settings.CameraFar > Settings.MaxZoom);
        Assert.True(Settings.CameraNear > 0f);
        Assert.True(Settings.CameraNear < Settings.MinZoom);
    }
}
