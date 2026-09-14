using Godot;
using ManyWinters.Core.Population;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// The band as a list: whose band it is, how many there are, then two short lines each - name,
// age and what they are doing on one, their belly under it. Pressing a name takes the camera to
// that person and selects them, which is the whole point of the window: a name in a list is no
// help while the player still has to find the person in the forest (docs/todo/todo.md).
//
// Kept as short vertically as it can be: a band of ten has to fit on the screen without the
// window growing a scrollbar, so the age is a bare number, the action shares the name's line and
// the spacing is tight.
//
// A window rather than a dock, opened to go looking for somebody and closed again; the selection
// panel is the one that stays open all game. Mirrored across the screen from it - same width, same
// inset, other edge - so the two read as a pair: the band on the left, whoever is selected out of
// it on the right.
//
// It holds no opinions of its own - the title, the count, the headings and the hunger readings all
// arrive as a BandRoster. This class draws them and reports which line was pressed.
internal partial class BandPanel : FloatingPanel
{
    // The same page on the other side of the screen, so both numbers come from the panel it
    // mirrors rather than being kept in step by hand. Main reads the inset to place the window
    // (see Main.ToggleBandPanel).
    private const float Width = SelectionPanel.Width;
    internal const float Margin = SelectionPanel.Margin;

    // Room for the scrollbar FloatingPanel grows once the band outgrows the screen, as in
    // ChroniclePanel: without it a wrapped label's minimum width is one character.
    private const int ScrollbarWidth = 16;
    private const float TextWidth = Width - (PanelChrome.PaperPadding * 2) - ScrollbarWidth;

    private const int SummaryFontSize = 14;
    private const int HeadingFontSize = 16;
    private const int TaskFontSize = 13;
    private const int MeterHeight = 5;

    // The whole line is the button, and its two labels ride on the button's rect rather than being
    // laid out by it (a Button is no container), so they cannot push it taller - it is told how
    // tall a line of the heading face is.
    private const int LineHeight = HeadingFontSize + 8;

    // Between a person's name and their belly, against PersonSpacing between one person and the
    // next: without the difference a column of names and bars reads as one list of twice as many
    // things.
    private const int RowSpacing = 2;
    private const int PersonSpacing = 8;

    private Label _summary = null!;
    private VBoxContainer _people = null!;
    private readonly List<PersonRow> _rows = [];

    // Who the player pressed. Main decides what that means - the panel knows it named a person,
    // not that naming one moves a camera.
    internal event Action<Person>? PersonChosen;

    public BandPanel()
        : base("Band", onPaper: true)
    {
        CustomMinimumSize = new Vector2(Width, 0);
        Visible = false;
        Theme = PanelChrome.PaperButtons(HeadingFontSize);
    }

    public override void _Ready()
    {
        base._Ready();
        Body.AddThemeConstantOverride("separation", PersonSpacing);

        _summary = InscriptionFont.BodyLabel(string.Empty, SummaryFontSize, InscriptionFont.FadedDarkInk);
        _summary.CustomMinimumSize = new Vector2(TextWidth, 0);
        _summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Body.AddChild(_summary);

        _people = new VBoxContainer();
        _people.AddThemeConstantOverride("separation", PersonSpacing);
        Body.AddChild(_people);
    }

    internal void Update(BandRoster roster)
    {
        SetTitle(roster.Title);
        _summary.Text = roster.Summary;
        SyncRows(roster.People);
    }

    // Rows are kept and updated in place, not thrown away and rebuilt, for the same reason the
    // selection panel's actions are: this refreshes on every tick, and a button freed between the
    // press and the release swallows the click. The list only grows - a band that has lost
    // somebody leaves the spare row hidden, ready for the next child.
    private void SyncRows(IReadOnlyList<RosterEntry> entries)
    {
        while (_rows.Count < entries.Count)
        {
            _rows.Add(NewRow());
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Apply(i < entries.Count ? entries[i] : null);
        }
    }

    private PersonRow NewRow()
    {
        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", RowSpacing);
        _people.AddChild(container);

        // The whole line is one button - the player aims at a person, not at their name - so both
        // texts are labels laid over it, and neither takes the mouse.
        var line = new Button { Text = string.Empty, CustomMinimumSize = new Vector2(0, LineHeight) };
        container.AddChild(line);

        // Inside the button's own padding, so the name starts where a button's caption would.
        var texts = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        texts.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        texts.AddThemeConstantOverride("margin_left", PanelChrome.FilledPadding);
        texts.AddThemeConstantOverride("margin_right", PanelChrome.FilledPadding);
        line.AddChild(texts);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        texts.AddChild(row);

        // In the button face, because it was the button's caption until the whole row became the
        // button.
        var heading = InscriptionFont.BodyBoldLabel(string.Empty, HeadingFontSize, InscriptionFont.DarkInk);
        heading.AutowrapMode = TextServer.AutowrapMode.Off;
        heading.VerticalAlignment = VerticalAlignment.Center;
        heading.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(heading);

        // Against the right edge, and it takes what is left of the line: a column of actions ending
        // flush is one column, not a ragged tail behind names of every length. A long one
        // ("Keeping up with Somebody") gives up its own tail to an ellipsis rather than wrapping or
        // widening the window. Wrapping is turned back off because InscriptionFont hands out
        // wrapping labels (Styled) - right for a paragraph, and here it would lay the text out
        // below the single line this row is tall, where it is simply not drawn. ClipText then keeps
        // the text out of the label's minimum width, or that is what would widen the window.
        var task = InscriptionFont.BodyLabel(string.Empty, TaskFontSize, InscriptionFont.FadedDarkInk);
        task.AutowrapMode = TextServer.AutowrapMode.Off;
        task.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        task.HorizontalAlignment = HorizontalAlignment.Right;
        task.VerticalAlignment = VerticalAlignment.Center;
        task.ClipText = true;
        task.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        task.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(task);

        // Thinner than the card's meters and with no caption: the player is scanning a column of
        // bellies for the empty one, not reading a measurement.
        var fed = PanelChrome.MeterBar(MeterHeight);
        container.AddChild(fed);

        var person = new PersonRow(container, heading, task, fed);
        line.Pressed += () =>
        {
            if (person.Entry is { } entry)
            {
                PersonChosen?.Invoke(entry.Person);
            }
        };

        return person;
    }

    // One person: their name and age, what they are doing against the other edge, and their belly
    // under both. Holds the entry it is currently showing, so the press reports the person the
    // player saw.
    private sealed class PersonRow(VBoxContainer container, Label heading, Label task, ProgressBar fed)
    {
        public RosterEntry? Entry { get; private set; }

        public void Apply(RosterEntry? entry)
        {
            Entry = entry;
            container.Visible = entry is not null;
            if (entry is not { } shown)
            {
                return;
            }

            heading.Text = shown.Heading;
            task.Text = shown.Task;
            fed.Value = shown.Fed.Fraction;
            fed.AddThemeStyleboxOverride("fill", PanelChrome.Filled(shown.Fed.Fill));
        }
    }
}
