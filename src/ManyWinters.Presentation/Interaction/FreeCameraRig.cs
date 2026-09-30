using Godot;
using ManyWinters.Presentation.Fog;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Interaction;

// Free pan/zoom/rotate camera shared by TerrainSandbox and Main. Perspective is the default
// projection (compared directly in the sandbox, see docs/terrain-and-world-scale-architecture.md);
// orthographic stays available via ToggleProjection.
public sealed partial class FreeCameraRig : Node3D
{
    private readonly Camera3D _camera;
    private readonly PresentationSettings _presentation;
    private readonly Func<float, float, float> _sampleHeight;
    private float _zoomDistance;
    private float _orthographicSize;
    private float _tiltDegrees;
    private bool _isOrthographic;
    private bool _mouseRotating;
    private Vector3 _panVelocity = Vector3.Zero;

    // sampleHeight: the same ground-height function everything else on the ground uses. Panning
    // only moves the rig in XZ, so without it the rig's Y stays frozen where it started and the
    // camera ends up under a nearby bump after panning.
    public FreeCameraRig(Vector3 initialPosition, PresentationSettings presentation, Func<float, float, float> sampleHeight)
    {
        _presentation = presentation;
        _zoomDistance = presentation.InitialZoomDistance;
        _orthographicSize = presentation.InitialZoomDistance;
        _tiltDegrees = presentation.DefaultTiltDegrees;
        _sampleHeight = sampleHeight;
        Position = initialPosition;

        // CullMask excludes the cloud-fog layer bit: that layer holds only the cloud scatter's
        // mask-only cloud proxies, which the default mask would draw on top of each real cloud.
        _camera = new Camera3D
        {
            Far = presentation.CameraFar,
            Near = presentation.CameraNear,
            CullMask = 0xFFFFFFFF & ~CloudFogMask.CloudLayerBit,
        };
        AddChild(_camera);
    }

    public Vector3 CameraGlobalPosition => _camera.GlobalPosition;

    // The orbit/pan target - Main's fallback line-of-sight target for the occlusion fade when
    // nothing is selected.
    public Vector3 RigGlobalPosition => GlobalPosition;

    // For screen-space projection (Main's selection marker, click radius) - UnprojectPosition and
    // IsPositionBehind are not exposed any other way.
    public Camera3D Camera => _camera;

    // How far from RigGlobalPosition a decoration is still worth building a node for. Tracks the
    // current zoom, not a fixed world distance, so zooming out to see the whole map keeps
    // everything in it, not just a fixed radius around the rig.
    public float ViewRadius => (_isOrthographic ? _orthographicSize : _zoomDistance) * _presentation.ViewRadiusMultiplier;

    // UpdateCamera reads GlobalPosition and calls LookAt, both of which need this node inside the
    // tree - not yet true during the constructor, since composition code adds this rig to the
    // scene only after constructing it.
    public override void _Ready() => UpdateCamera();

    // Puts the orbit point on a spot in the world, keeping the zoom, rotation and tilt the player
    // has set: taking the view to somebody (Main's band roster) is a pan, not a new camera.
    //
    // Only X and Z are taken from the target - the rig rides the ground under it, and a
    // person's own Y is their feet on a slope. Any pan still gliding is dropped, or the eased
    // velocity would carry the view straight off the person just arrived at.
    public void FocusOn(Vector3 target)
    {
        _panVelocity = Vector3.Zero;
        Position = new Vector3(target.X, _sampleHeight(target.X, target.Z), target.Z);
        UpdateCamera();
    }

    public void ToggleProjection()
    {
        _isOrthographic = !_isOrthographic;
        _camera.Projection = _isOrthographic ? Camera3D.ProjectionType.Orthogonal : Camera3D.ProjectionType.Perspective;
        UpdateCamera();
    }

