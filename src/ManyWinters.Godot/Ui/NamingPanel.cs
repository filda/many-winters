using Godot;

namespace ManyWinters.Godot.Ui;

// The moment a thing nobody in the band has a word for gets one - its own small page, laid over
// the workshop rather than folded into it (see WorkshopPanel), so a discovery does not make the
// workbench itself grow and shrink around it.
//
// Time already stands still for this, the same clock the workshop underneath is holding: nothing
// here starts or stops it, it only asks a question of the player while the world waits.
public partial class NamingPanel : FloatingPanel
{
    private const float Width = 320f;
    private const int BodyFontSize = 15;
    private const int ImageSize = 96;

    private Label _description = null!;
    private TextureRect _image = null!;
    private LineEdit _name = null!;

    public NamingPanel()
        : base("Nobody has a word for this", onPaper: true)
    {
        CustomMinimumSize = new Vector2(Width, 0);
        // Centred the same way the workshop is, and added after it (see Main) - the two land on
        // the same spot, which is what reads as one page laid on top of the other rather than a
        // second window somewhere else on the screen.
        KeepCentred = true;
        Visible = false;
        Theme = PanelChrome.PaperButtons(BodyFontSize);
    }

    public override void _Ready()
    {
        base._Ready();
        Body.AddThemeConstantOverride("separation", 8);

        _description = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, InscriptionFont.DarkInk);
        _description.HorizontalAlignment = HorizontalAlignment.Center;
        Body.AddChild(_description);

        _image = new TextureRect
        {
            CustomMinimumSize = new Vector2(ImageSize, ImageSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        Body.AddChild(_image);

        Body.AddChild(InscriptionFont.BodyLabel("What is it called?", BodyFontSize, InscriptionFont.FadedDarkInk));

        _name = new LineEdit { PlaceholderText = "a name for it" };
        _name.TextSubmitted += _ => Confirm();
        Body.AddChild(_name);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        Body.AddChild(buttons);

        var confirm = new Button { Text = "Call it that" };
        confirm.Pressed += Confirm;
        buttons.AddChild(confirm);

        var cancel = new Button { Text = "Not now" };
        cancel.Pressed += Close;
        buttons.AddChild(cancel);
    }

    // Asked here and now rather than as an interruption of something else - the description and
    // picture are what the player is looking at already (ReportOutcome, WorkshopPanel.IconFor),
    // repeated here so the question is not left to memory.
    internal void Open(string description, Texture2D? image)
    {
        _description.Text = description;
        _image.Texture = image;
        _image.Visible = image is not null;
        _name.Text = string.Empty;
        Visible = true;
        _name.GrabFocus();
    }

    // What the player called it, or that they would rather not say right now - Main decides what
    // either means for the thing that was made (see Vocabulary).
    internal event Action<string>? Named;
    internal event Action? Cancelled;

    protected override void OnCloseRequested() => Close();

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Cancelled?.Invoke();
    }

    private void Confirm()
    {
        var word = _name.Text.Trim();
        if (word.Length == 0)
        {
            return;
        }

        Visible = false;
        Named?.Invoke(word);
    }
}
