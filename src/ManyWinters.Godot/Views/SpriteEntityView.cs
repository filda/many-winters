using Godot;
using ManyWinters.Godot.Interaction;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// Everything the world is drawn out of - people, resources, graves, buildings - is one or more
// billboarded sprite layers standing on the ground: dimmed while out of sight (fog of war's
// "remembered" tier), ghosted while blocking the view of the selection, and for most of them
// hoverable and clickable to the pixel. Each view keeps only what genuinely differs: which
// layers it draws from which textures, what its seed varies, what a click means, and any
// animation of its own.
//
// Not unit-testable: a Node-derived type cannot be constructed outside the engine (see the test
// project's README). Anything that is a function of its inputs lives in Logic/ (RememberedFade,
// SpriteExtents, HoverArbiter, WalkCycle); this is the thin shell that calls them.
internal abstract partial class SpriteEntityView : Area3D, IHoverable
{
    // One drawn layer. TexturePath and BaseModulate are settable because PersonView re-points
    // and re-colours its layers on death.
    protected sealed class SpriteLayer(Sprite3D sprite, string texturePath, bool picks, bool outlines)
    {
        public Sprite3D Sprite { get; } = sprite;

        public string TexturePath { get; set; } = texturePath;

        // The layer's colour in full sight, before the fog tint is multiplied in. Taken from the
        // sprite as registered, so seeded variation (canopy brightness, garment colour) is in it.
        public Color BaseModulate { get; set; } = sprite.Modulate;

        // Whether this layer's opaque pixels count as the entity under the cursor. A fruit
        // overlay does not: it lies inside the canopy's own silhouette and adds no pixels.
        public bool Picks { get; } = picks;

        // Whether the hover rim traces this layer - not the same question. A branch layer is
        // pickable, but a rim around one-pixel twigs reads as a squiggle beside the canopy.
        public bool Outlines { get; } = outlines;
    }

    private readonly List<SpriteLayer> _layers = new();
    private readonly RememberedFade _remembered = new();

    // Null for a view that never lights up (a grave, a building). Null _onMissedClick too means
    // nothing can be clicked, and the view gets no collision shape or ray picking.
    private readonly HoverArbiter? _hover;
    private readonly InputEventEventHandler? _onMissedClick;

    private CollisionShape3D? _collisionShape;
    private bool _isHovered;

    // The world height every layer is created at (BillboardSprite.Create) and the height
    // WorldPresenter placed this node by; the ground shadow and ground-contact correction are
    // measured against it.
    protected float NominalHeight { get; }

    protected SpriteEntityView(float nominalHeight, HoverArbiter? hover, InputEventEventHandler? onMissedClick)
    {
        NominalHeight = nominalHeight;
        _hover = hover;
        _onMissedClick = onMissedClick;
    }

    private bool IsPickable => _onMissedClick is not null;

    // Sealed so the order - build the layers, measure them, paint them - is settled once here.
    // Views build themselves in Build().
    public sealed override void _Ready()
    {
        Build();

        if (IsPickable)
        {
            InputRayPickable = true;
            RefreshCollisionShape();
            // No MouseExited subscription: Godot only sends it to the collider its own picking
            // chose, which leaves sprites lit forever. Losing hover is settled once a frame by
            // IsStillUnderCursor.
            InputEvent += OnInputEvent;
        }
        else
        {
            InputRayPickable = false;
        }

        // Process frames only while something moves - a fade in flight or a self-animating view.
        // Thousands of resource nodes sit still nearly all of the time.
        SetProcess(NeedsEveryFrame || _remembered.IsFading);
        ApplyTints();
    }

    // Where a view creates its layers, ground shadow and seeded scale. Called from _Ready, so
    // the node is in the tree and WorldPresenter has already set its Position.
    protected abstract void Build();

    // For a view with an animation of its own (PersonView's walk cycle), whose processing
    // cannot be switched off between fades.
    protected virtual bool NeedsEveryFrame => false;

    protected virtual void OnProcess(double delta)
    {
    }

    public sealed override void _Process(double delta)
    {
        if (_remembered.IsFading)
        {
            var stillFading = _remembered.Advance((float)delta);
            ApplyTints();
            if (!stillFading && !NeedsEveryFrame)
            {
                SetProcess(false);
            }
        }

        OnProcess(delta);
    }

    // Drops out of the hover arbiter without a callback: QueueFree is already requested, and
    // touching a freed node is a crash.
    public sealed override void _ExitTree() => _hover?.Forget(this);

    // Takes over a sprite the view created. Creation stays with the view, where alpha cut,
    // render priority and occlusion-fade exclusion are decided; from here on this class tints,
    // scales, measures and picks against it.
    protected SpriteLayer Register(Sprite3D sprite, string texturePath, bool picks = true, bool outlines = true)
    {
        var layer = new SpriteLayer(sprite, texturePath, picks, outlines);
        _layers.Add(layer);
        AddChild(sprite);
        return layer;
    }

