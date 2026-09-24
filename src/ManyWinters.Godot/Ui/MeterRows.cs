using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// The bars a MeterReading draws into, shared between the summary card and the full detail page
// so the two keep exactly the same look without either copying the other's plumbing. Updated in
// place, never freed and rebuilt: both refresh on every tick, and tearing the bars down and
// putting them back is what made the card blink.
//
// Each measure is one line, its caption with the bar beside it. A grid rather than a row per
// line, so every bar starts where the widest caption ends and the bars stand in one column.
internal sealed class MeterRows
{
    // Between one line and the next: tight enough to read as one block of measures.
    private const int RowSpacing = 6;

    // Between a caption and its bar - a word's worth, not a column gap.
    private const int CaptionSpacing = 10;

    private readonly GridContainer _grid;
    private readonly int _barHeight;
    private readonly int _fontSize;
    private readonly List<Row> _rows = [];

    internal MeterRows(VBoxContainer host, int barHeight, int fontSize)
    {
        _barHeight = barHeight;
        _fontSize = fontSize;
        _grid = new GridContainer { Columns = 2 };
        _grid.AddThemeConstantOverride("v_separation", RowSpacing);
        _grid.AddThemeConstantOverride("h_separation", CaptionSpacing);
        host.AddChild(_grid);
    }

    internal void Sync(IReadOnlyList<MeterReading> readings)
    {
        while (_rows.Count < readings.Count)
        {
            _rows.Add(NewRow());
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Apply(i < readings.Count ? readings[i] : null);
        }
    }

    private Row NewRow()
    {
        var caption = InscriptionFont.BodyLabel(string.Empty, _fontSize, InscriptionFont.DarkInk);
        caption.AutowrapMode = TextServer.AutowrapMode.Off;
        _grid.AddChild(caption);

        var bar = PanelChrome.MeterBar(_barHeight);
        bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        bar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _grid.AddChild(bar);

        return new Row(caption, bar);
    }

    // One measure's caption and bar, kept between refreshes and given new numbers. Both cells
    // hide together, so the grid skips the whole line rather than shifting the next one's caption
    // into this one's bar column.
    private sealed class Row(Label caption, ProgressBar bar)
    {
        public void Apply(MeterReading? reading)
        {
            caption.Visible = reading is not null;
            bar.Visible = reading is not null;
            if (reading is not { } shown)
            {
                return;
            }

            caption.Text = shown.Label;
            bar.Value = shown.Fraction;
            bar.AddThemeStyleboxOverride("fill", PanelChrome.Filled(shown.Fill));
        }
    }
}
