using Godot;
using ManyWinters.Godot.Interaction;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

// Everything the world is drawn out of - people, resources, graves, buildings - is the same
// kind of thing seen from here: one or more billboarded sprite layers standing on the ground,
// dimmed while the group cannot see the place (fog of war's "remembered" tier), ghosted while
// they block the view of the selection, and for most of them hoverable and clickable to the
// pixel. All of that used to be written out once per view, and the four copies had drifted:
// graves and buildings never dimmed with the fog at all, buildings alone had no collision
// shape, the hover highlight grew a sprite past its own click rectangle, and a person could
// only be picked by their body and never by the cloak hanging off it.
//
// What stays with each view is what genuinely differs: which layers it draws out of which
// textures, what its own seed varies, what a click on it means, and any animation of its own.
//
// Not unit-testable, and not meant to be - a Node-derived type cannot be constructed outside
// the engine at all (see the test project's README). Everything here that is a function of its
// inputs lives in Logic/ instead (RememberedFade, SpriteExtents, BillboardUv, HoverArbiter,
// WalkCycle); this is the thin shell that fetches values, calls those, and puts the answer on
// a node.
internal abstract partial class SpriteEntityView : Area3D, IHoverable
{
    // One drawn layer. TexturePath and BaseModulate are settable because a layer can be
    // re-pointed at a different image (PersonView swaps in the lying-down variants on death)
    // and re-coloured wholesale (that same death draining the colour out of it).
    protected sealed class SpriteLayer(Sprite3D sprite, string texturePath, bool picks, bool outlines)
    {
        public Sprite3D Sprite { get; } = sprite;

        public string TexturePath { get; set; } = texturePath;

        // What this layer looks like in full sight, before the fog tint or a hover highlight
        // is multiplied into it. Taken from the sprite as it was registered, so whatever
        // variation the view baked in first - a tree canopy's brightness jitter, a garment's
        // colour - is part of it.
        public Color BaseModulate { get; set; } = sprite.Modulate;

        // Whether a cursor over this layer's opaque pixels counts as being on the entity. A
        // fruit overlay does not: it is drawn inside the canopy's own silhouette, so it has no
        // pixels of its own to add.
        public bool Picks { get; } = picks;

        // Whether this layer is part of the shape the hover rim traces, which is not the same
        // question. A tree's branch layer is pickable - a click on a bare twig should select the
        // tree - but tracing it draws a bright three-pixel line around a one-pixel dark twig,
        // which reads as a squiggle floating in the air beside the canopy rather than as part of
        // the tree's outline.
        public bool Outlines { get; } = outlines;
    }

    private readonly List<SpriteLayer> _layers = new();
    private readonly RememberedFade _remembered = new();

    // Null for a view that never lights up under the cursor (a grave, a building). Null for
    // _onMissedClick too means nothing can be clicked either, and then the view gets no
    // collision shape and no ray picking at all - see IsPickable.
    private readonly HoverArbiter? _hover;
    private readonly InputEventEventHandler? _onMissedClick;

    private CollisionShape3D? _collisionShape;
    private bool _isHovered;

    // The world height every one of this view's layers is created at (BillboardSprite.Create),
    // and the height WorldPresenter assumed when it placed this node - so it is also what the
    // ground shadow and the ground-contact correction below are measured against.
    protected float NominalHeight { get; }

    protected SpriteEntityView(float nominalHeight, HoverArbiter? hover, InputEventEventHandler? onMissedClick)
    {
        NominalHeight = nominalHeight;
        _hover = hover;
        _onMissedClick = onMissedClick;
    }

    private bool IsPickable => _onMissedClick is not null;

    // Sealed, so that the order of the things every view has to do - build the layers, then
    // measure them, then paint them - is settled once here instead of being re-established
    // (and mis-established) in four separate _Ready overrides. Views build themselves in
    // Build() instead.
    public sealed override void _Ready()
    {
        Build();

        if (IsPickable)
        {
            InputRayPickable = true;
            RefreshCollisionShape();
            // No MouseExited subscription: Godot only ever sends that to the one collider its
            // own picking chose, which is exactly what used to leave sprites lit forever (see
            // HoverArbiter). Losing hover is settled once a frame by IsStillUnderCursor.
            InputEvent += OnInputEvent;
        }
        else
        {
            InputRayPickable = false;
        }

        // Frames are processed only while something actually has to move - a fade in flight,
        // or a view that animates itself. There are thousands of resource nodes and nearly all
        // of them sit perfectly still nearly all of the time.
        SetProcess(NeedsEveryFrame || _remembered.IsFading);
        ApplyTints();
    }

    // Where a view creates its layers (Register), its ground shadow (SetUpGroundShadow) and
    // its seeded scale (ScaleAndKeepGroundContact). Called from _Ready, so this node is in the
    // tree and WorldPresenter has already set its Position.
    protected abstract void Build();