    // Re-points a layer at another image, keeping its world height. BillboardSprite.Apply
    // resets Modulate to white, so the layer's base colour is handed back in here.
    protected void Retexture(SpriteLayer layer, string texturePath, Color baseModulate, Color fallbackColor)
    {
        BillboardSprite.Apply(layer.Sprite, texturePath, NominalHeight, fallbackColor);
        layer.TexturePath = texturePath;
        layer.BaseModulate = baseModulate;
    }

    // The soft blob under the entity, seated where a sprite of NominalHeight has its bottom
    // edge. Not a layer: never tinted, never picked against (docs/todo/todo.md plans a real
    // silhouette).
    protected void SetUpGroundShadow(float diameter)
    {
        var groundShadow = GroundShadow.Create(diameter);
        groundShadow.Position += new Vector3(0f, (-NominalHeight / 2f) + GroundShadow.GroundOffset, 0f);
        AddChild(groundShadow);
    }

    // WorldPresenter puts the origin at groundHeight + NominalHeight/2, which seats the bottom
    // edge on the ground only at scale 1: scaling multiplies that half-height, so anything
    // shorter floats and anything taller sinks. Shifting Position by the same displacement
    // cancels it.
    protected void ScaleAndKeepGroundContact(float widthScale, float heightScale)
    {
        Scale = new Vector3(widthScale, heightScale, widthScale);
        GroundContactCorrection = new Vector3(0f, (NominalHeight / 2f) * (heightScale - 1f), 0f);
        Position += GroundContactCorrection;
    }

    // How far ScaleAndKeepGroundContact lifted this node above where WorldSpace.ToRender puts
    // an unscaled one, so a later position handed in (PersonView's per-tick target) is lifted
    // the same; otherwise the first tick walks every person down to the uncorrected height.
    protected Vector3 GroundContactCorrection { get; private set; }

    // The drawn silhouette in this node's local metres: the union of the picking layers' visible
    // extents, each scaled by its own sprite's scale (1 everywhere today, but a layer's scale is
    // the one factor unreadable elsewhere). This node's own scale is left out: the engine
    // applies it to every child, so counting it here would apply it twice.
    protected SpriteExtents.Extent VisibleExtent()
    {
        SpriteExtents.Extent? combined = null;
        foreach (var layer in _layers)
        {
            if (!layer.Picks)
            {
                continue;
            }

            var extent = SpriteExtents.Scaled(
                SpriteVisibleExtent.Compute(layer.TexturePath, NominalHeight),
                layer.Sprite.Scale.X,
                layer.Sprite.Scale.Y);

            // The extent is read off the unflipped texture, so a mirrored layer's content sits
            // the same distance from centre but on the other side.
            if (layer.Sprite.FlipH)
            {
                extent = extent with { CenterXOffset = -extent.CenterXOffset };
            }

            combined = combined is { } soFar ? SpriteExtents.Combine(soFar, extent) : extent;
        }

        return combined ?? new SpriteExtents.Extent(NominalHeight, NominalHeight, 0f, 0f);
    }

    // Top of the drawn silhouette above this node's origin, in *world* metres - for Main's
    // screen-space selection marker, which adds it to GlobalPosition; hence this node's scale
    // is in it, unlike in the local extent above.
    public float TopHeightOffset
    {
        get
        {
            var extent = VisibleExtent();
            return (extent.CenterYOffset + (extent.Height / 2f)) * Scale.Y;
        }
    }

    // Cut to the drawn silhouette, not the full square canvas, or the shape would hover and
    // click well outside anything visible. Re-derived whenever the texture changes (a corpse
    // lies down, wider and shorter).
    protected void RefreshCollisionShape()
    {
        if (!IsPickable)
        {
            return;
        }

        if (_collisionShape is null)
        {
            _collisionShape = new CollisionShape3D { Shape = new BoxShape3D() };
            AddChild(_collisionShape);
        }

        var extent = VisibleExtent();
        ((BoxShape3D)_collisionShape.Shape).Size = new Vector3(extent.Width, extent.Height, extent.Width);
        _collisionShape.Position = new Vector3(extent.CenterXOffset, extent.CenterYOffset, 0f);
    }

    public void ShowHovered(bool hovered)
    {
        if (hovered == _isHovered)
        {
            return;
        }

        _isHovered = hovered;

        // A rim traced around the silhouette, nothing else: no scale bump or tint, so geometry
        // stays put and nothing has to be re-measured.
        ShowOutline(hovered);
    }

    // One rim around the whole entity rather than one per layer, traced from the layers marked
    // Outlines only. It hangs on the last of them, the one drawn on top, so it composites over
    // the others.
    private void ShowOutline(bool hovered)
    {
        Sprite3D? host = null;
        List<Texture2D>? textures = null;
        foreach (var layer in _layers)
        {
            if (!layer.Outlines)
            {
                continue;
            }

            host = layer.Sprite;
            (textures ??= new List<Texture2D>()).Add(layer.Sprite.Texture);
        }

        if (host is null || textures is null)
        {
            return;
        }

        if (hovered)
        {
            HoverOutline.Show(host, textures);
        }
        else
        {
            HoverOutline.Clear(host);
        }
    }

