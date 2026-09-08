using Godot;
using ManyWinters.Core.Construction;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Views;

public partial class BuildingView(BuildingId buildingId, BuildingKindId kind) : Node3D
{
    // Was 1.2 - shorter than PersonView.Height (1.8), reading as knee-high next to a person
    // despite the art depicting a door someone could actually walk through. A modest one-room
    // hut should clear a person's head with some roof to spare.
    public const float Size = 2.8f;
    private const float MinScale = 0.9f;
    private const float MaxScale = 1.1f;
    private const float ShadowDiameter = 3.5f;

    private Sprite3D _sprite = null!;
    private Color _baseModulate;

    // Same fade as every other view (see RememberedFade): a hut the group has walked away
    // from is a place they remember standing there, not one they are currently looking at.
    private readonly RememberedFade _remembered = new();

    public override void _Ready()
    {
        var fallbackColor = EntityVisualVariation.Tint(ColorFor(kind), buildingId.Seed);
        var scale = EntityVisualVariation.Scale(buildingId.Seed, MinScale, MaxScale);
        Scale = Vector3.One * scale;
        // Same ground-contact fix as PersonView/ResourceNodeView: WorldPresenter set this
        // node's own Position assuming Scale stayed 1, so Scale.Y != 1 shifts the sprite's
        // (and the ground shadow's, both children scaled along with it) bottom edge away
        // from the ground by Size/2*(scale-1). Shifting Position back by that same amount
        // cancels it out.
        Position += new Vector3(0f, (Size / 2f) * (scale - 1f), 0f);

        var groundShadow = GroundShadow.Create(ShadowDiameter);
        groundShadow.Position += new Vector3(0, (-Size / 2f) + GroundShadow.GroundOffset, 0);
        AddChild(groundShadow);

        _sprite = BillboardSprite.Create(TexturePaths.ForBuilding(kind), Size, fallbackColor);
        _baseModulate = _sprite.Modulate;
        AddChild(_sprite);

        // Processing frames only while a fade is actually running - a settled camp is a
        // handful of huts that sit unchanged for hours of play.
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

    // A building is neither hoverable nor selectable yet, so there is no hover state to
    // compose with here.
    private void ApplyTint() => SpriteLayerTint.Apply(_sprite, _baseModulate, _remembered, hovered: false);

    private static Color ColorFor(BuildingKindId kind)
    {
        var path = $"res://Content/buildings/{kind.Value}/{kind.Value}.tres";
        var visual = ResourceLoader.Exists(path) ? ResourceLoader.Load<BuildingVisualDefinition>(path) : null;
        return visual?.Color ?? new Color(0.6f, 0.6f, 0.6f);
    }
}
