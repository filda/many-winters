using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Presentation.Fog;
using ManyWinters.Presentation.Logic;
using ManyWinters.Presentation.Ui;
using ManyWinters.Presentation.Views;

namespace ManyWinters.Presentation.Interaction;

// The one simulation tick a rendered frame may owe, and everything that must stay in step with
// it. Continuous presentation that runs regardless of ticking lives elsewhere.
public sealed class SimulationLoop(
    WorldState world,
    SimulationPacing pacing,
    WorldPresenter presenter,
    FreeCameraRig cameraRig,
    FogOfWarRenderer fogOfWar,
    GroundClouds groundClouds,
    OrderCoordinator orders,
    MainUi ui,
    SelectionController selection,
    BandContinuityController continuity)
{
    private readonly TickAccumulator _accumulator = new(pacing.TickIntervalSeconds);

    public void Update(double delta)
    {
        // Time stands still while any registered modal holds the clock - an inscription, a pause
        // the player asked for, the controls page, the workbench, the detail page - and, in a
        // session launched with the clock held, for as long as the session runs; the world
        // advances instead only via the "advance one tick" key. Not calling Advance at all keeps
        // a held clock from consuming accumulated time.
        if (ui.HoldsClock || LaunchOptions.ClockHeld)
        {
            return;
        }

        if (!_accumulator.Advance(delta))
        {
            return;
        }

        TickOnce();
    }

    // One simulation tick: the world advances, and everything that has to stay in step with it -
    // exploration/fog/clouds, pending orders, the status-bar clock, selection and debug
    // summaries, band-ending announcements, and each person's and resource's new state - is
    // pushed to the views. Called by Update when the accumulator is full, and directly by the
    // "advance one tick" key while the deterministic simulation is frozen, so an order placed
    // during the freeze is resolved exactly once and the frame settles at the next fixed tick.
    public void TickOnce()
    {
        if (selection.SelectedCreature is { } selectedCreature)
        {
            world.Execute(new GrantIdleGraceCommand(selectedCreature, pacing.SelectedPersonIdleGraceTicks));
        }

        world.Advance(1);
        presenter.RefreshExploration(cameraRig.RigGlobalPosition, cameraRig.ViewRadius);
        fogOfWar.Refresh();
        groundClouds.Refresh();
        orders.ResolvePending();
        ui.StatusBar.SetTick(world.Clock.CurrentTick, world.CurrentSeason);
        selection.Refresh();
        RefreshBuildingsLabel();
        RefreshGravesLabel();
        continuity.AnnounceEndingIfAny();

        foreach (var person in world.People)
        {
            presenter.SetPersonAlive(person.Id, person.IsAlive);
            // Before the position: the target it is given is lifted by the ground-contact
            // correction this tick's size produced. A corpse keeps the age it died at.
            presenter.SetPersonAge(person.Id, world.ExactAgeInYearsAt(person, person.DeathTick ?? world.Clock.CurrentTick));
            // Never true before IsAlive is false, so this is always the second of the two - a
            // dead person's bones never vanish, only their look deepens once the record of them
            // has decayed.
            presenter.SetPersonDecayed(person.Id, world.IsDecayed(person));
            // A person who dies mid-stride still tweens to that tick's final position over the
            // next second - one last visible step. Snapping (overSeconds: 0) once dead pins the
            // corpse there with nothing left to glide.
            presenter.SetPersonPosition(person.Id, person.Position, person.IsAlive ? (float)pacing.TickIntervalSeconds : 0f);
        }

        foreach (var animal in world.Animals)
        {
            presenter.SetAnimalAlive(animal.Id, animal.IsAlive);
            presenter.SetAnimalAge(animal.Id, world.ExactAgeInYearsAt(animal, animal.DeathTick ?? world.Clock.CurrentTick));
            presenter.SetAnimalDecayed(animal.Id, world.IsDecayed(animal));
            presenter.SetAnimalPosition(animal.Id, animal.Position, animal.IsAlive ? (float)pacing.TickIntervalSeconds : 0f);
        }

        foreach (var node in world.Entities)
        {
            if (node.Growth is not { } growth)
            {
                continue;
            }

            if (!growth.IsAlive)
            {
                // Nodes that withered from climate stress; felling removes its own view
                // immediately.
                presenter.RemoveResourceNodeView(node.Id);
                continue;
            }

            presenter.SetResourceNodeHasFruit(node.Id, growth.RemainingAmount > 0);
        }

        GD.Print($"Tick {world.Clock.CurrentTick}: {world.People.Count(p => p.IsAlive)} of {world.People.Count} people alive.");

        // Its own line, not folded into the one above: the herds exist from world creation, well
        // before the first tick, but the test harness's own boot-time log offset makes every
        // earlier line unreadable to a test, so this prints every tick, same as the population
        // line above - the first "advance one tick" a golden path test does is enough for E2E to
        // witness the herds exist without clicking anything.
        GD.Print($"Animals: {world.Animals.Count}");

        // A verbose session follows the game from its log alone, so each tick also says what the
        // renderer drew - the one honest answer to whether the world reached the screen at all.
        if (LaunchOptions.Verbose)
        {
            GD.Print($"Draw: {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame):0} objects, "
                + $"{Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0} calls.");
        }
    }

    // What letting a clock-holding page go is supposed to do: the world resumes on the very next
    // frame rather than up to a full interval later.
    public void TickAsSoonAsPossible() => _accumulator.TickAsSoonAsPossible();

    // Also called outside a tick, right after an order executes or is queued: a felled tree or a
    // built store should not wait for the next tick to leave the debug counts stale.
    public void RefreshBuildingsLabel()
    {
        var buildings = world.Entities.Where(e => e.Category == EntityCategory.Building).ToList();
        ui.Inspector.ShowBuildings("Buildings: " + (buildings.Count > 0
            ? string.Join(", ", buildings.Select(BuildingSummary))
            : "none"));
    }

    public void RefreshGravesLabel() => ui.Inspector.ShowGraves($"Graves: {world.Graves.Count}");

    private static string BuildingSummary(Entity building)
    {
        var inventory = building.Storage!.Counts.Count > 0
            ? string.Join(", ", building.Storage.Counts.Select(kv => $"{kv.Key} x{kv.Value}"))
            : "empty";
        return $"{building.Kind} #{building.Id} ({building.Condition!.Value:0}%) [{inventory}]";
    }
}
