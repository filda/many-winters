using Godot;

namespace ManyWinters.Godot.Ui;

// Debug only: the world's raw numbers and the levers that move them. What the player is meant
// to read and press lives in SelectionPanel; this window keeps the dump, the spawner, the
// extinguisher and the map reveal, none of which belong in the game proper (docs/todo/todo.md).
//
// Shut until the status bar's Inspector button is pressed. It used to open with the game and
// sit over the corner the band's roster now claims, which put a debug tool in front of the
// player before they had asked for one.
//
// Raises what the player pressed rather than acting on it: composition code decides what
// spawning, extinguishing, or revealing the map actually does to the world.
internal sealed class DebugInspector
{
    private const float Width = 340f;

    private readonly Label _infoLabel;
    private readonly Label _buildingsLabel;
    private readonly Label _gravesLabel;

    public event Action? SpawnRequested;
    public event Action? ExtinguishRequested;
    public event Action<bool>? RevealMapToggled;

    public DebugInspector(CanvasLayer canvas, StatusBar statusBar, PresentationSettings presentation)
    {
        var panel = new FloatingPanel("Inspector (debug)")
        {
            Position = new Vector2(16, 16),
            Visible = false,
            CustomMinimumSize = new Vector2(Width, 0),
            // A Theme resource cascades its DefaultFontSize down to every descendant Control that
            // doesn't set its own override - unlike AddThemeFontSizeOverride, which only affects
            // the single Control it's called on.
            Theme = new Theme { DefaultFontSize = presentation.InspectorFontSize },
        };
        canvas.AddChild(panel);
        statusBar.InspectorRequested += () => panel.Visible = !panel.Visible;

        _infoLabel = new Label
        {
            Text = "No selection.",
            CustomMinimumSize = new Vector2(Width, 0),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        panel.Body.AddChild(_infoLabel);

        var spawnButton = new Button { Text = "Spawn Person" };
        spawnButton.Pressed += () => SpawnRequested?.Invoke();
        panel.Body.AddChild(spawnButton);

        // The quick way to the epitaph and its "Another band comes" offer (docs/todo/todo.md):
        // the epitaph of a band nobody is left in carries no closing words, so without this the
        // only way to that screen is playing the band out by hand.
        var extinguishButton = new Button { Text = "Extinguish Band" };
        extinguishButton.Pressed += () => ExtinguishRequested?.Invoke();
        panel.Body.AddChild(extinguishButton);

        // A development view, not a gameplay one (see RevealableExploration): the whole map as if
        // fog of war did not exist.
        var revealMapToggle = new CheckButton { Text = "Reveal Map" };
        revealMapToggle.Toggled += toggledOn => RevealMapToggled?.Invoke(toggledOn);
        panel.Body.AddChild(revealMapToggle);

        _buildingsLabel = new Label { Text = "Buildings: none" };
        panel.Body.AddChild(_buildingsLabel);

        _gravesLabel = new Label { Text = "Graves: none" };
        panel.Body.AddChild(_gravesLabel);
    }

    public void ShowInfo(string text) => _infoLabel.Text = text;

    public void ShowBuildings(string text) => _buildingsLabel.Text = text;

    public void ShowGraves(string text) => _gravesLabel.Text = text;
}
