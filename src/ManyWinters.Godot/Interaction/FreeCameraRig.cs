using Godot;
using ManyWinters.Godot.Fog;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Interaction;

// Free pan/zoom/rotate camera shared by TerrainSandbox and Main. Perspective is the default
// projection (compared directly in the sandbox, see docs/terrain-and-world-scale-architecture.md);
// orthographic stays available via ToggleProjection.
public sealed class FreeCameraRig
{
    // Pan speed scales with zoom distance: the zoom range spans 3 to 2000, so a fixed speed is
    // glacial zoomed out and wild zoomed in - the same reason zoom is multiplicative.
    private const float PanSpeedPerZoomUnit = 1f;
    // How fast velocity eases toward its target - higher = snappier, lower = floatier.
    // 1/PanEaseRate is roughly the time constant (seconds) to close ~63% of the gap.
    private const float PanEaseRate = 10f;
    private const float RotateSpeed = 1.5f;
    private const float ZoomRatePerSecond = 2.5f;
    // A wheel notch has no delta of its own, so it's treated as this many seconds' worth of
    // R/F's held-key rate - keeps a single zoom feel instead of a separately tuned step.
    private const float ScrollZoomNotchSeconds = 0.05f;
    // Right-drag rotate/tilt, alongside Q/E and Page Up/Down for mouse-less control.
    private const float MouseRotateRadiansPerPixel = 0.005f;
    private const float MouseTiltDegreesPerPixel = 0.15f;

    // Degrees of elevation above the rig's plane; height = zoom * sin, distance = zoom * cos.
    // The clamp keeps the view from going fully overhead or edge-on, both of which break the
    // cutout illusion. The upper bound matters most: FixedY billboards (see BillboardSprite) only
    // yaw toward the camera's horizontal direction, so at 90 deg every sprite renders edge-on.
    private const float DefaultTiltDegrees = 20f;
    private const float MinTiltDegrees = 12f;
    private const float MaxTiltDegrees = 70f;
    private const float TiltSpeedDegreesPerSecond = 45f;

    // Minimum clearance the camera keeps above the ground directly under it - see UpdateCamera.
    private const float MinCameraGroundClearance = 0.3f;

    private readonly Node3D _rig;
    private readonly Camera3D _camera;
    private readonly float _minZoom;
    private readonly float _maxZoom;
    private readonly Func<float, float, float> _sampleHeight;
    private float _zoomDistance;
    private float _orthographicSize;
    private float _tiltDegrees = DefaultTiltDegrees;
    private bool _isOrthographic;
    private bool _mouseRotating;
    private Vector3 _panVelocity = Vector3.Zero;

    public Vector3 CameraGlobalPosition => _camera.GlobalPosition;

    // The orbit/pan target - Main's fallback line-of-sight target for the occlusion fade when
    // nothing is selected.
    public Vector3 RigGlobalPosition => _rig.GlobalPosition;

    // For screen-space projection (Main's selection marker, click radius) - UnprojectPosition and
    // IsPositionBehind are not exposed any other way.
    public Camera3D Camera => _camera;

    // sampleHeight: the same ground-height function everything else on the ground uses
    // (TerrainRenderer.SampleHeight). Panning only moves the rig in XZ, so without it the rig's Y
    // stays frozen where it started and the camera ends up under a nearby bump after panning.
    public FreeCameraRig(Node3D parent, Vector3 initialPosition, float initialDistance, float minZoom, float maxZoom, Func<float, float, float> sampleHeight)
    {
        _minZoom = minZoom;
        _maxZoom = maxZoom;
        _zoomDistance = initialDistance;
        _orthographicSize = initialDistance;
        _sampleHeight = sampleHeight;

        _rig = new Node3D { Position = initialPosition };
        parent.AddChild(_rig);

        // Depth precision depends on the Far/Near ratio, not Far alone. The engine default Near
        // (0.05) against this Far gave 100,000:1 - so little precision remained at background-tree
        // depths that FogOfWarRenderer's depth-reconstruction shaders (fog_of_war_screen.gdshader,
        // fog_of_war_remembered.gdshader) cut a flat "ceiling" through unrelated canopies. 0.5 cuts
        // the ratio 10x; nothing is ever legitimately closer to the camera than that.
        // CullMask excludes CloudFogMask.CloudLayerBit: that layer holds only CloudScatter's
        // mask-only cloud proxies, which the default mask would draw on top of each real cloud.
        _camera = new Camera3D { Far = 5000f, Near = 0.5f, CullMask = 0xFFFFFFFF & ~CloudFogMask.CloudLayerBit };
        _rig.AddChild(_camera);
        UpdateCamera();
    }

