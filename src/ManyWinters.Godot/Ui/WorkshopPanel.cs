using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// The workbench: everything one person is carrying, and the one question the player may ask of
// it - take one thing or two, and see what comes of putting them together (see
// docs/materials-and-crafting-architecture.md section 7).
//
// Deliberately not a list of verbs. There is one button and it says "Try it": the player forms
// the hypothesis and the simulation rules on it, which is the loop worth playing. A menu of
// Twist/Bind/Knap would hand them the answer before they had the idea.
//
// Time stands still while this is open, the way it does for the pause page - tinkering is meant
// to be unhurried, not something to rush before the world moves on. Main holds the clock for
// whichever of those is visible.
public partial class WorkshopPanel : FloatingPanel
{
    // Wider than the cards that sit beside the world: this one is the workbench itself, in the
    // middle of the screen (CentreOnScreen), and what a thing is made of runs long enough that a
    // narrow column broke half the lines. Wide and low rather than tall - a bench is a surface
    // things are laid out on, and a column of carried things reaching down the screen reads as an
    // inventory screen.
    private const float Width = 640f;
    private const int BodyFontSize = 15;
    private const int SectionSpacing = 6;

    // The pack is laid out across the bench rather than down it - things side by side, the way
    // they would be if they had been tipped out onto it.
    private const int Columns = 8;

    // How far the pack may reach before it scrolls within the bench instead of pushing everything
    // under it further down. Two rows of things - past that it is a hoard being sorted rather than
    // a handful of things being looked over.
    private const float MaxPackHeight = 128f;

    // One thing's square of bench, how far its picture sits from the edges of that square, and how
    // much bench is left between two of them.
    private const int TileSize = 56;
    private const int IconMargin = 6;
    private const int TileSpacing = 6;

    // The mark in the corner of a picture saying how many of that thing are held. Small: it is a
    // note on the picture, not a caption under it.
    private const int CountFontSize = 12;

    // What a thing nobody has drawn yet comes out as - the same tint the world falls back to for
    // an item with no icon (see ItemPileView).
    private static readonly Color Undrawn = new(0.55f, 0.45f, 0.3f, 0.55f);
    private const int ScrollbarWidth = 16;

    private static readonly Color Ink = InscriptionFont.DarkInk;
    private static readonly Color QuietInk = InscriptionFont.FadedDarkInk;

    private readonly List<PickTile> _tiles = [];
    private readonly List<WorkshopEntry> _picked = [];

    private GridContainer _entries = null!;
    private ScrollContainer _pack = null!;
    private Label _hint = null!;
    private Label _words = null!;
    private Button _try = null!;
    private Label _outcome = null!;
    private VBoxContainer _naming = null!;
    private LineEdit _name = null!;
    private ActionList _recipes = null!;
    private IReadOnlyList<WorkshopEntry> _carried = [];

    public WorkshopPanel()
        : base("Workshop", onPaper: true)
    {
        CustomMinimumSize = new Vector2(Width, 0);
        // The bench is what the player is doing, not a card beside the world: it holds the middle
        // of the screen, and keeps it when the window goes fullscreen and back.
        KeepCentred = true;
        Visible = false;
        Theme = PanelChrome.PaperButtons(BodyFontSize);
    }

    public override void _Ready()
    {
        base._Ready();
        Body.AddThemeConstantOverride("separation", SectionSpacing);

        _hint = InscriptionFont.BodyLabel("Take one thing, or two.", BodyFontSize, QuietInk);
        Body.AddChild(_hint);

        // The pack keeps its own scroll, so a big haul stays inside the bench: the window's own
        // scroll only starts once the whole thing has reached the foot of the screen, which is
        // exactly the height this panel is meant not to have.
        _pack = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        Body.AddChild(_pack);

        _entries = new GridContainer
        {
            Columns = Columns,
            CustomMinimumSize = new Vector2(Width - (PanelChrome.PaperPadding * 2) - ScrollbarWidth, 0),
        };
        _entries.AddThemeConstantOverride("v_separation", TileSpacing);
        _entries.AddThemeConstantOverride("h_separation", TileSpacing);
        _pack.AddChild(_entries);

        // Recipes are named up front (see WorkshopActions.Recipes) - a plain column of buttons,
        // the same control the selected person's card uses for the same reason (see ActionList).
        _recipes = new ActionList();
        _recipes.ActionInvoked += offer => RecipeInvoked?.Invoke(offer);
        Body.AddChild(_recipes);

        // What the thing in hand is like, never what it is for (see MaterialWords).
        _words = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, QuietInk);
        _words.Visible = false;
        Body.AddChild(_words);

