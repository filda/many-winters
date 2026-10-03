using Godot;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Presentation.Interaction;
using ManyWinters.Presentation.Logic;
using ManyWinters.Presentation.Views;

namespace ManyWinters.Presentation.Ui;

// Who the player has picked out of the world - a person or a grave, never both - and everything
// on screen that shows it: the card, the band's roster, and the marker over the selected
// person's head. A caller only ever asks for a selection transition; this is
// the one place the mutual exclusion between a person and a grave is kept.
public sealed class SelectionController
{
    private readonly WorldState _world;
    private readonly WorldPresenter _presenter;
    private readonly FreeCameraRig _cameraRig;
    private readonly PresentationSettings _presentation;
    private readonly TextureRect _marker;
    private readonly SelectionPanel _selectionPanel;
    private readonly BandPanel _bandPanel;

    private Person? _person;
    private Animal? _animal;
    private Grave? _grave;

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
        _selectionPanel.PackRequested += OnPageRequested;
        _selectionPanel.DetailRequested += OnPageRequested;
        _selectionPanel.CloseRequested += Clear;

        _bandPanel = ui.BandPanel;
        _bandPanel.PersonChosen += SelectAndFocus;

        // The world forgets an animal's bones on its own, with nobody asking - if that was the
        // one selected, its card must come down with it rather than keep showing a corpse whose
        // view is already gone.
        world.AnimalRemoved += OnAnimalRemoved;
    }

    // Forwarded from the selection card or (in composition code) the contextual menu - both draw
    // offers for whoever is selected, so this is the one signal a caller needs to carry an offer
    // out.
    public event Action<ActionOffer>? ActionInvoked;

    // The person's page, asked for from the name or the pack line on the card - who it was asked
    // for, since a page is opened for somebody rather than for whoever happens to be selected when
    // it finally opens.
    public event Action<Person>? PageRequested;

    // Raised after every selection change, so the still-separate debug inspector can redraw from
    // Person or Grave without this controller knowing that window exists.
    public event Action? Refreshed;

    public Person? Person => _person;

    public Grave? Grave => _grave;

    // Whichever of the two - a person or an animal - is selected, for everything that only reads
    // what every Creature has: the marker's position, the occlusion fade's sight line, the clock's
    // idle-grace hint. Never both, so there is never a question of which one wins.
    public Creature? SelectedCreature => (Creature?)_person ?? _animal;

    public void Select(Person person)
    {
        _person = person;
        _animal = null;
        _grave = null;
        Refresh();

        // A verbose session follows the game from its log alone; the card coming up for the
        // person just picked says so.
        if (LaunchOptions.Verbose)
        {
            GD.Print($"Card shown for {person.Name}.");
        }
    }

    public void Select(Animal animal)
    {
        _animal = animal;
        _person = null;
        _grave = null;
        Refresh();
    }

    public void Select(Grave grave)
    {
        _grave = grave;
        _person = null;
        _animal = null;
        Refresh();
    }

    // Every window that shows something about whoever is selected or was, closed together so a
    // future one is not the one somebody forgets to add here - which is exactly how the person's
    // page got left open through an ending it was never told about.
    public void CloseForBandEnd()
    {
        _bandPanel.Visible = false;
        Clear();
    }

    // Everything on screen that is about people: the player's panel for whoever is selected, and
    // the band's roster, whose lines go stale on exactly the same occasions.
    public void Refresh()
    {
        RefreshBandPanel();

        if (_grave is { } grave)
        {
            _selectionPanel.ShowGrave(InspectorText.ForGraveRecord(grave, _world.Configuration.SkillCatalog));
        }
        else if (_person is { } person)
        {
            _selectionPanel.ShowPerson(SelectionCard.For(_world, person), PersonActions.For(_world, person));
        }
        else if (_animal is { } animal)
        {
            // No page and no actions for an animal yet: the card is everything there is to show.
            _selectionPanel.ShowAnimal(AnimalCard.For(_world, animal));
        }
        else
        {
            _selectionPanel.ClearSelection();
        }

        Refreshed?.Invoke();
    }

    // Placed on the way open rather than once at setup, so it always comes back where the player
    // expects it however far they dragged it last time: mirrored across the screen from the
    // selection panel, same inset from its own edge, the band on the left and whoever is picked
    // out of it on the right.
    //
    // Filled on the way open as well as on every tick: the clock can be standing still (a pause,
    // an inscription), and an empty roster is no answer to "where is everybody".
    public void ToggleBandPanel()
    {
        _bandPanel.Visible = !_bandPanel.Visible;
        _bandPanel.Position = new Vector2(BandPanel.Margin, BandPanel.Margin);
        RefreshBandPanel();
    }

    // A 2D overlay, not a 3D billboard. Camera3D.UnprojectPosition/IsPositionBehind do the
    // projection; this anchors a Control on it.
    public void UpdateMarker()
    {
        if (SelectedCreature is not { } creature
            || _presenter.GetCreatureGlobalPosition(creature.Id) is not { } creaturePosition
            || _presenter.GetCreatureHeadHeightOffset(creature.Id) is not { } headHeightOffset)
        {
            _marker.Visible = false;
            return;
        }

        var camera = _cameraRig.Camera;
        var headPosition = creaturePosition + new Vector3(0, headHeightOffset, 0);
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

    // Pressing a name on the roster: select the person and take the view to them. Selecting alone
    // would leave the player looking at the same empty forest with a marker somewhere off screen.
    private void SelectAndFocus(Person person)
    {
        Select(person);

        if (_presenter.GetCreatureGlobalPosition(person.Id) is { } position)
        {
            _cameraRig.FocusOn(position);
        }
    }

    // The player put their own card away with the cross in its corner: nobody is selected any
    // more, so the card comes down with the selection rather than on its own.
    private void Clear()
    {
        _person = null;
        _animal = null;
        _grave = null;
        Refresh();
    }

    private void OnAnimalRemoved(Animal animal)
    {
        if (ReferenceEquals(_animal, animal))
        {
            Clear();
        }
    }

    // Pressed on the name or the pack line on the selected person's card.
    private void OnPageRequested()
    {
        if (_person is { } person)
        {
            PageRequested?.Invoke(person);
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
