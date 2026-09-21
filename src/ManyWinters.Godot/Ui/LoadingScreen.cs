using Godot;

namespace ManyWinters.Godot.Ui;

// The title page held on screen while Main builds the world, with a bar and a line of text along
// the bottom saying what is being built. Light ink on the image, the same face and ink as the
// inscription overlay - the other thing the game sets over a dark ground.
//
// Every layer is a node that actually draws (ColorRect, TextureRect): a bare Control paints
// nothing, and a "panel" stylebox on one is silently ignored.
public partial class LoadingScreen : Control
{
    private const string SplashImagePath = "res://Content/splash/title_page.png";

    // How much of the screen's height the band along the bottom takes.
    private const float BandHeightFraction = 0.08f;

    private const int BarHeight = 6;
    private const int BarCornerRadius = 2;
    private const float BarWidth = 500f;
    private const int Spacing = 8;
    private const int StatusFontSize = 14;

    // The title page's own ground, laid under the image so the first frame is never a hole - the
    // texture is loaded here, and until it is there is nothing to draw.
    private static readonly Color Ground = new(0.32f, 0.29f, 0.26f);

    // The image goes dark towards the bottom but not evenly, and a word has to stay readable over
    // whichever part of it is under the bar.
    private static readonly Color Band = new(0f, 0f, 0f, 0.65f);

    private ProgressBar _bar = null!;
    private Label _status = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // The world behind is still being built; nothing under this is ready to be clicked.
        MouseFilter = MouseFilterEnum.Stop;

        AddChild(FullRect(new ColorRect { Color = Ground }));
        AddChild(FullRect(new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(SplashImagePath),
            // The page is drawn for one aspect ratio and the window is whatever the player made
            // it: fill the screen and lose the edges rather than letterbox or stretch the faces.
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        }));

        var band = new ColorRect
        {
            Color = Band,
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorLeft = 0f,
            AnchorTop = 1f - BandHeightFraction,
            AnchorRight = 1f,
            AnchorBottom = 1f,
        };
        AddChild(band);

        var centre = new CenterContainer();
        centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        band.AddChild(centre);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(BarWidth, 0) };
        column.AddThemeConstantOverride("separation", Spacing);
        centre.AddChild(column);

        _bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            // The number would promise a precision the steps do not have.
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, BarHeight),
        };
        _bar.AddThemeStyleboxOverride("background", Trough(new Color(InscriptionFont.Ink, 0.18f)));
        _bar.AddThemeStyleboxOverride("fill", Trough(new Color(InscriptionFont.Ink, 0.75f)));
        column.AddChild(_bar);

        _status = InscriptionFont.BodyLabel(string.Empty, StatusFontSize, new Color(InscriptionFont.Ink, 0.8f));
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        // One line: the steps are named short enough, and a wrap would shift the bar above it.
        _status.AutowrapMode = TextServer.AutowrapMode.Off;
        column.AddChild(_status);
    }

    public void Show(float progress, string step)
    {
        _bar.Value = Mathf.Clamp(progress, 0f, 100f);
        _status.Text = step;
    }

    private static StyleBoxFlat Trough(Color color) => new()
    {
        BgColor = color,
        CornerRadiusTopLeft = BarCornerRadius,
        CornerRadiusTopRight = BarCornerRadius,
        CornerRadiusBottomLeft = BarCornerRadius,
        CornerRadiusBottomRight = BarCornerRadius,
    };

    private static T FullRect<T>(T control) where T : Control
    {
        control.MouseFilter = MouseFilterEnum.Ignore;
        control.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return control;
    }
}
