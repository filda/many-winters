using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// What may be done with the thing the player pointed at: its name, and the actions under it.
// This is where every order aimed at something in the world is given - fell that tree, bury him,
// put the wood in there, build here - so that a person's own card is left with only what they do
// to themselves.
//
// A page like the rest of what the player holds, not the engine's grey PopupMenu: the game reads
// as a chronicle, and a menu is no less part of it than the card it was opened from. It draws its
// actions with the same control the card does, so the two cannot drift apart.
//
// Opened at the cursor and closed the moment something is pressed or the player looks elsewhere.
// It keeps the offers it was opened with rather than refreshing them: the world moves on while
// it is up, and a menu that reshuffles under the cursor is unusable. A line gone stale by the
// time it is pressed is caught where every action is - the command asks its own preconditions
// again before doing anything.
internal partial class ContextMenu : PanelContainer
{
    // Narrower than the selection card: these lines are short verbs, and a menu as wide as a
    // panel reads as a panel that has landed in the wrong place.
    private const float Width = 230f;

    private const int HeadingFontSize = 22;
    private const int BodyFontSize = 15;
    private const int SectionSpacing = 6;

    // How far the menu keeps off the edge of the screen when it is pushed back inside it.
    private const float ScreenMargin = 8f;

    private Label _heading = null!;
    private ActionList _actions = null!;

    // The owner runs the pressed action; the menu only knows what an offer is, not what
    // executing one means for the rest of the game.
    internal event Action<ActionOffer>? ActionInvoked;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        AddThemeStyleboxOverride("panel", PanelChrome.Parchment());
        // Added first, so every label and button that follows sits on top of the grain.
        AddChild(PanelChrome.Grain("menu"));
        Theme = PanelChrome.PaperButtons(BodyFontSize);

        // The padding lives here rather than in the StyleBox, so the grain above reaches the
        // paper's own edge instead of stopping at a clean frame.
        var padding = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            padding.AddThemeConstantOverride(side, PanelChrome.PaperPadding);
        }

        AddChild(padding);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(Width - (PanelChrome.PaperPadding * 2), 0) };
        column.AddThemeConstantOverride("separation", SectionSpacing);
        padding.AddChild(column);

        // The title face, as on the selection card's name: what was pointed at is the world's,
        // not ours.
        _heading = InscriptionFont.TitleLabel(string.Empty, HeadingFontSize, InscriptionFont.DarkInk);
        column.AddChild(_heading);

        _actions = new ActionList();
        _actions.ActionInvoked += offer => ActionInvoked?.Invoke(offer);
        column.AddChild(_actions);
    }

    internal void Open(string heading, IReadOnlyList<ActionOffer> offers, Vector2 screenPosition)
    {
        _heading.Text = heading;
        _actions.Show(offers);
        Position = screenPosition;
        Visible = true;
    }

    internal void Close() => Visible = false;

    // Pushed back inside the screen from here rather than at Open: the menu's height is whatever
    // the actions on it add up to, and the engine only knows that once it has laid them out.
    // Costs one calculation per frame while the menu is up, and nothing at all while it is not.
    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        Position = ScreenPlacement.KeptOnScreen(Position, Size, GetViewport().GetVisibleRect().Size, ScreenMargin);
    }
}