    // For a view with an animation of its own, which therefore cannot have its processing
    // switched off between fades (PersonView's walk cycle).
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

    // A view leaving the scene drops out of the hover arbiter without being called back into -
    // QueueFree has already been asked for, and touching a freed node is a crash rather than a
    // stale highlight.
    public sealed override void _ExitTree() => _hover?.Forget(this);

    // Takes over a sprite the view has just created. Creation stays with the view because that
    // is where the reasoning about alpha cut, render priority and occlusion-fade exclusion
    // belongs (see BillboardSprite.Create); from here on this class tints it, scales it,
    // measures it and picks against it along with all the others.
    protected SpriteLayer Register(Sprite3D sprite, string texturePath, bool picks = true, bool outlines = true)
    {
        var layer = new SpriteLayer(sprite, texturePath, picks, outlines);
        _layers.Add(layer);
        AddChild(sprite);
        return layer;
    }

    // Re-points a layer at a different image, keeping its world height. BillboardSprite.Apply
    // resets Modulate to white, so the layer's base colour is handed back in here rather than
    // left for the caller to remember to restore afterwards.
    protected void Retexture(SpriteLayer layer, string texturePath, Color baseModulate, Color fallbackColor)
    {
        BillboardSprite.Apply(layer.Sprite, texturePath, NominalHeight, fallbackColor);
        layer.TexturePath = texturePath;
        layer.BaseModulate = baseModulate;
    }

    // The soft blob under the entity, seated on the ground at where a sprite of NominalHeight
    // has its bottom edge. Not a layer: it is never tinted, never scaled by hover and never
    // picked against (and docs/todo/todo.md has the plan to give it a real silhouette).
    protected void SetUpGroundShadow(float diameter)
    {
        var groundShadow = GroundShadow.Create(diameter);
        groundShadow.Position += new Vector3(0f, (-NominalHeight / 2f) + GroundShadow.GroundOffset, 0f);
        AddChild(groundShadow);
    }

    // WorldPresenter put this node's origin at groundHeight + NominalHeight/2, which lands a
    // sprite's bottom edge exactly on the ground - as long as the scale stays 1. Scaling
    // multiplies that -NominalHeight/2 before it is added to Position, so anything shorter
    // floats with a gap under it and anything taller sinks in - the "trees clearly in the air"
    // a live check turned up. Shifting Position by the same amount the scale just displaced the
    // contact point cancels it back out, whichever way it went.
    protected void ScaleAndKeepGroundContact(float widthScale, float heightScale)
    {
        Scale = new Vector3(widthScale, heightScale, widthScale);
        Position += new Vector3(0f, (NominalHeight / 2f) * (heightScale - 1f), 0f);
    }

    // The entity's drawn silhouette in this node's own local metres: the union of its picking
    // layers' visible extents - a split tree's trunk and canopy together reconstruct exactly
    // what one combined image used to be - each scaled by whatever its own sprite is scaled to.
    // Nothing scales a single layer today (hover stopped doing it - see ShowHovered), so that
    // factor is 1 in every current caller; it stays in because a layer's own scale is the one
    // thing this cannot read off anywhere else. This node's own scale is deliberately not in
    // it: the engine applies that to every child, so counting it here would apply it twice.
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

    // How far above this node's own origin the top of the drawn silhouette sits, in *world*
    // metres - for Main's screen-space selection marker, which adds it to GlobalPosition. This
    // node's own scale is in it for that reason, unlike in the local extent above.
    public float TopHeightOffset
    {
        get
        {
            var extent = VisibleExtent();
            return (extent.CenterYOffset + (extent.Height / 2f)) * Scale.Y;
        }
    }

    // Cut to the drawn silhouette rather than the full square canvas - a canopy or a standing
    // figure does not fill its canvas, so a shape the nominal size would hover and click well
    // outside anything visible. Re-derived whenever what is drawn changes, which today means a
    // different texture: a corpse lies down, wider and shorter than the figure that fell.
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

