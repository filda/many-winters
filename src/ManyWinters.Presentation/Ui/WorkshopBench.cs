using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Ui;

// The workbench: everything one person is carrying, and the one question the player may ask of
// it - take one thing or two, and see what comes of putting them together
// (docs/materials-and-crafting-architecture.md section 7).
//
// Deliberately not a list of verbs. There is one button and it says "Make": the player forms the
// hypothesis and the simulation rules on it, which is the loop worth playing. A menu of
// Twist/Bind/Knap would hand them the answer before they had the idea.
//
// Not a page of its own but the right half of the person's page, so it has no frame or cross of
// its own: one column for the page to place, headed by its own "Workshop" title with Eat, Drop and
// Make and the one line about what just happened, and under that the pack and the recipes. Its
// head is its own, as the person's half has theirs, so neither half's heading decides where the
// other's content starts.
//
// A fixed shape rather than a page that grows and shrinks with what is laid on it, so the bench
// doesn't lurch every time something comes off it or is put down. Above all the tiles stay where
// they are: nothing over them ever appears or disappears, it only gains or loses its text.
public sealed class WorkshopBench
{
    private const int BodyFontSize = 15;
    private const int TitleFontSize = 22;
    private const int SectionSpacing = 6;

    // The pack sits to the left of the recipe list rather than spanning the whole bench, which is
    // what leaves it fewer columns than it once had. The two together decide how wide the bench
    // is: it takes no more of the page than its tiles and its recipes fill, so no empty stretch
    // of paper opens between them.
    private const int Columns = 4;

    // How wide the recipe list's column is, the rest of the bench going to the pack.
    private const float RecipeColumnWidth = 220f;

    // How far either half of the bench may reach before it scrolls within its column instead of
    // growing the column - which is what keeps the whole bench a fixed shape.
    private const float MaxPackHeight = 128f;

    // The title row is as tall as its tallest occupant whether or not the buttons are showing, so
    // a button appearing under the cursor never pushes the lines under it down.
    private const float TitleRowHeight = 34f;

    // The status line's height, whatever it holds or does not, so nothing between the title and
    // the tiles ever changes height.
    private const float LineHeight = 22f;

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

    private readonly GridContainer _entries;
    private readonly ScrollContainer _pack;
    private readonly Button _try;
    private readonly Button _eat;
    private readonly Button _drop;
    private readonly Label _status;
    private readonly ActionList _recipes;
    private IReadOnlyList<WorkshopEntry> _carried = [];

    // What the bench last said about an attempt, a refusal or a naming - empty once there is
    // nothing to say about the current pick.
    private string _said = string.Empty;

    // What the one thing in hand is like, when anything is known of it.
    private string _description = string.Empty;

    public WorkshopBench()
    {
        // Eat, Drop and Make sit beside the "Workshop" title rather than down in the body - they
        // read on the selection the way the icons on a toolbar do, not on the pack laid out
        // underneath.
        var titleRow = new HBoxContainer { CustomMinimumSize = new Vector2(0, TitleRowHeight) };
        titleRow.AddThemeConstantOverride("separation", SectionSpacing);

        var title = InscriptionFont.TitleLabel("Workshop", TitleFontSize, Ink);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.AutowrapMode = TextServer.AutowrapMode.Off;
        titleRow.AddChild(title);

        _eat = WorkshopIcons.Button("Eat", WorkshopIcons.Eat());
        _eat.Visible = false;
        _eat.Pressed += () => EatRequested?.Invoke();
        titleRow.AddChild(_eat);

        // Shown only while there is something for it to do - a button that reads "Make" while
        // greyed out is a button promising an answer it does not have.
        _try = WorkshopIcons.Button("Make", WorkshopIcons.Make());
        _try.Visible = false;
        _try.Pressed += () => Attempted?.Invoke();
        titleRow.AddChild(_try);

        // Last, at the far end from Eat: putting a thing down is the one press here that loses it.
        _drop = WorkshopIcons.Button("Drop", WorkshopIcons.Drop());
        _drop.Visible = false;
        _drop.Pressed += () => DropRequested?.Invoke();
        titleRow.AddChild(_drop);

        // One line under the title, rather than buried under the pack: what the last attempt (or
        // naming) said, else what the thing in hand is like, else, with nothing picked, how to
        // begin. Italic for the same
        // reason a diary entry is: this is the bench speaking, not a label.
        _status = InscriptionFont.BodyItalicLabel(string.Empty, BodyFontSize, Ink);
        _status.AutowrapMode = TextServer.AutowrapMode.Off;
        _status.ClipText = true;
        _status.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _status.CustomMinimumSize = new Vector2(0, LineHeight);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", SectionSpacing);
        Root = root;

        var head = new VBoxContainer();
        head.AddThemeConstantOverride("separation", 0);
        head.AddChild(titleRow);
        head.AddChild(_status);
        root.AddChild(head);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", SectionSpacing);
        root.AddChild(body);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", SectionSpacing * 2);
        body.AddChild(columns);

        // The pack keeps its own scroll, so a big haul stays inside the bench rather than growing
        // it - which is what a fixed-size workbench needs, the window's scroll being for a page
        // that is allowed to be as tall as what is written on it.
        var pack = new VBoxContainer();
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
        // same shape") - only ever holding offers the person can carry out, since a recipe with
        // nothing to explain a grey button is not worth a line.
        _recipes = new ActionList();
        _recipes.ActionInvoked += offer => RecipeInvoked?.Invoke(offer);
        recipeScroll.AddChild(_recipes);
    }