    public void HandleInput(float delta)
    {
        // Only the keys are given up while the player is typing: the rig still follows the
        // ground under it, and a pan already gliding still eases to a stop rather than freezing
        // mid-glide the moment a name is asked for.
        var typing = TextEntry.HasTheKeyboard(_camera.GetViewport());
        bool Held(Key key) => !typing && Input.IsKeyPressed(key);

        var panDirection = Vector2.Zero;
        if (Held(Key.W) || Held(Key.Up))
        {
            panDirection.Y -= 1;
        }

        if (Held(Key.S) || Held(Key.Down))
        {
            panDirection.Y += 1;
        }

        if (Held(Key.A) || Held(Key.Left))
        {
            panDirection.X -= 1;
        }

        if (Held(Key.D) || Held(Key.Right))
        {
            panDirection.X += 1;
        }

        var panSpeed = (_isOrthographic ? _orthographicSize : _zoomDistance) * _presentation.PanSpeedPerZoomUnit;
        var targetPanVelocity = CameraMotion.PanVelocity(Basis, panDirection, panSpeed);
        _panVelocity = CameraMotion.Eased(_panVelocity, targetPanVelocity, _presentation.PanEaseRate, delta);
        Position += _panVelocity * delta;

        // Every frame, not only while a pan key is held: the rig's Y never drifts from the ground
        // under it however it got to this (X, Z), and a rig that started wrong self-heals.
        var rigPosition = Position;
        rigPosition.Y = _sampleHeight(rigPosition.X, rigPosition.Z);
        Position = rigPosition;

        var rotateDirection = 0f;
        if (Held(Key.Q))
        {
            rotateDirection -= 1;
        }

        if (Held(Key.E))
        {
            rotateDirection += 1;
        }

        if (rotateDirection != 0f)
        {
            RotateY(rotateDirection * _presentation.RotateSpeed * delta);
        }

        var zoomDirection = 0f;
        if (Held(Key.R))
        {
            zoomDirection -= 1;
        }

        if (Held(Key.F))
        {
            zoomDirection += 1;
        }

        if (zoomDirection != 0f)
        {
            Zoom(zoomDirection * delta);
        }

        var tiltDirection = 0f;
        if (Held(Key.Pageup))
        {
            tiltDirection += 1;
        }

        if (Held(Key.Pagedown))
        {
            tiltDirection -= 1;
        }

        if (tiltDirection != 0f)
        {
            var before = _tiltDegrees;
            var minTilt = _presentation.MinTiltDegrees;
            var maxTilt = _presentation.MaxTiltDegrees;
            _tiltDegrees = CameraMotion.Tilted(_tiltDegrees, tiltDirection * _presentation.TiltSpeedDegreesPerSecond * delta, minTilt, maxTilt);

            // A verbose session follows the game from its log alone; the tilt says when it has
            // gone all the way up or down - the step that crossed the limit and was clamped onto
            // it - which is as far as a held key can ever drive it. Range comparisons, not
            // equality: the clamped value is checked by where it landed, not what it equals.
            if (LaunchOptions.Verbose
                && (before < maxTilt && _tiltDegrees >= maxTilt
                    || before > minTilt && _tiltDegrees <= minTilt))
            {
                GD.Print($"Camera tilted to {_tiltDegrees:0} degrees.");
            }
        }

        // Unconditional: UpdateCamera also re-checks the camera's ground clearance, and while that
        // ran only on zoom/tilt input, WASD panning could carry the camera into a bump with nothing
        // to correct it until the player happened to zoom or tilt.
        UpdateCamera();
    }

    // Right-drag rotates/tilts, wheel zooms. Callers forward raw _Input/_UnhandledInput events so
    // Main and TerrainSandbox get identical mouse behavior alongside HandleInput's keyboard.
    public void HandleMouseInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } mouseButton:
                _mouseRotating = mouseButton.Pressed;
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                HandleScrollZoom(-1f);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                HandleScrollZoom(1f);
                break;
            case InputEventMouseMotion mouseMotion when _mouseRotating:
                RotateY(-mouseMotion.Relative.X * _presentation.MouseRotateRadiansPerPixel);
                _tiltDegrees = CameraMotion.Tilted(
                    _tiltDegrees,
                    -mouseMotion.Relative.Y * _presentation.MouseTiltDegreesPerPixel,
                    _presentation.MinTiltDegrees,
                    _presentation.MaxTiltDegrees);
                UpdateCamera();
                break;
        }
    }

    // A wheel notch (direction < 0 zooms in, > 0 zooms out) applied as one immediate step,
    // as opposed to HandleInput's held-key rate.
    private void HandleScrollZoom(float direction)
    {
        Zoom(direction * _presentation.ScrollZoomNotchSeconds);
        UpdateCamera();
    }

    // Whichever projection is live is the one that zooms - the other keeps its own value, so
    // toggling back mid-session lands where it was left rather than being dragged along.
    private void Zoom(float signedSeconds)
    {
        var rate = _presentation.ZoomRatePerSecond;
        var minZoom = _presentation.MinZoom;
        var maxZoom = _presentation.MaxZoom;
        if (_isOrthographic)
        {
            _orthographicSize = CameraMotion.Zoomed(_orthographicSize, signedSeconds, rate, minZoom, maxZoom);
        }
        else
        {
            _zoomDistance = CameraMotion.Zoomed(_zoomDistance, signedSeconds, rate, minZoom, maxZoom);
        }
    }

    private void UpdateCamera()
    {
        _camera.Position = CameraMotion.OffsetDirection(_tiltDegrees) * _zoomDistance;
        _camera.LookAt(GlobalPosition, Vector3.Up);
        _camera.Size = _orthographicSize;

        // Belt-and-suspenders on top of HandleInput's rig ground-following: the camera sits offset
        // from the rig, and a close, low tilt can put its own (X, Z) over a bump the rig is not on.
        var globalPosition = _camera.GlobalPosition;
        var cleared = CameraMotion.ClearedHeight(globalPosition.Y, _sampleHeight(globalPosition.X, globalPosition.Z), _presentation.MinCameraGroundClearance);
        if (cleared > globalPosition.Y)
        {
            globalPosition.Y = cleared;
            _camera.GlobalPosition = globalPosition;
            _camera.LookAt(GlobalPosition, Vector3.Up);
        }
    }
}
