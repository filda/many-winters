using Godot;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Maps;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Godot.Fog;
using ManyWinters.Godot.Interaction;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Terrain;
using ManyWinters.Godot.Views;

namespace ManyWinters.Godot.Ui;

// The lifecycle of successive bands in this same world: the initial arrival, the fate the
// simulation is heading toward, the epitaph once nobody is left, and the successor that follows
// it. Composes existing Core decisions (BandArrival, BandEnding, Prologue, Epitaph) rather than
// repeating them - this type only sequences the presentation around what they already decided.
internal sealed class BandContinuityController
{
    private readonly WorldState _world;
    private readonly WorldPresenter _presenter;
    private readonly FogOfWarRenderer _fogOfWar;
    private readonly GroundClouds _groundClouds;
    private readonly FreeCameraRig _cameraRig;
    private readonly TerrainRenderer _terrain;
    private readonly MainUi _ui;
    private readonly SelectionController _selection;
    private readonly WorkshopController _workshop;

    private EndingAnnouncements _endingAnnouncements = new();

    // Captured the one moment BandArrival.Of really means "just arrived"; read again later only
    // for its live population counts (see Main.TogglePause).
    public long ArrivalTick { get; private set; }

    public Position CampCenter { get; private set; }

    public BandContinuityController(
        WorldState world,
        Position initialCampCenter,
        WorldPresenter presenter,
        FogOfWarRenderer fogOfWar,
        GroundClouds groundClouds,
        FreeCameraRig cameraRig,
        TerrainRenderer terrain,
        MainUi ui,
        SelectionController selection,
        WorkshopController workshop)
    {
        _world = world;
        CampCenter = initialCampCenter;
        _presenter = presenter;
        _fogOfWar = fogOfWar;
        _groundClouds = groundClouds;
        _cameraRig = cameraRig;
        _terrain = terrain;
        _ui = ui;
        _selection = selection;
        _workshop = workshop;

        _ui.InscriptionOverlay.AnotherBandRequested += StartAnotherBand;
        // A word the band coined outlives whoever coined it, so it goes in the chronicle rather
        // than only into the panel that asked for it.
        _workshop.InscriptionRecorded += Record;
    }

    // The band the player starts with, captured once presentation exists to show it arriving.
    public void ShowInitialArrival()
    {
        var arrival = BandArrival.Of(_world);
        ArrivalTick = arrival.ArrivalTick;
        ShowInscription(Prologue.Write(arrival), offerAnotherBand: false);
    }

    // The fate is read off the world every tick and shown the first tick it changes (see
    // EndingAnnouncements): once when the last man or woman dies, once more when the last
    // person does.
    public void AnnounceEndingIfAny()
    {
        if (!_endingAnnouncements.ShouldAnnounce(BandEnding.FateOf(_world.People)))
        {
            return;
        }

        if (BandEnding.Of(_world) is { } ending)
        {
            var nobodyIsLeft = ending.Fate == BandFate.Ended;

            // The roster and the selection are about the dead, and the world under this epitaph
            // waits for a successor band: put the old band's windows away now, the way that
            // band's arrival would have (StartAnotherBand), not only then.
            if (nobodyIsLeft)
            {
                CloseBandWindows();
            }

            ShowInscription(Epitaph.Write(ending), offerAnotherBand: nobodyIsLeft);
        }
    }

    // Written down without stopping anything. For a moment the player is already living
    // through - they have just typed the name themselves - taking the whole screen to tell them
    // what they did would be ceremony in the way of play. The chronicle keeps it either way, and
    // that is what outlives the band.
    private void Record(Inscription inscription)
    {
        _ui.Chronicle.Add(inscription);
        _ui.StatusBar.ShowChronicleButton();
        GD.Print($"Inscription: {inscription.Title}");
    }

    // A successor band arrives into this same world: a fresh crowd is spawned and the prologue
    // takes their place on screen. The old band's dead and graves, buildings, and chronicle stay
    // where they are.
    private void StartAnotherBand()
    {
        // Put away the old band's windows - the roster and selection are about dead people.
        CloseBandWindows();

        // The new band has not walked this land yet - fog clears around their new camp.
        _world.Exploration.Reset();

        var idRng = new Random(_world.Clock.CurrentTick.GetHashCode());
        CampCenter = MapLoader.SpawnNewBand(_world, idRng, CampCenter);

        // So the new band's fate changes are announced independently of the old band's.
        _endingAnnouncements = new EndingAnnouncements();

        // Brief pre-roll so the new band is not standing still behind the prologue.
        _world.Advance(IdleTask.MaxPauseTicks + 1);

        // Main._Process is blocked while the inscription is up, so refresh the fog here rather
        // than waiting for it.
        _presenter.RefreshExploration(_cameraRig.RigGlobalPosition, _cameraRig.ViewRadius);
        _fogOfWar.Refresh();
        _groundClouds.Refresh();

        ShowInitialArrival();

        var campX = (float)CampCenter.X;
        var campZ = (float)CampCenter.Y;
        var campHeight = _terrain.SampleHeight(campX, campZ);
        _cameraRig.FocusOn(new Vector3(campX, campHeight, campZ));
    }

    // Every window that shows something about whoever is selected or was, closed together so a
    // future one is not the one somebody forgets to add here - which is exactly how the detail
    // page got left open through an ending it was never told about.
    private void CloseBandWindows()
    {
        _selection.CloseForBandEnd();
        _workshop.Close();
    }

    // Every inscription stops the clock until dismissed (see Main._Process); its title goes up
    // on the overlay and the whole of it into the chronicle, where it stays for the session.
    private void ShowInscription(Inscription inscription, bool offerAnotherBand)
    {
        Record(inscription);
        _ui.InscriptionOverlay.Show(inscription, offerAnotherBand);
    }
}
