using Godot;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Interaction;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Views;

namespace ManyWinters.Godot.Ui;

// Who the player has picked out of the world - a person or a grave, never both - and everything
// on screen that shows it: the card, the full page behind it, the band's roster, and the marker
// over the selected person's head. A caller only ever asks for a selection transition; this is
// the one place the mutual exclusion between a person and a grave is kept.
internal sealed class SelectionController
{
    private readonly WorldState _world;
    private readonly WorldPresenter _presenter;
    private readonly FreeCameraRig _cameraRig;
    private readonly PresentationSettings _presentation;
    private readonly TextureRect _marker;
    private readonly SelectionPanel _selectionPanel;
    private readonly BandPanel _bandPanel;
    private readonly PersonDetailPanel _detailPanel;

    private Person? _person;
    private Grave? _grave;

    // Forwarded from the selection card, the detail page, or (in composition code) the contextual
    // menu - all three draw offers for whoever is selected, so this is the one signal a caller
    // needs to carry an offer out.
    public event Action<ActionOffer>? ActionInvoked;

    // The pack line, pressed on the card or on the detail page - who it was pressed for, since a
    // workshop is opened for somebody rather than for whoever happens to be selected when it
    // finally opens.
    public event Action<Person>? WorkshopRequested;

    // Raised after every selection change, so the still-separate debug inspector can redraw from
    // Person or Grave without this controller knowing that window exists.
    public event Action? Refreshed;

    // Letting the detail page go primes the tick accumulator, the same reason WorkshopController
    // raises its own Closed.
    public event Action? Closed;

    public Person? Person => _person;

    public Grave? Grave => _grave;

    public SelectionController(
        SelectionUi ui,
        WorldState world,
        WorldPresenter presenter,
        FreeCameraRig cameraRig,
        PresentationSettings presentation)
    {
        _world = world;
        _presenter = presenter;
        _cameraRig = cameraRig;
        _presentation = presentation;

        _marker = ui.Marker;

        // The player's panel, against the opposite edge from the debug inspector so both can be
        // open.
        _selectionPanel = ui.Panel;
        _selectionPanel.ActionInvoked += offer => ActionInvoked?.Invoke(offer);
        _selectionPanel.PackRequested += OnPackRequested;
        _selectionPanel.DetailRequested += OpenDetail;
        _selectionPanel.CloseRequested += Clear;

        _bandPanel = ui.BandPanel;
        _bandPanel.PersonChosen += SelectAndFocus;

        // The full page, opened from the name on the selected person's card. Like the workbench
        // it holds the clock while it is up (see SimulationLoop.Update) and shields everything
        // under it from the click that would otherwise land on the world or another window
        // through it - reading or acting on somebody here is meant to have the player's whole
        // attention, the same as working something over is.
        _detailPanel = ui.DetailPanel;
        _detailPanel.Closed += () => Closed?.Invoke();
        _detailPanel.ActionInvoked += offer => ActionInvoked?.Invoke(offer);
        _detailPanel.PackRequested += OnPackRequested;
    }

    public void Select(Person person)
    {
        _person = person;
        _grave = null;
        Refresh();

        // A verbose session follows the game from its log alone; the card coming up for the
        // person just picked says so.
        if (LaunchOptions.Verbose)
        {
            GD.Print($"Card shown for {person.Name}.");
        }
    }

    public void Select(Grave grave)
    {
        _grave = grave;
        _person = null;
        Refresh();
    }

    // Pressing a name on the roster: select the person and take the view to them. Selecting alone
    // would leave the player looking at the same empty forest with a marker somewhere off screen.
    private void SelectAndFocus(Person person)
    {
        Select(person);

        if (_presenter.GetPersonGlobalPosition(person.Id) is { } position)
        {
            _cameraRig.FocusOn(position);
        }
    }

    // The player put their own card away with the cross in its corner: nobody is selected any
    // more, so the card comes down with the selection rather than on its own.
    private void Clear()
    {
        _person = null;
        _grave = null;
        Refresh();
    }