        _try = new Button { Text = "Try it", Alignment = HorizontalAlignment.Left, Disabled = true };
        _try.Pressed += OnTryPressed;
        Body.AddChild(_try);

        _outcome = InscriptionFont.BodyLabel(string.Empty, BodyFontSize, Ink);
        _outcome.Visible = false;
        Body.AddChild(_outcome);

        // Only ever up in the moment a thing nobody has a word for has just been made. Naming
        // is not a screen the player visits; it is the discovery itself asking to be called
        // something (see Vocabulary).
        _naming = new VBoxContainer { Visible = false };
        _naming.AddThemeConstantOverride("separation", SectionSpacing);
        Body.AddChild(_naming);

        _naming.AddChild(InscriptionFont.BodyLabel("Nobody has a word for this. What is it called?", BodyFontSize, QuietInk));

        _name = new LineEdit { PlaceholderText = "a name for it" };
        _name.TextSubmitted += _ => Christen();
        _naming.AddChild(_name);

        var christen = new Button { Text = "Call it that", Alignment = HorizontalAlignment.Left };
        christen.Pressed += Christen;
        _naming.AddChild(christen);
    }

    // Opened fresh: nothing picked, nothing yet said about the last attempt.
    internal void Open(IReadOnlyList<WorkshopEntry> carried, IReadOnlyList<ActionOffer> recipes)
    {
        _picked.Clear();
        _outcome.Visible = false;
        _naming.Visible = false;
        Visible = true;
        Show(carried);
        ShowRecipes(recipes);
    }

    // Redrawn whenever the pack does (see Show) - Main asks for both together after anything
    // that could have changed what is carried.
    internal void ShowRecipes(IReadOnlyList<ActionOffer> recipes) => _recipes.Show(recipes);

    // Pressed on a recipe line. Main runs it, the same way it runs a line off the person's own
    // card (see ActionInvoked there) - this panel knows what an offer is, not what making one
    // means for the rest of the game.
    internal event Action<ActionOffer>? RecipeInvoked;

    // The clock is held while the bench is out, so the cross cannot simply hide it.
    protected override void OnCloseRequested() => Close();

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Closed?.Invoke();
    }

    // Put away, so the world can start moving again (see Main).
    internal event Action? Closed;

    // Redrawn after every attempt, because the pack has changed underneath it. A pick that is no
    // longer in the pack - the grass that just became cord - quietly stops being picked.
    internal void Show(IReadOnlyList<WorkshopEntry> carried)
    {
        _carried = carried;
        _picked.RemoveAll(picked => !carried.Contains(picked));

        while (_tiles.Count < carried.Count)
        {
            _tiles.Add(NewTile());
        }

        for (var i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].Apply(i < carried.Count ? carried[i] : null, i < carried.Count && _picked.Contains(carried[i]));
        }

        _hint.Text = carried.Count > 0 ? "Take one thing, or two." : "Carrying nothing to work with.";
        // As tall as the pack needs, up to where it starts scrolling instead.
        _pack.CustomMinimumSize = new Vector2(0, Mathf.Min(_entries.GetCombinedMinimumSize().Y, MaxPackHeight));
    }

    // What the panel is currently able to offer, so the button says what pressing it would do.
    internal void Offer(ActionOffer? offer, string? refusal, IReadOnlyList<string> words)
    {
        _words.Text = words.Count > 0 ? $"It is {string.Join(", ", words)}." : string.Empty;
        _words.Visible = words.Count > 0;

        _try.Disabled = offer is not { IsAvailable: true };
        _try.Text = _picked.Count == 0 ? "Try it" : $"Try it ({_picked.Count})";

        if (refusal is { Length: > 0 })
        {
            _outcome.Text = refusal;
            _outcome.Visible = true;
        }
    }

    // What came of the last attempt, in the player's own words rather than a number (section 9).
    internal void ReportOutcome(string sentence)
    {
        _outcome.Text = sentence;
        _outcome.Visible = sentence.Length > 0;
    }

    // The band has just made something there is no word for. Asked here and now, because this
    // is the moment of discovery rather than an interruption of it.
    internal void AskForAName()
    {
        _naming.Visible = true;
        _name.Text = string.Empty;
        _name.GrabFocus();
    }

    // What the player called it. Main is what writes it down; this panel knows a word was
    // typed, not what having a word for a thing means.
    internal event Action<string>? Named;

    private void Christen()
    {
        var word = _name.Text.Trim();
        if (word.Length == 0)
        {
            return;
        }

        _naming.Visible = false;
        Named?.Invoke(word);
    }

    internal IReadOnlyList<WorkshopEntry> Picked => _picked;

    private void OnTryPressed() => Attempted?.Invoke();

    // Raised for Main to ask the world what the current pick would do and to carry it out; the
    // panel itself holds no world.
    internal event Action? Attempted;

    // One square of bench per thing: its picture, the count in the corner where there is more
    // than one of it, and its name under the cursor. Not a line of text - a pack is things, and
    // picking two of them to try together is looking at what you have rather than reading it.
    private PickTile NewTile()
    {
        var button = new Button
        {
            ToggleMode = true,
            CustomMinimumSize = new Vector2(TileSize, TileSize),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };

        // A picked thing is outlined as well as shaded: two of these decide what is being tried,
        // and which two has to be readable at a glance across the bench.
        button.AddThemeStyleboxOverride("pressed", PickedBox());
        _entries.AddChild(button);

        // A Button draws its own box before its children, which is what lays the picture on the
        // tile rather than behind it.
        var undrawn = new ColorRect { Color = Undrawn, MouseFilter = MouseFilterEnum.Ignore };
        undrawn.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.KeepSize, IconMargin * 2);
        button.AddChild(undrawn);

        var icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.KeepSize, IconMargin);
        button.AddChild(icon);

        var count = InscriptionFont.BodyBoldLabel(string.Empty, CountFontSize, Ink);
        count.HorizontalAlignment = HorizontalAlignment.Right;
        count.VerticalAlignment = VerticalAlignment.Bottom;
        count.MouseFilter = MouseFilterEnum.Ignore;
        count.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.KeepSize, IconMargin / 2);
        button.AddChild(count);

        var tile = new PickTile(button, icon, undrawn, count);
        button.Pressed += () =>
        {
            if (tile.Entry is { } entry)
            {
                TogglePick(entry);
            }
        };

        return tile;
    }

    // The same shading every pressed thing on paper takes, with a line of ink round it.
    private static StyleBoxFlat PickedBox()
    {
        var box = PanelChrome.Filled(new Color(InscriptionFont.DarkInk, 0.18f));
        box.BorderColor = InscriptionFont.DarkInk;
        box.BorderWidthLeft = 1;
        box.BorderWidthRight = 1;
        box.BorderWidthTop = 1;
        box.BorderWidthBottom = 1;
        return box;
    }

    // The picture drawn for a thing, or none where nothing has been drawn for it yet - the tile
    // then carries the blank tint instead (see ItemIcons).
    private static Texture2D? IconFor(WorkshopEntry entry)
    {
        foreach (var path in ItemIcons.For(entry.Target))
        {
            if (ResourceLoader.Exists(path))
            {
                return ResourceLoader.Load<Texture2D>(path);
            }
        }

        return null;
    }

    // Two is all a binding holds, so a third pick pushes the oldest out rather than refusing the
    // click - the player is changing their mind, not making a mistake.
    private void TogglePick(WorkshopEntry entry)
    {
        if (!_picked.Remove(entry))
        {
            _picked.Add(entry);
            if (_picked.Count > 2)
            {
                _picked.RemoveAt(0);
            }
        }

        _outcome.Visible = false;
        Show(_carried);
        PickChanged?.Invoke();
    }

    internal event Action? PickChanged;

    private sealed class PickTile(Button button, TextureRect icon, ColorRect undrawn, Label count)
    {
        public WorkshopEntry? Entry { get; private set; }

        public void Apply(WorkshopEntry? entry, bool picked)
        {
            Entry = entry;
            button.Visible = entry is not null;
            if (entry is not { } shown)
            {
                return;
            }

            // The name is what the cursor asks for. On the tile it would be a caption under a
            // picture of the same thing, said twice.
            button.TooltipText = shown.Label;

            var texture = IconFor(shown);
            icon.Texture = texture;
            icon.Visible = texture is not null;
            undrawn.Visible = texture is null;

            // One of a thing is what a picture of it already says.
            count.Text = shown.Count > 1 ? $"×{shown.Count}" : string.Empty;
            count.Visible = shown.Count > 1;

            button.SetPressedNoSignal(picked);
        }
    }
}
