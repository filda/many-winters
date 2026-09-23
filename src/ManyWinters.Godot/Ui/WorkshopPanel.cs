using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// The workbench: everything one person is carrying, and the one question the player may ask of
// it - take one thing or two, and see what comes of putting them together
// (docs/materials-and-crafting-architecture.md section 7).
//
// Deliberately not a list of verbs. There is one button and it says "Make": the player forms the
// hypothesis and the simulation rules on it, which is the loop worth playing. A menu of
// Twist/Bind/Knap would hand them the answer before they had the idea.
//
// A fixed shape rather than a page that grows and shrinks with what is currently laid on it - the
// naming question that used to live inside it, since moved to its own page, was what made it
// lurch every time a thing nobody had a word for came off the bench.
//
// Time stands still while this is open, the way it does for the pause page - tinkering is meant
// to be unhurried, not something to rush before the world moves on. Main holds the clock for
// whichever of those is visible.
public partial class WorkshopPanel : PaperPanel
{
    // Wider than the cards that sit beside the world: this one is the workbench itself, in the
    // middle of the screen, and what a thing is made of runs long enough that a narrow column
    // broke half the lines. Wide and low rather than tall - a bench is a surface
    // things are laid out on, and a column of carried things reaching down the screen reads as an
    // inventory screen.
    private const float Width = 640f;
    private const float BodyHeight = 280f;
    private const int BodyFontSize = 15;
    private const int SectionSpacing = 6;

    // The pack sits to the left of the recipe list rather than spanning the whole bench, which is
    // what leaves it fewer columns than it once had.
    private const int Columns = 5;

    // How wide the recipe list's own column is, the rest of the bench going to the pack.
    private const float RecipeColumnWidth = 220f;

    // How far either half of the bench may reach before it scrolls within its own column instead
    // of growing the column - which is what keeps the whole bench a fixed shape.
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
    // an item with no icon.
    private static readonly Color Undrawn = new(0.55f, 0.45f, 0.3f, 0.55f);

    private static readonly Color Ink = InscriptionFont.DarkInk;
    private static readonly Color QuietInk = InscriptionFont.FadedDarkInk;

    private readonly List<PickTile> _tiles = [];
    private readonly List<WorkshopEntry> _picked = [];

    private GridContainer _entries = null!;
    private ScrollContainer _pack = null!;
    private Label _hint = null!;
    private Label _words = null!;
    private Button _try = null!;
    private Button _eat = null!;
    private Button _drop = null!;
    private Label _outcome = null!;
    private ActionList _recipes = null!;
    private IReadOnlyList<WorkshopEntry> _carried = [];

    public WorkshopPanel()
        : base("Workshop", fixedBodyHeight: BodyHeight)
    {
        CustomMinimumSize = new Vector2(Width, 0);
        // The bench is what the player is doing, not a card beside the world: it holds the middle
        // of the screen, and keeps it when the window goes fullscreen and back.
        Placement = PanelPlacement.Centred;
        Visible = false;
        Theme = PanelChrome.PaperButtons(BodyFontSize);
    }