    // Puts the orbit point on a spot in the world, keeping the zoom, rotation and tilt the player
    // has set: taking the view to somebody (Main's band roster) is a pan, not a new camera.
    //
    // Only X and Z are taken from the target - the rig rides the ground under it (see
    // HandleInput), and a person's own Y is their feet on a slope. Any pan still gliding is
    // dropped, or the eased velocity would carry the view straight off the person just arrived at.
    public void FocusOn(Vector3 target)
    {
        _panVelocity = Vector3.Zero;
        _rig.Position = new Vector3(target.X, _sampleHeight(target.X, target.Z), target.Z);
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
        var panDirection = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
        {
            panDirection.Y -= 1;
        }

        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
        {
            panDirection.Y += 1;
        }

        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
        {
            panDirection.X -= 1;
        }

        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
        {
            panDirection.X += 1;
        }

        var panSpeed = (_isOrthographic ? _orthographicSize : _zoomDistance) * PanSpeedPerZoomUnit;
        var targetPanVelocity = CameraMotion.PanVelocity(_rig.Basis, panDirection, panSpeed);
        _panVelocity = CameraMotion.Eased(_panVelocity, targetPanVelocity, PanEaseRate, delta);
        _rig.Position += _panVelocity * delta;

        // Every frame, not only while a pan key is held: the rig's Y never drifts from the ground
        // under it however it got to this (X, Z), and a rig that started wrong self-heals.
        var rigPosition = _rig.Position;
        rigPosition.Y = _sampleHeight(rigPosition.X, rigPosition.Z);
        _rig.Position = rigPosition;

        var rotateDirection = 0f;
        if (Input.IsKeyPressed(Key.Q))
        {
            rotateDirection -= 1;
        }

        if (Input.IsKeyPressed(Key.E))
        {
            rotateDirection += 1;
        }

        if (rotateDirection != 0f)
        {
            _rig.RotateY(rotateDirection * RotateSpeed * delta);
        }

        var zoomDirection = 0f;
        if (Input.IsKeyPressed(Key.R))
        {
            zoomDirection -= 1;
        }

        if (Input.IsKeyPressed(Key.F))
        {
            zoomDirection += 1;
        }

        if (zoomDirection != 0f)
        {
            Zoom(zoomDirection * delta);
        }

        var tiltDirection = 0f;
        if (Input.IsKeyPressed(Key.Pageup))
        {
            tiltDirection += 1;
        }

        if (Input.IsKeyPressed(Key.Pagedown))
        {
            tiltDirection -= 1;
        }

        if (tiltDirection != 0f)
        {
            _tiltDegrees = CameraMotion.Tilted(_tiltDegrees, tiltDirection * TiltSpeedDegreesPerSecond * delta, MinTiltDegrees, MaxTiltDegrees);
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
                _rig.RotateY(-mouseMotion.Relative.X * MouseRotateRadiansPerPixel);
                _tiltDegrees = CameraMotion.Tilted(
                    _tiltDegrees,
                    -mouseMotion.Relative.Y * MouseTiltDegreesPerPixel,
                    MinTiltDegrees,
                    MaxTiltDegrees);
                UpdateCamera();
                break;
        }
    }

    // A wheel notch (direction < 0 zooms in, > 0 zooms out) applied as one immediate step,
    // as opposed to HandleInput's held-key rate.
    private void HandleScrollZoom(float direction)
    {
        Zoom(direction * ScrollZoomNotchSeconds);
        UpdateCamera();
    }

    // Whichever projection is live is the one that zooms - the other keeps its own value, so
    // toggling back mid-session lands where it was left rather than being dragged along.
    private void Zoom(float signedSeconds)
    {
        if (_isOrthographic)
        {
            _orthographicSize = CameraMotion.Zoomed(_orthographicSize, signedSeconds, ZoomRatePerSecond, _minZoom, _maxZoom);
        }
        else
        {
            _zoomDistance = CameraMotion.Zoomed(_zoomDistance, signedSeconds, ZoomRatePerSecond, _minZoom, _maxZoom);
        }
    }

    private void UpdateCamera()
    {
        _camera.Position = CameraMotion.OffsetDirection(_tiltDegrees) * _zoomDistance;
        _camera.LookAt(_rig.GlobalPosition, Vector3.Up);
        _camera.Size = _orthographicSize;

        // Belt-and-suspenders on top of HandleInput's rig ground-following: the camera sits offset
        // from the rig, and a close, low tilt can put its own (X, Z) over a bump the rig is not on.
        var globalPosition = _camera.GlobalPosition;
        var cleared = CameraMotion.ClearedHeight(globalPosition.Y, _sampleHeight(globalPosition.X, globalPosition.Z), MinCameraGroundClearance);
        if (cleared > globalPosition.Y)
        {
            globalPosition.Y = cleared;
            _camera.GlobalPosition = globalPosition;
            _camera.LookAt(_rig.GlobalPosition, Vector3.Up);
        }
    }
}
