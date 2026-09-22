using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// The bars a MeterReading draws into, shared between the summary card and the full detail page
// so the two keep exactly the same look without either copying the other's plumbing. Updated in
// place, never freed and rebuilt: both refresh on every tick, and tearing the bars down and
// putting them back is what made the card blink.
internal sealed class MeterRows(VBoxContainer host, int barHeight, int fontSize)
{
    private readonly List<Row> _rows = [];

    internal void Sync(IReadOnlyList<MeterReading> readings)
    {
        while (_rows.Count < readings.Count)
        {
            _rows.Add(NewRow());
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            if (i < readings.Count)
            {
                _rows[i].Show(readings[i]);
            }
            else
            {
                _rows[i].Hide();
            }
        }
    }

    private Row NewRow()
    {
        var container = new VBoxContainer();
        host.AddChild(container);

        var caption = InscriptionFont.BodyLabel(string.Empty, fontSize, InscriptionFont.DarkInk);
        container.AddChild(caption);

        var bar = PanelChrome.MeterBar(barHeight);
        container.AddChild(bar);

        return new Row(container, caption, bar);
    }

    // One measure's caption and bar, kept between refreshes and given new numbers.
    private sealed class Row(VBoxContainer container, Label caption, ProgressBar bar)
    {
        public void Show(MeterReading reading)
        {
            container.Visible = true;

            caption.Text = reading.Label;
            bar.Value = reading.Fraction;
            bar.AddThemeStyleboxOverride("fill", PanelChrome.Filled(reading.Fill));
        }

        public void Hide() => container.Visible = false;
    }
}
