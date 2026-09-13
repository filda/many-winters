using Godot;

namespace ManyWinters.Godot.Ui;

// Shown while Main holds the clock at the player's request rather than for a narrative beat
// (InscriptionOverlay): the world stands still, the camera keeps working, and it shows what to
// come back to - the band's name in the title face, and who is left. On a PanelChrome card
// rather than bare over the world: a status screen opened at will, not a moment the world is
// having.
public partial class PausePanel : Control
{
    private const int TitleFontSize = 60;
    private const int BodyFontSize = 20;
    private const float ColumnWidth = 640f;
    private const int Spacing = 20;

    private Label _title = null!;
    private Label _sinceArrival = null!;
    private Label _population = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Swallows clicks anywhere on screen, not just over the card - same reasoning as
        // InscriptionOverlay.
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var centre = new CenterContainer();
        centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centre);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelChrome.Background());
        centre.AddChild(panel);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ColumnWidth, 0) };
        column.AddThemeConstantOverride("separation", Spacing);
        panel.AddChild(column);

        var notice = InscriptionFont.BodyLabel("Paused. Time does not pass. Press Space to resume.", BodyFontSize, InscriptionFont.Ink);
        notice.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(notice);

        _title = InscriptionFont.OutlinedTitleLabel(string.Empty, TitleFontSize);
        column.AddChild(_title);

        _sinceArrival = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.Ink);
        _sinceArrival.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_sinceArrival);

        _population = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.Ink);
        _population.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_population);
    }

    public void Show(string bandName, string sinceArrival, string population)
    {
        _title.Text = bandName;
        _sinceArrival.Text = $"{sinceArrival} since arrival";
        _population.Text = population;
        Visible = true;
    }
}