        // A rim traced around the silhouette, and nothing else: hover used to bump every layer
        // to 1.1 and multiply a yellow tint into it, which grew the thing under the cursor away
        // from its own click rectangle and washed out the drawing it was meant to point out.
        // Geometry is untouched now, so nothing here has to be re-measured either.
        ShowOutline(hovered);
    }

    // The rim that says what the cursor is on, traced once around the whole entity rather than
    // once per layer - see HoverOutline. Only the layers that make up the readable silhouette go
    // into it (SpriteLayer.Outlines): a fruit overlay sits inside the canopy's own shape and has
    // no edge worth tracing, and a branch layer's twigs are too fine to trace without the line
    // reading as a squiggle beside the tree. The rim hangs on the last of them, the one drawn on
    // top, so it composites over the others.
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

    // Lets HoverRescue ask "is this exact point actually opaque on you", for when some other
    // entity's broad-phase box won the pick instead - see its own doc comment for why that is
    // not just a hypothetical. A view that never lights up answers no, so the rescue carries
    // on past it to whatever is really under the cursor.
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

    // Asked once a frame while this view holds the highlight (HoverArbiter.Revalidate) - the
    // same test as above but from wherever the cursor is right now rather than from a picking
    // event, since the everyday ways a highlight got stuck all consist of no picking event
    // arriving at all. A cursor over any UI panel counts as off: physics picking never fires
    // under a Control, so the sprite behind one would otherwise stay lit.
    public bool IsStillUnderCursor()
    {
        var viewport = GetViewport();
        return viewport.GuiGetHoveredControl() is null
            && viewport.GetCamera3D() is { } camera
            && IsOpaqueAtScreen(camera, viewport.GetMousePosition());
    }

    public bool TryClickAt(Camera3D camera, Vector3 worldPosition, MouseButton button) =>
        WantsClick(button) && IsOpaqueAt(camera, worldPosition) && OnClicked(button);

    // Which buttons this view answers to. A button it does not want is left entirely alone -
    // not even the missed-click fallback runs - so a right-click on a tree stays as inert as
    // it has always been rather than quietly becoming a ground order.
    protected virtual bool WantsClick(MouseButton button) => button == MouseButton.Left;

    // What a click on this entity means. False means "not mine after all", which sends the
    // click on down the same fallback chain as a click that missed the pixels.
    protected virtual bool OnClicked(MouseButton button) => false;

    // Pins the hit-test plane to a stable anchor instead of each sprite's own GlobalPosition -
    // PersonView's walk bob moves its layers' local Position every frame, which otherwise
    // sweeps the sampled pixel across silhouette edges under a cursor that never moved and
    // reads as the hover flickering on and off. Null means "each sprite's own position", which
    // is right for everything that does not animate its layers.
    protected virtual Vector3? PixelHitAnchor => null;

    private bool IsOpaqueAt(Camera3D camera, Vector3 worldPosition) =>
        IsOpaqueAtScreen(camera, camera.UnprojectPosition(worldPosition));

    // A point is on the entity if it lands on any picking layer's opaque pixels: a split tree's
    // layers together are the silhouette one combined texture used to be (branches included - a
    // click on a bare twig tip should still select the tree), and the cloak hanging off a
    // person is as much them as the body underneath it.
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
                // Nothing opaque here and nothing behind it either means the cursor is over
                // bare ground showing through, so whatever was lit has been left behind.
                if (!TryHoverAt(camera3D, position) && !HoverRescue.TryHoverElsewhere(this, camera3D, position))
                {
                    _hover.Clear();
                }

                break;
            // The broad-phase collision box is bigger than the silhouette inside it - Godot
            // only delivers a click to the nearest pickable collider along the ray, so a click
            // landing inside the box but off the opaque pixels (on the ground shadow at a
            // person's feet, say) would otherwise be silently swallowed here instead of
            // reaching the ground underneath. Try whatever else is genuinely at this point
            // first (HoverRescue's click counterpart), and only then fall all the way back to a
            // plain ground-click order.
            case InputEventMouseButton { Pressed: true } mouseEvent when WantsClick(mouseEvent.ButtonIndex):
                if (!TryClickAt(camera3D, position, mouseEvent.ButtonIndex)
                    && !HoverRescue.TryClickElsewhere(this, camera3D, position, mouseEvent.ButtonIndex))
                {
                    _onMissedClick?.Invoke(camera, @event, position, normal, shapeIdx);
                }

                break;
        }
    }

    // Fog of war's "remembered" tier (WorldPresenter.RefreshExploration) - explored, but nobody
    // has this place in sight right now. Only aims the fade; the tint itself moves a frame at a
    // time in _Process. Called once a tick for every live view, so the no-change case has to
    // cost nothing, which is what RememberedFade.Retarget answers.
    public void SetRemembered(bool remembered)
    {
        if (!_remembered.Retarget(remembered))
        {
            return;
        }

        SetProcess(true);
    }

    // Straight to the end state, no fade - for a view created for somewhere the group has
    // already left, where there was never anything on screen to fade out of. Touches no node,
    // so WorldPresenter can call it before this view enters the scene tree.
    public void SnapRemembered(bool remembered) => _remembered.Snap(remembered);

    // Every layer's displayed colour, always re-derived from its own base modulate so that
    // repeated calls cannot compound a tint. The single place any layer gets written, so the
    // fog fade, hover and a view's own state changes cannot disagree about what the other two
    // did. Alpha is left exactly as it is on the sprite: that channel belongs to Main's
    // occlusion fade, which re-applies it every frame (see BillboardSprite.OcclusionFadedSprites),
    // and a fade writing full alpha back would blink a ghosted sprite solid once a frame for as
    // long as it ran.
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
