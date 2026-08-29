using Godot;

namespace ManyWinters.Godot;

// Free pan/zoom/rotate camera (visual plan "Confirmed design decisions: Camera"). Shared by
// TerrainSandbox.cs and Main.cs so both get identical camera behavior over real terrain.
// Perspective is the default projection (settled by direct comparison in the sandbox, see
// docs/terrain-and-world-scale-architecture.md); orthographic stays available via ToggleProjection
// for future comparison.
public sealed class FreeCameraRig
{
    // Pan speed scales with the current zoom distance rather than being a fixed
    // units/second value - the zoom range here spans 3 to 2000 (a ~700x range), so a fixed
    // speed feels glacial zoomed out and wildly oversized zoomed in, the same reason zoom
    // itself is multiplicative rather than additive.
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

    // Degrees of elevation above the rig's horizontal plane. 45 matches the original fixed
    // Vector3(0, 1, 1) direction; the clamp keeps the view from ever going fully overhead or
    // fully edge-on, both of which break the billboard/cutout illusion.
    private const float DefaultTiltDegrees = 45f;
    private const float MinTiltDegrees = 20f;
    private const float MaxTiltDegrees = 80f;
    private const float TiltSpeedDegreesPerSecond = 45f;

    private readonly Node3D _rig;
    private readonly Camera3D _camera;
    private readonly float _minZoom;
    private readonly float _maxZoom;
    private float _zoomDistance;
    private float _orthographicSize;
    private float _tiltDegrees = DefaultTiltDegrees;
    private bool _isOrthographic;
    private bool _mouseRotating;
    private Vector3 _panVelocity = Vector3.Zero;

    public Vector3 CameraGlobalPosition => _camera.GlobalPosition;

    public FreeCameraRig(Node3D parent, Vector3 initialPosition, float initialDistance, float minZoom, float maxZoom)
    {
        _minZoom = minZoom;
        _maxZoom = maxZoom;
        _zoomDistance = initialDistance;
        _orthographicSize = initialDistance;

        _rig = new Node3D { Position = initialPosition };
        parent.AddChild(_rig);

        _camera = new Camera3D { Far = 5000f };
        _rig.AddChild(_camera);
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

        var targetPanVelocity = Vector3.Zero;
        if (panDirection != Vector2.Zero)
        {
            var basis = _rig.Basis;
            var forward = new Vector3(basis.Z.X, 0, basis.Z.Z).Normalized();
            var right = new Vector3(basis.X.X, 0, basis.X.Z).Normalized();
            var panSpeed = (_isOrthographic ? _orthographicSize : _zoomDistance) * PanSpeedPerZoomUnit;
            targetPanVelocity = ((right * panDirection.X) + (forward * panDirection.Y)).Normalized() * panSpeed;
        }

        // Exponential ease toward the target velocity (zero when no key is held) instead of
        // snapping straight to it, so starting and stopping both feel smooth rather than
        // instant.
        var panEase = 1f - MathF.Exp(-PanEaseRate * delta);
        _panVelocity = _panVelocity.Lerp(targetPanVelocity, panEase);
        _rig.Position += _panVelocity * delta;

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
            var zoomFactor = MathF.Pow(ZoomRatePerSecond, zoomDirection * delta);
            if (_isOrthographic)
            {
                _orthographicSize = Mathf.Clamp(_orthographicSize * zoomFactor, _minZoom, _maxZoom);
            }
            else
            {
                _zoomDistance = Mathf.Clamp(_zoomDistance * zoomFactor, _minZoom, _maxZoom);
            }

            UpdateCamera();
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
            _tiltDegrees = Mathf.Clamp(_tiltDegrees + (tiltDirection * TiltSpeedDegreesPerSecond * delta), MinTiltDegrees, MaxTiltDegrees);
            UpdateCamera();
        }
    }

    // Mouse-driven camera control: right-drag rotates/tilts, wheel zooms. Callers forward
    // their raw _Input/_UnhandledInput events here so both Main.cs and TerrainSandbox.cs get
    // identical mouse behavior alongside HandleInput's keyboard handling.
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
                _tiltDegrees = Mathf.Clamp(
                    _tiltDegrees - (mouseMotion.Relative.Y * MouseTiltDegreesPerPixel),
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
        var factor = MathF.Pow(ZoomRatePerSecond, direction * ScrollZoomNotchSeconds);
        if (_isOrthographic)
        {
            _orthographicSize = Mathf.Clamp(_orthographicSize * factor, _minZoom, _maxZoom);
        }
        else
        {
            _zoomDistance = Mathf.Clamp(_zoomDistance * factor, _minZoom, _maxZoom);
        }

        UpdateCamera();
    }

    private Vector3 CameraDirection()
    {
        var tiltRadians = Mathf.DegToRad(_tiltDegrees);
        return new Vector3(0, MathF.Sin(tiltRadians), MathF.Cos(tiltRadians));
    }

    private void UpdateCamera()
    {
        _camera.Position = CameraDirection() * _zoomDistance;
        _camera.LookAt(_rig.GlobalPosition, Vector3.Up);
        _camera.Size = _orthographicSize;
    }
}
