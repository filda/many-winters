using Godot;
using ManyWinters.Core.Continuity;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;
using ManyWinters.Godot.Interaction;

namespace ManyWinters.Godot.Views;

public partial class GraveView(Grave grave, Action<Grave> onSelected, CollisionObject3D.InputEventEventHandler onMissedClick) : Area3D
{
    public const float Size = 0.8f;
    private const float ShadowDiameter = 0.9f;

    private const string MarkedTexturePath = "res://Content/graves/grave_marked.png";
    private const string UnmarkedTexturePath = "res://Content/graves/grave_unmarked.png";

    private static readonly Color MarkedColor = new(0.7f, 0.7f, 0.75f);
    private static readonly Color UnmarkedColor = new(0.4f, 0.3f, 0.2f);

    private Sprite3D _sprite = null!;
    private string _texturePath = null!;
    private Color _baseModulate;

    // A grave is the one thing in the world the group is meant to come back to, so it is also
    // the one where staying at full brightness after they walked away was most obviously
    // wrong - a bright headstone standing in a field of sepia trees. Same fade as everything
    // else now (see RememberedFade).
    private readonly RememberedFade _remembered = new();

    public override void _Ready()
    {
        InputRayPickable = true;

        _texturePath = grave.IsMarked ? MarkedTexturePath : UnmarkedTexturePath;
        var fallbackColor = grave.IsMarked ? MarkedColor : UnmarkedColor;

        var groundShadow = GroundShadow.Create(ShadowDiameter);
        groundShadow.Position += new Vector3(0, (-Size / 2f) + GroundShadow.GroundOffset, 0);
        AddChild(groundShadow);

        _sprite = BillboardSprite.Create(_texturePath, Size, fallbackColor);
        _baseModulate = _sprite.Modulate;
        AddChild(_sprite);

        // Sized (and centered) to the actual drawn mound/stone, not the full square canvas.
        var extent = SpriteVisibleExtent.Compute(_texturePath, Size);
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(extent.Width, extent.Height, extent.Width) },
            Position = new Vector3(extent.CenterXOffset, extent.CenterYOffset, 0),
        });

        InputEvent += OnInputEvent;

        // Graves outlive the group's attention by design, so one is routinely created and
        // then left alone for a long time - and can be created already out of sight (see
        // SnapRemembered). Frames are processed only while a fade is actually running.
        SetProcess(_remembered.IsFading);
        ApplyTint();
    }

    // Fog of war's "remembered" tier (WorldPresenter.RefreshExploration) - aims the fade,
    // which then moves a frame at a time in _Process.
    public void SetRemembered(bool remembered)
    {
        if (!_remembered.Retarget(remembered))
        {
            return;
        }

        SetProcess(true);
    }

    // Straight to the end state, no fade. See ResourceNodeView.SnapRemembered.
    public void SnapRemembered(bool remembered) => _remembered.Snap(remembered);

    public override void _Process(double delta)
    {
        var stillFading = _remembered.Advance((float)delta);
        ApplyTint();
        if (!stillFading)
        {
            SetProcess(false);
        }
    }

    // No hover state to compose with: a grave is clickable but never highlighted (see
    // HoverArbiter for what does get a highlight).
    private void ApplyTint() => SpriteLayerTint.Apply(_sprite, _baseModulate, _remembered, hovered: false);

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (camera is not Camera3D camera3D || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            return;
        }

        // The broad-phase collision box (see SpriteVisibleExtent) is bigger than the actual
        // silhouette - Godot only delivers a click to the nearest pickable collider along the
        // ray, so a click landing inside the box but off the opaque pixels (e.g. on this
        // grave's own ground shadow) would otherwise be silently swallowed here instead of
        // reaching the ground underneath. Forward it to whatever a plain ground click at this
        // same spot would have done.
        if (!TryClickAt(camera3D, position) && !HoverRescue.TryClickElsewhere(this, camera3D, position, MouseButton.Left))
        {
            onMissedClick(camera, @event, position, normal, shapeIdx);
        }
    }

    public bool TryClickAt(Camera3D camera, Vector3 worldPosition)
    {
        if (!SpritePixelHit.IsOpaqueAt(camera, worldPosition, _sprite, _texturePath))
        {
            return false;
        }

        onSelected(grave);
        return true;
    }
}
