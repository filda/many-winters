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
    private const float Width = 320f;
    private const int BodyFontSize = 15;
    private const int SectionSpacing = 10;
    private const int ScrollbarWidth = 16;

    private static readonly Color Ink = InscriptionFont.DarkInk;
    private static readonly Color QuietInk = InscriptionFont.FadedDarkInk;

    private readonly List<PickRow> _rows = [];
    private readonly List<WorkshopEntry> _picked = [];

    private VBoxContainer _entries = null!;
    private Label _hint = null!;
    private Label _words = null!;
    private Button _try = null!;
    private Label _outcome = null!;
    private VBoxContainer _naming = null!;
    private LineEdit _name = null!;
    private IReadOnlyList<WorkshopEntry> _carried = [];

    public WorkshopPanel()
        : base("Workshop", onPaper: true)
    {
        CustomMinimumSize = new Vector2(Width, 0);
        Visible = false;
        Theme = PanelChrome.PaperButtons(BodyFontSize);
    }

    public override void _Ready()
    {
        base._Ready();
        Body.AddThemeConstantOverride("separation", SectionSpacing);

        _hint = InscriptionFont.BodyLabel("Take one thing, or two.", BodyFontSize, QuietInk);
        Body.AddChild(_hint);

        _entries = new VBoxContainer { CustomMinimumSize = new Vector2(Width - (PanelChrome.PaperPadding * 2) - ScrollbarWidth, 0) };
        Body.AddChild(_entries);

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
    internal void Open(IReadOnlyList<WorkshopEntry> carried)
    {
        _picked.Clear();
        _outcome.Visible = false;
        _naming.Visible = false;
        Visible = true;
        Show(carried);
    }

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

        while (_rows.Count < carried.Count)
        {
            _rows.Add(NewRow());
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Apply(i < carried.Count ? carried[i] : null, i < carried.Count && _picked.Contains(carried[i]));
        }

        _hint.Text = carried.Count > 0 ? "Take one thing, or two." : "Carrying nothing to work with.";
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

    private PickRow NewRow()
    {
        var button = new Button { Text = string.Empty, Alignment = HorizontalAlignment.Left, ToggleMode = true };
        _entries.AddChild(button);

        var row = new PickRow(button);
        button.Pressed += () =>
        {
            if (row.Entry is { } entry)
            {
                TogglePick(entry);
            }
        };

        return row;
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

    private sealed class PickRow(Button button)
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

            button.Text = shown.Label;
            button.SetPressedNoSignal(picked);
        }
    }
}
