using Godot;

namespace ManyWinters.Godot.Ui;

// The same translucent card every floating panel over the game world sits on - the inspector,
// the chronicle, and the pause panel - so they read as one UI language instead of each
// inventing its own frame.
public static class PanelChrome
{
    public static StyleBoxFlat Background() => new()
    {
        BgColor = new Color(0f, 0f, 0f, 0.6f),
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 10,
        ContentMarginBottom = 10,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
    };
}
