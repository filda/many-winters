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

    // Degrees of elevation above the rig's horizontal plane. Height above ground and
    // horizontal distance from the target both derive from this same angle and ZoomDistance
    // (height = zoomDistance * sin, distance = zoomDistance * cos) - lowering it from the
    // original 45 (matching the old fixed Vector3(0, 1, 1) direction) is what drops the
    // default view's height while pushing its horizontal distance out a little, since sin
    // falls and cos rises together as the angle shrinks. The clamp keeps the view from ever
    // going fully overhead or fully edge-on, both of which break the billboard/cutout
    // illusion. The upper bound matters more now that every sprite uses FixedY billboarding
    // (see BillboardSprite.cs): that mode only ever yaws to face the camera's *horizontal*
    // direction, so looking straight down (90 deg) leaves nothing to yaw toward - every
    // sprite would render edge-on and vanish. 70 keeps a comfortable margin below that
    // degenerate case.
    private const float DefaultTiltDegrees = 20f;
    private const float MinTiltDegrees = 12f;
    private const float MaxTiltDegrees = 70f;
    private const float TiltSpeedDegreesPerSecond = 45f;

    // Minimum clearance the camera itself keeps above the ground directly under it - see
    // UpdateCamera's own doc comment.
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

    // Where the camera is actually looking (its orbit/pan target) - a fallback line-of-sight
    // target for Main's occlusion fade when nothing is selected to check occlusion against
    // instead.
    public Vector3 RigGlobalPosition => _rig.GlobalPosition;

    // For screen-space projection (Main's selection marker overlay) - UnprojectPosition/
    // IsPositionBehind aren't exposed any other way.
    public Camera3D Camera => _camera;

    // sampleHeight: the same ground-height function everything else on the ground uses
    // (TerrainRenderer.SampleHeight) - the rig only ever moves in Main's/TerrainSandbox's
    // XZ plane on its own (panning), so without this its own Y stays frozen at wherever it
    // started forever, drifting away from the real terrain height under it as soon as it
    // pans anywhere the ground isn't at exactly that same height. Harmless-looking on the
    // old, gentle real elevation (drifts slowly over a large distance); a lot more obvious
    // once the ground itself got a deliberately short-wavelength bump on top (todo:
    // "použít Perlinův šum na lehkou modifikaci terénu") - the camera could end up visibly
    // under a nearby bump after nothing more than ordinary panning.
    public FreeCameraRig(Node3D parent, Vector3 initialPosition, float initialDistance, float minZoom, float maxZoom, Func<float, float, float> sampleHeight)
    {
        _minZoom = minZoom;
        _maxZoom = maxZoom;
        _zoomDistance = initialDistance;
        _orthographicSize = initialDistance;
        _sampleHeight = sampleHeight;

        _rig = new Node3D { Position = initialPosition };
        parent.AddChild(_rig);

        // Near matters as much as Far for depth buffer precision - it's the Far/Near *ratio*
        // that determines how much of the buffer's precision actually lands in the distances
        // gameplay cares about (tens to a couple hundred meters), not Far alone. The engine
        // default Near (0.05) against this Far gave a 100,000:1 ratio - so little precision
        // remained by the time a background tree's own depth got encoded that
        // FogOfWarRenderer's depth-reconstruction shaders (fog_of_war_screen.gdshader,
        // fog_of_war_remembered.gdshader) recovered visibly wrong world positions for some of
        // its pixels, cutting a flat "ceiling" through unrelated tree canopies at a roughly
        // consistent height. 0.5 cuts that ratio by 10x - nothing in this game is ever
        // legitimately closer to the camera than that anyway.
        // CullMask excludes CloudFogMask.CloudLayerBit - that layer holds only
        // CloudScatter's mask-only proxies (cloud_mask_proxy.gdshader's flat flag-color
        // stand-ins), never meant to be seen directly; the default cull mask (every bit
        // set) would otherwise render them right on top of each real cloud sprite.
        _camera = new Camera3D { Far = 5000f, Near = 0.5f, CullMask = 0xFFFFFFFF & ~CloudFogMask.CloudLayerBit };
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

        // Every frame, not just while a pan key is actually held - the cheapest way to
        // guarantee the rig's own Y never drifts from the real ground height under it,
        // regardless of how it got to its current (X, Z) (also self-heals a rig that
        // somehow started off already wrong, rather than only ever getting it right on the
        // next pan).
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
            var zoomFactor = MathF.Pow(ZoomRatePerSecond, zoomDirection * delta);
            if (_isOrthographic)
            {
                _orthographicSize = Mathf.Clamp(_orthographicSize * zoomFactor, _minZoom, _maxZoom);
            }
            else
            {
                _zoomDistance = Mathf.Clamp(_zoomDistance * zoomFactor, _minZoom, _maxZoom);
            }
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
        }

        // Unconditional, every frame - not just when zoom/tilt actually changed this frame.
        // UpdateCamera repositions the camera relative to the rig AND re-checks its own
        // ground clearance (see its own doc comment) - while that clamp only ran on zoom/
        // tilt input, ordinary WASD panning could carry the camera briefly *into* a bump
        // between one of those and the next, with nothing there to catch and correct it
        // until the player happened to also zoom or tilt.
        UpdateCamera();
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

        // Belt-and-suspenders on top of HandleInput's own rig ground-following above - the
        // camera sits at an offset from the rig (elevated, pulled back), so it's normally
        // well clear of the ground even where the rig itself sits right at it, but a close,
        // low tilt angle can still put the camera's own (X, Z) over a nearby bump the rig
        // isn't directly on top of.
        var globalPosition = _camera.GlobalPosition;
        var minHeight = _sampleHeight(globalPosition.X, globalPosition.Z) + MinCameraGroundClearance;
        if (globalPosition.Y < minHeight)
        {
            globalPosition.Y = minHeight;
            _camera.GlobalPosition = globalPosition;
            _camera.LookAt(_rig.GlobalPosition, Vector3.Up);
        }
    }
}