    public override void _Ready()
    {
        base._Ready();
        Body.AddThemeConstantOverride("separation", SectionSpacing);

        // Whatever the last attempt (or naming) said, right under the title rather than buried
        // under the pack - it is the one line about what just happened, and reads as part of the
        // heading rather than as one more line of body text. Italic for the same reason a diary
        // entry is: this is the bench speaking about what it just watched happen, not a label.
        _outcome = InscriptionFont.BodyItalicLabel(string.Empty, BodyFontSize, Ink);
        _outcome.Visible = false;
        Body.AddChild(_outcome);

        _hint = InscriptionFont.BodyLabel("Take one thing, or two.", BodyFontSize, QuietInk);
        Body.AddChild(_hint);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", SectionSpacing * 2);
        Body.AddChild(columns);

        // The pack keeps its own scroll, so a big haul stays inside the bench rather than growing
        // it - which is what a fixed-size workbench needs, the window's own scroll being for a
        // page that is allowed to be as tall as what is written on it.
        var pack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddChild(pack);

        _pack = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, MaxPackHeight),
        };
        pack.AddChild(_pack);

        _entries = new GridContainer
        {
            Columns = Columns,
            CustomMinimumSize = new Vector2((Columns * (TileSize + TileSpacing)) - TileSpacing, 0),
        };
        _entries.AddThemeConstantOverride("v_separation", TileSpacing);
        _entries.AddThemeConstantOverride("h_separation", TileSpacing);
        _pack.AddChild(_entries);

        // Recipes get a section of their own beside the pack rather than a place in the same
        // column - named up front, they are a different kind of choice from picking things to
        // try, and read as one when they sit under the pack they are made out of.
        var recipeColumn = new VBoxContainer { CustomMinimumSize = new Vector2(RecipeColumnWidth, 0) };
        columns.AddChild(recipeColumn);
        recipeColumn.AddChild(InscriptionFont.BodyBoldLabel("Recipes", BodyFontSize, Ink));

        var recipeScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, MaxPackHeight),
        };
        recipeColumn.AddChild(recipeScroll);

        // The same control the selected person's card uses, for the same reason ("same control,
        // same shape") - only ever holding offers the person can actually carry out, since a
        // recipe with nothing to explain a grey button is not worth a line.
        _recipes = new ActionList();
        _recipes.ActionInvoked += offer => RecipeInvoked?.Invoke(offer);
        recipeScroll.AddChild(_recipes);

        // What the thing in hand is like, never what it is for.
        _words = InscriptionFont.BodyItalicLabel(string.Empty, BodyFontSize, QuietInk);
        _words.Visible = false;
        Body.AddChild(_words);
    }

    // Eat, Drop and the one verb the current pick can answer, set beside the "Workshop" title
    // rather than down in the body - they read on the selection the way the icons on a toolbar
    // do, not on the pack laid out underneath. Built while the title bar itself is still going
    // up, so their Pressed handlers are wired here too rather than back in _Ready.
    protected override void BuildTitleBarExtras(HBoxContainer titleBar)
    {
        _eat = WorkshopIcons.Button("Eat", WorkshopIcons.Eat());
        _eat.Visible = false;
        _eat.Pressed += () => EatRequested?.Invoke();
        titleBar.AddChild(_eat);

        _drop = WorkshopIcons.Button("Drop", WorkshopIcons.Drop());
        _drop.Visible = false;
        _drop.Pressed += () => DropRequested?.Invoke();
        titleBar.AddChild(_drop);

        // Shown only while there is something for it to do - a button that reads "Make" while
        // greyed out is a button promising an answer it does not have.
        _try = WorkshopIcons.Button("Make", WorkshopIcons.Make());
        _try.Visible = false;
        _try.Pressed += OnTryPressed;
        titleBar.AddChild(_try);
    }

    // Opened fresh: nothing picked, nothing yet said about the last attempt.
    internal void Open(IReadOnlyList<WorkshopEntry> carried, IReadOnlyList<ActionOffer> recipes)
    {
        _picked.Clear();
        _outcome.Visible = false;
        Visible = true;
        Show(carried);
        ShowRecipes(recipes);
    }

    // Redrawn whenever the pack does - the owner asks for both together after anything that
    // could have changed what is carried.
    internal void ShowRecipes(IReadOnlyList<ActionOffer> recipes) => _recipes.Show(recipes);

    // Pressed on a recipe line. The owner runs it, the same way it runs a line off the person's
    // own card - this panel knows what an offer is, not what making one means for the rest of
    // the game.
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

    // Put away, so the world can start moving again.
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

        // Said only once something has actually been picked - stated up front, before the player
        // has touched the pack, it is an instruction nobody asked for yet.
        _hint.Text = carried.Count > 0 ? "Take one thing, or two." : "Carrying nothing to work with.";
        _hint.Visible = carried.Count == 0 || _picked.Count > 0;
        // As tall as the pack needs, up to where it starts scrolling instead.
        _pack.CustomMinimumSize = new Vector2(0, Mathf.Min(_entries.GetCombinedMinimumSize().Y, MaxPackHeight));
    }

    // What the panel is currently able to offer, so the button says what pressing it would do.
    //
    // The refusal fully decides the status line rather than only filling it in when there is one
    // to show: a pick that is undone (or acted on some other way, like Eat or Drop) leaves no
    // refusal behind, and the line has to be told to go quiet rather than being left holding
    // whatever it last said.
    internal void Offer(ActionOffer? offer, string? refusal, IReadOnlyList<string> words)
    {
        _words.Text = words.Count > 0 ? $"It is {string.Join(", ", words)}." : string.Empty;
        _words.Visible = words.Count > 0;

        _try.Visible = offer is { IsAvailable: true };
        _try.Text = _picked.Count > 1 ? $"Make ({_picked.Count})" : "Make";

        _outcome.Text = refusal ?? string.Empty;
        _outcome.Visible = refusal is { Length: > 0 };
    }

    // Eat and Drop, for whatever is picked right now - each hidden rather than disabled when
    // there is nothing for it to do.
    internal void OfferItemActions(ActionOffer? eat, ActionOffer? drop)
    {
        _eat.Visible = eat is not null;
        _eat.Disabled = eat is not { IsAvailable: true };

        _drop.Visible = drop is not null;
        _drop.Disabled = drop is not { IsAvailable: true };
    }

    // What came of the last attempt, in the player's own words rather than a number (section 9).
    internal void ReportOutcome(string sentence)
    {
        _outcome.Text = sentence;
        _outcome.Visible = sentence.Length > 0;
    }

    internal IReadOnlyList<WorkshopEntry> Picked => _picked;

    private void OnTryPressed() => Attempted?.Invoke();

    // Raised for Main to ask the world what the current pick would do and to carry it out; the
    // panel itself holds no world.
    internal event Action? Attempted;

    // Pressed Eat or Drop on whatever is picked. Main carries it out the same way it does an
    // attempt or a recipe - this panel only says which button was pressed.
    internal event Action? EatRequested;
    internal event Action? DropRequested;

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
    // then carries the blank tint instead. Internal rather than private: the naming panel wants
    // the same picture, larger, for the thing it is asking a name for.
    internal static Texture2D? IconFor(CarriedThing thing)
    {
        foreach (var path in ItemIcons.For(thing))
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

            var texture = IconFor(shown.Target);
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