    // Every window that shows something about whoever is selected or was, closed together so a
    // future one is not the one somebody forgets to add here - which is exactly how the detail
    // page got left open through an ending it was never told about.
    public void CloseForBandEnd()
    {
        _bandPanel.Visible = false;
        Clear();
    }

    // Nothing else answers to Escape for the detail page - unlike the workshop and naming panel,
    // it never sits under a page of its own, so there is no priority to keep straight.
    public void CloseDetail() => _detailPanel.Close();

    // Everything on screen that is about people: the player's panel for whoever is selected, and
    // the band's roster, whose lines go stale on exactly the same occasions.
    public void Refresh()
    {
        RefreshBandPanel();

        if (_grave is { } grave)
        {
            _selectionPanel.ShowGrave(InspectorText.ForGraveRecord(grave, _world.Configuration.SkillCatalog));
            _detailPanel.Close();
        }
        else if (_person is { } person)
        {
            var card = SelectionCard.For(_world, person);
            var offers = PersonActions.For(_world, person);
            _selectionPanel.ShowPerson(card, offers);

            // Only while it is open, and on the same person it was opened for - the summary card
            // it reads from is rebuilt every refresh, and the page left open behind it should
            // read as true as the card does rather than freezing on the moment it was opened.
            if (_detailPanel.Visible)
            {
                _detailPanel.Show(card, offers);
            }
        }
        else
        {
            _selectionPanel.ClearSelection();
            _detailPanel.Close();
        }

        Refreshed?.Invoke();
    }

    // Placed on the way open rather than once at setup, so it always comes back where the player
    // expects it however far they dragged it last time: mirrored across the screen from the
    // selection panel, same inset from its own edge (see BandPanel), the band on the left and
    // whoever is picked out of it on the right.
    //
    // Filled on the way open as well as on every tick: the clock can be standing still (a pause,
    // an inscription), and an empty roster is no answer to "where is everybody".
    public void ToggleBandPanel()
    {
        _bandPanel.Visible = !_bandPanel.Visible;
        _bandPanel.Position = new Vector2(BandPanel.Margin, BandPanel.Margin);
        RefreshBandPanel();
    }

    // A 2D overlay, not a 3D billboard (see PresentationSettings.SelectionMarkerScreenSize).
    // Camera3D.UnprojectPosition/IsPositionBehind do the projection; this anchors a Control on it.
    public void UpdateMarker()
    {
        if (_person is not { } person
            || _presenter.GetPersonGlobalPosition(person.Id) is not { } personPosition
            || _presenter.GetPersonHeadHeightOffset(person.Id) is not { } headHeightOffset)
        {
            _marker.Visible = false;
            return;
        }

        var camera = _cameraRig.Camera;
        var headPosition = personPosition + new Vector3(0, headHeightOffset, 0);
        if (camera.IsPositionBehind(headPosition))
        {
            _marker.Visible = false;
            return;
        }

        // SelectionMarkerScreenGap is screen pixels, so it applies to the projected point, not to
        // headPosition before projecting.
        var screenPosition = camera.UnprojectPosition(headPosition);
        _marker.Position = new Vector2(
            screenPosition.X - (_marker.Size.X / 2f),
            screenPosition.Y - _presentation.SelectionMarkerScreenGap - _marker.Size.Y);
        _marker.Visible = true;
    }

    // The player asked to see the selected person's full page.
    private void OpenDetail()
    {
        if (_person is not { } person)
        {
            return;
        }

        _detailPanel.Open(SelectionCard.For(_world, person), PersonActions.For(_world, person));
    }

    // Pressed on the pack line, on the selected person's own card or on their detail page.
    private void OnPackRequested()
    {
        if (_person is { } person)
        {
            WorkshopRequested?.Invoke(person);
        }
    }

    private void RefreshBandPanel()
    {
        if (_bandPanel.Visible)
        {
            _bandPanel.Update(BandRoster.Of(_world));
        }
    }
}