    // Lets HoverRescue ask whether this exact point is opaque on this view, when another
    // entity's broad-phase box won the pick. A view that never lights up answers no, so the
    // rescue carries on to whatever is really under the cursor.
    public bool TryHoverAt(Camera3D camera, Vector3 worldPosition)
    {
        if (_hover is null)
        {
            return false;
        }

        var opaque = IsOpaqueAt(camera, worldPosition);
        _hover.Set(this, opaque);
        return opaque;
    }

    // Asked once a frame while this view holds the highlight: the same test from the cursor's
    // current position rather than from a picking event, since a
    // stuck highlight is always a missing event. A cursor over any UI panel counts as off -
    // physics picking never fires under a Control, so the sprite behind one would stay lit.
    public bool IsStillUnderCursor()
    {
        var viewport = GetViewport();
        return viewport.GuiGetHoveredControl() is null
            && viewport.GetCamera3D() is { } camera
            && IsOpaqueAtScreen(camera, viewport.GetMousePosition());
    }

    public bool TryClickAt(Camera3D camera, Vector3 worldPosition, MouseButton button) =>
        WantsClick(button) && IsOpaqueAt(camera, worldPosition) && OnClicked(button);

    // Which of the two order buttons this view answers to. An unwanted one is left entirely
    // alone - not even the missed-click fallback runs - so a click a view declines outright
    // cannot become a ground order behind it.
    protected virtual bool WantsClick(MouseButton button) => button == MouseButton.Left;

    // What a click on this entity means. False means "not mine after all" and sends the click
    // down the same fallback chain as one that missed the pixels.
    protected virtual bool OnClicked(MouseButton button) => false;

    // Pins the hit-test plane to a stable anchor instead of each sprite's own GlobalPosition:
    // PersonView's walk bob moves the layers every frame, which sweeps the sampled pixel across
    // silhouette edges and flickers the hover. Null means each sprite's own position.
    protected virtual Vector3? PixelHitAnchor => null;

    private bool IsOpaqueAt(Camera3D camera, Vector3 worldPosition) =>
        IsOpaqueAtScreen(camera, camera.UnprojectPosition(worldPosition));

    // A point is on the entity if it lands on any picking layer's opaque pixels: a split tree's
    // layers together form one silhouette, and a person's cloak is as much them as the body.
    private bool IsOpaqueAtScreen(Camera3D camera, Vector2 screenPosition)
    {
        foreach (var layer in _layers)
        {
            if (layer.Picks && SpritePixelHit.IsOpaqueAtScreen(camera, screenPosition, layer.Sprite, layer.TexturePath, PixelHitAnchor))
            {
                return true;
            }
        }

        return false;
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (camera is not Camera3D camera3D)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseMotion when _hover is not null:
                // Nothing opaque here and nothing behind it either: the cursor is over bare
                // ground, so whatever was lit has been left behind.
                if (!TryHoverAt(camera3D, position) && !HoverRescue.TryHoverElsewhere(this, camera3D, position))
                {
                    _hover.Clear();
                }

                break;
            // The collision box is bigger than the silhouette inside it, and Godot delivers a
            // click only to the nearest pickable collider, so a click inside the box but off the
            // pixels (on the shadow at a person's feet) would be swallowed here. Try whatever
            // else is at this point first, then fall back to a ground-click order.
            //
            // OrderButtons first, and for every view: the wheel arrives here as a pressed mouse
            // button too, and a scroll over a tree is a zoom rather than an order to fell it.
            case InputEventMouseButton { Pressed: true } mouseEvent
                when OrderButtons.Includes(mouseEvent.ButtonIndex) && WantsClick(mouseEvent.ButtonIndex):
                if (!TryClickAt(camera3D, position, mouseEvent.ButtonIndex)
                    && !HoverRescue.TryClickElsewhere(this, camera3D, position, mouseEvent.ButtonIndex))
                {
                    _onMissedClick?.Invoke(camera, @event, position, normal, shapeIdx);
                }

                break;
        }
    }

    // Fog of war's "remembered" tier: explored, but nobody has it in sight. Only aims the fade;
    // the tint moves in _Process. Called once a tick for every live view, so the no-change case
    // must cost nothing.
    public void SetRemembered(bool remembered)
    {
        if (!_remembered.Retarget(remembered))
        {
            return;
        }

        SetProcess(true);
    }

    // Straight to the end state, no fade - for a view created somewhere the group already left.
    // Touches no node, so WorldPresenter can call it before the view enters the tree.
    public void SnapRemembered(bool remembered) => _remembered.Snap(remembered);

    // The single place any layer's colour is written, always re-derived from its base modulate
    // so repeated calls cannot compound a tint. Alpha is left as it is on the sprite: that
    // channel belongs to the occlusion fade, and writing full alpha back would blink a ghosted
    // sprite solid once a frame.
    protected void ApplyTints()
    {
        foreach (var layer in _layers)
        {
            var color = _remembered.Applied(layer.BaseModulate);
            color.A = layer.Sprite.Modulate.A;
            layer.Sprite.Modulate = color;
        }
    }
}
