using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

// Bottom-of-screen bar: transient notifications (left, auto-clearing), tick/season (right),
// and a help popup - the reference text that used to permanently occupy screen space now
// only shows up when asked for.
public partial class StatusBar : PanelContainer
{
    // Tall enough to give the "?" button (which drives its own theme minimum height) and
    // a centered line of text room inside PanelBackground's 10px top/bottom margins -
    // 36f left only 16px of interior, so content was being squeezed past the panel edge.
    private const float BarHeight = 48f;
    private const float NotificationSeconds = 4f;

    private const string HelpText =
        "WASD/arrows: pan camera. Q/E: rotate. R/F: zoom. T: toggle ortho/perspective.\n\n" +
        "Left-click: select person. Right-click another person: teach them what the " +
        "selected person knows. Click a resource node: gather (needs a selected person). " +
        "Click a grave: view its record. Click empty ground: walk there (needs a selected " +
        "person).";

    private Label _notificationLabel = null!;
    private Label _tickLabel = null!;
    private global::Godot.Timer _notificationTimer = null!;

    public override void _Ready()
    {
        // Anchored to the bottom edge with both top and bottom anchors at 1 (not
        // SetAnchorsPreset - with no explicit offsets it collapses the rect to zero
        // height), then pulled up by a fixed BarHeight via OffsetTop so it actually has a
        // visible height and stays pinned to the bottom across viewport resizes.
        AnchorLeft = 0f;
        AnchorRight = 1f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetLeft = 0f;
        OffsetRight = 0f;
        OffsetTop = -BarHeight;
        OffsetBottom = 0f;

        var row = new HBoxContainer();
        AddChild(row);

        _notificationLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.AddChild(_notificationLabel);

        _tickLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
        row.AddChild(_tickLabel);

        var helpButton = new Button { Text = "?", CustomMinimumSize = new Vector2(28, 0) };
        helpButton.Pressed += ShowHelp;
        row.AddChild(helpButton);

        _notificationTimer = new global::Godot.Timer { WaitTime = NotificationSeconds, OneShot = true };
        _notificationTimer.Timeout += () => _notificationLabel.Text = string.Empty;
        AddChild(_notificationTimer);
    }

    // Transient feedback ("select a person first", "too far away", ...) - separate from
    // the inspector, which only ever shows the current selection's actual persistent state.
    public void Notify(string message)
    {
        _notificationLabel.Text = message;
        _notificationTimer.Start();
    }

    public void SetTick(long tick, Season season)
    {
        _tickLabel.Text = $"Tick: {tick}  Season: {season}";
    }

    private void ShowHelp()
    {
        var dialog = new AcceptDialog
        {
            Title = "Controls",
            DialogText = HelpText,
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(440, 200));
    }
}
