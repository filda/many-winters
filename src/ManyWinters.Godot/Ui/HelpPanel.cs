using Godot;

namespace ManyWinters.Godot.Ui;

// The controls, on a page of the same paper as the pause screen. It was an engine AcceptDialog
// holding one long paragraph, which grew as wide as the paragraph was and hung off both edges of
// the screen; a grey system box is also not what anything else here is made of. Two short columns
// of headed lines instead: they fit on the smallest window the game opens in without a scrollbar,
// and the eye finds a key without reading a wall of prose.
//
// Like the pause panel it holds the clock while it is up (see Main), so nobody starves behind it,
// and it swallows clicks into the world for the same reason - an order given to a stopped world
// would land the moment it starts again. The camera keeps working: the point of a controls page
// is to try them.
public partial class HelpPanel : Control
{
    private const int TitleFontSize = 44;
    private const int HeadingFontSize = 20;
    private const int BodyFontSize = 17;
    private const int ButtonFontSize = 18;

    // Wide enough that most lines do not wrap at all; two of them and the padding still leave
    // room on either side of a 1152px window, which is the narrowest the game opens in.
    private const float ColumnWidth = 360f;
    private const int ColumnGap = 28;
    private const int Spacing = 18;
    private const int LineSpacing = 2;

    // Left column, then right. Grouped by what the player is doing rather than by which button
    // does it: everything about moving the view is one habit, giving orders is another.
    private static readonly (string Heading, string[] Lines)[] LeftColumn =
    [
        ("Looking", [
            "WASD or the arrow keys — move about",
            "Q and E, or drag with the right button — turn",
            "R and F, or the mouse wheel — closer and further",
            "Page Up and Page Down — tilt the view",
            "T — flat or in perspective",
        ]),
        ("Time", [
            "Space — hold the world still, and let it go again",
        ]),
    ];

    private static readonly (string Heading, string[] Lines)[] RightColumn =
    [
        ("Orders — somebody has to be picked first", [
            "Left-click a person — pick them",
            "Left-click a plant or a tree — gather from it",
            "Left-click the ground — walk there",
            "Right-click anything — every order it will take",
            "Escape — put the menu away",
        ]),
        ("Windows", [
            "Band — everyone in a list; press a name to go to them",
            "Chronicle — the inscriptions, once there are any",
            "Inspector — the raw numbers, a tool rather than the game",
        ]),
    ];

    // "Back to the land" was pressed, or Escape: the page comes down.
    public event Action? Dismissed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        Theme = InscriptionFont.BodyTheme(ButtonFontSize);

        var centre = new CenterContainer();
        centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centre);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelChrome.Parchment());
        centre.AddChild(panel);
        panel.AddChild(PanelChrome.Grain());

        var padding = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            padding.AddThemeConstantOverride(side, PanelChrome.PaperPadding);
        }

        panel.AddChild(padding);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", Spacing);
        padding.AddChild(page);

        page.AddChild(InscriptionFont.PaperTitleLabel("How this is played", TitleFontSize));

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", ColumnGap);
        page.AddChild(columns);
        columns.AddChild(Column(LeftColumn));
        columns.AddChild(Column(RightColumn));

        var away = new CenterContainer();
        page.AddChild(away);

        var back = new Button { Text = "Back to the land" };
        back.Pressed += Dismiss;
        away.AddChild(back);
    }

    public void Toggle()
    {
        if (Visible)
        {
            Dismiss();
            return;
        }

        Visible = true;
    }

    public void Dismiss()
    {
        Visible = false;
        Dismissed?.Invoke();
    }

    private static VBoxContainer Column((string Heading, string[] Lines)[] sections)
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ColumnWidth, 0) };
        column.AddThemeConstantOverride("separation", Spacing);

        foreach (var (heading, lines) in sections)
        {
            var section = new VBoxContainer();
            section.AddThemeConstantOverride("separation", LineSpacing);
            column.AddChild(section);

            section.AddChild(InscriptionFont.BodyBoldLabel(heading, HeadingFontSize, InscriptionFont.DarkInk));
            foreach (var line in lines)
            {
                section.AddChild(InscriptionFont.BodyLabel(line, BodyFontSize, InscriptionFont.DarkInk));
            }
        }

        return column;
    }
}
