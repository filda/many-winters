using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// A column of actions the player may press: a button per offer, with a quieter line under it
// saying why it is greyed out - or, for the one refusal that is not the player's to fix, that
// walking over is part of the order.
//
// Shared rather than copied because the two lists that draw actions - the selected person's card
// and the contextual menu for whatever they are pointed at - have to look and behave identically
// ("same control, same shape", docs/conventions.md), and one control is easier to keep that way
// than two in step.
//
// It holds no opinions of its own: what an action is called, and whether and why it can run
// arrive as ActionOffer. This class draws them and reports which one was pressed.
internal partial class ActionList : VBoxContainer
{
    private const int ReasonFontSize = 13;

    private readonly List<ActionRow> _rows = [];

    // The owner runs the pressed action; the list only knows what an offer is, not what
    // executing one means for the rest of the game.
    internal event Action<ActionOffer>? ActionInvoked;

    // Buttons are kept and updated in place, not thrown away and rebuilt: this refreshes every
    // tick, and a button freed between press and release swallows the click. The list only grows.
    internal void Show(IReadOnlyList<ActionOffer> offers)
    {
        while (_rows.Count < offers.Count)
        {
            _rows.Add(NewRow());
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Apply(i < offers.Count ? offers[i] : null);
        }
    }

    private ActionRow NewRow()
    {
        var container = new VBoxContainer();
        AddChild(container);

        // Left-aligned so a column of them reads as a list of choices rather than a stack of
        // centred captions.
        var button = new Button { Text = string.Empty, Alignment = HorizontalAlignment.Left };
        container.AddChild(button);

        var reason = InscriptionFont.BodyLabel(string.Empty, ReasonFontSize, InscriptionFont.FadedDarkInk);
        reason.Visible = false;
        container.AddChild(reason);

        var row = new ActionRow(container, button, reason);
        button.Pressed += () =>
        {
            if (row.Offer is { } offer)
            {
                ActionInvoked?.Invoke(offer);
            }
        };

        return row;
    }

    // Holds the offer currently shown, so a press reports the offer the player actually saw.
    private sealed class ActionRow(VBoxContainer container, Button button, Label reason)
    {
        public ActionOffer? Offer { get; private set; }

        public void Apply(ActionOffer? offer)
        {
            Offer = offer;
            container.Visible = offer is not null;
            if (offer is not { } shown)
            {
                return;
            }

            button.Text = shown.Label;
            button.Disabled = !shown.IsAvailable;

            var text = ActionBlockerText.For(shown);
            reason.Text = text;
            reason.Visible = text.Length > 0;
        }
    }
}