    // Pressed on a recipe line. The owner runs it, the same way it runs a line off the person's
    // card - the bench knows what an offer is, not what making one means for the rest of the
    // game.
    internal event Action<ActionOffer>? RecipeInvoked;

    // Raised for the owner to ask the world what the current pick would do and to carry it out;
    // the bench holds no world.
    internal event Action? Attempted;

    // Pressed Eat or Drop on whatever is picked. The owner carries it out the same way it does an
    // attempt or a recipe - the bench only says which button was pressed.
    internal event Action? EatRequested;
    internal event Action? DropRequested;

    internal event Action? PickChanged;

    // The whole bench, for the page to place as its right-hand column.
    internal Control Root { get; }

    internal IReadOnlyList<WorkshopEntry> Picked => _picked;

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

    // Opened fresh: nothing picked, nothing yet said about the last attempt.
    internal void Open(IReadOnlyList<WorkshopEntry> carried, IReadOnlyList<ActionOffer> recipes)
    {
        _picked.Clear();
        _said = string.Empty;
        Show(carried);
        ShowRecipes(recipes);
    }

    // Redrawn whenever the pack does - the owner asks for both together after anything that
    // could have changed what is carried.
    internal void ShowRecipes(IReadOnlyList<ActionOffer> recipes) => _recipes.Show(recipes);

    // Redrawn after every attempt, because the pack has changed underneath it. What is still held
    // stays picked, even a stack the attempt took some of, so trying again is one press; a pick
    // that is no longer in the pack - the grass that just became cord - quietly stops being picked.
    internal void Show(IReadOnlyList<WorkshopEntry> carried)
    {
        _carried = carried;
        var stillPicked = WorkshopActions.StillPicked(_picked, carried);
        _picked.Clear();
        _picked.AddRange(stillPicked);

        while (_tiles.Count < carried.Count)
        {
            _tiles.Add(NewTile());
        }

        for (var i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].Apply(i < carried.Count ? carried[i] : null, i < carried.Count && _picked.Contains(carried[i]));
        }

        SyncStatus();
        // As tall as the pack needs, up to where it starts scrolling instead.
        _pack.CustomMinimumSize = new Vector2(0, Mathf.Min(_entries.GetCombinedMinimumSize().Y, MaxPackHeight));
    }

    // What the bench is currently able to offer, so the button says what pressing it would do.
    //
    // The refusal fully decides the status line rather than only filling it in when there is one
    // to show: a pick that is undone (or acted on some other way, like Eat or Drop) leaves no
    // refusal behind, and the line has to be told to go quiet rather than being left holding
    // whatever it last said.
    internal void Offer(ActionOffer? offer, string? refusal, IReadOnlyList<string> words)
    {
        // What the thing in hand is like, never what it is for.
        _description = words.Count > 0 ? $"It is {string.Join(", ", words)}." : string.Empty;

        _try.Visible = offer is { IsAvailable: true };
        _try.Text = _picked.Count > 1 ? $"Make ({_picked.Count})" : "Make";

        _said = refusal ?? string.Empty;
        SyncStatus();
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
        _said = sentence;
        SyncStatus();
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

    // The status line: what was last said wins; with nothing said, what the thing in hand is like,
    // or how to begin while nothing is in hand. Cut short rather than wrapped
    // when it runs long, and silent as empty text rather than a hidden line, because a line that
    // wrapped or vanished would move the tiles under it.
    private void SyncStatus()
    {
        var hint = _carried.Count == 0 ? "Carrying nothing to work with."
            : _picked.Count == 0 ? "Take one thing, or two."
            : _description;
        var saying = _said.Length > 0;
        _status.Text = saying ? _said : hint;
        _status.AddThemeColorOverride("font_color", saying ? Ink : QuietInk);
    }

    // One square of bench per thing: its picture, the count in the corner where there is more
    // than one of it, and its name under the cursor. Not a line of text - a pack is things, and
    // picking two of them to try together is looking at what you have rather than reading it.
    private PickTile NewTile()
    {
        var button = new Button
        {
            ToggleMode = true,
            CustomMinimumSize = new Vector2(TileSize, TileSize),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };

        // A picked thing is outlined as well as shaded: two of these decide what is being tried,
        // and which two has to be readable at a glance across the bench.
        button.AddThemeStyleboxOverride("pressed", PickedBox());
        _entries.AddChild(button);

        // A Button draws its own box before its children, which is what lays the picture on the
        // tile rather than behind it.
        var undrawn = new ColorRect { Color = Undrawn, MouseFilter = Control.MouseFilterEnum.Ignore };
        undrawn.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, IconMargin * 2);
        button.AddChild(undrawn);

        var icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, IconMargin);
        button.AddChild(icon);

        var count = InscriptionFont.BodyBoldLabel(string.Empty, CountFontSize, Ink);
        count.HorizontalAlignment = HorizontalAlignment.Right;
        count.VerticalAlignment = VerticalAlignment.Bottom;
        count.MouseFilter = Control.MouseFilterEnum.Ignore;
        count.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, IconMargin / 2);
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
