using Godot;

namespace ManyWinters.Godot.Ui;

// The translucent card every panel over the game world sits on - inspector, chronicle, pause
// panel, status bar - so they read as one UI language.
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
