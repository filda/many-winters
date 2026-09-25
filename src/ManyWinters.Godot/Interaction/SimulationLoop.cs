using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Godot.Fog;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Ui;
using ManyWinters.Godot.Views;

namespace ManyWinters.Godot.Interaction;

// The one simulation tick a rendered frame may owe, and everything that has to stay in step
// with it: exploration/fog/cloud refresh, pending orders, the status bar's clock, selection and
// debug summaries, band-ending announcements, and pushing each person's and resource's new state
// to its view. Continuous presentation that runs regardless of ticking lives in
// WorldFrameUpdater instead.
internal sealed class SimulationLoop(
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
        // Time stands still while any registered modal holds the clock (see MainUi) - an
        // inscription, a pause the player asked for, the controls page, the workbench, the
        // detail page - and, in a session launched with the clock held, for as long as the
        // session runs; the world is stepped on purpose with the "advance one tick" key (see
        // Main._Input) instead of letting the wall clock decide. Not calling Advance at all is
        // what keeps a held clock from consuming accumulated time.
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
    // "advance one tick" key (see Main._Input) while the deterministic simulation is frozen, so
    // an order placed during the freeze is resolved exactly once and the frame settles at the
    // next fixed tick.
    public void TickOnce()
    {
        if (selection.Person is { } selectedPerson)
        {
            world.Execute(new GrantIdleGraceCommand(selectedPerson, pacing.SelectedPersonIdleGraceTicks));
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
            // A person who dies mid-stride still tweens to that tick's final position over the
            // next second - one last visible step. Snapping (overSeconds: 0) once dead pins the
            // corpse there with nothing left to glide.
            presenter.SetPersonPosition(person.Id, person.Position, person.IsAlive ? (float)pacing.TickIntervalSeconds : 0f);
        }

        foreach (var node in world.Entities)
        {
            if (node.Growth is not { } growth)
            {
                continue;
            }

            if (!growth.IsAlive)
            {
                // Nodes that withered from climate stress (see WorldState.Advance); felling
                // removes its own view immediately.
                presenter.RemoveResourceNodeView(node.Id);
                continue;
            }

            presenter.SetResourceNodeHasFruit(node.Id, growth.RemainingAmount > 0);
        }

        GD.Print($"Tick {world.Clock.CurrentTick}: {world.People.Count(p => p.IsAlive)} of {world.People.Count} people alive.");
    }

    // What letting a clock-holding page go is supposed to do: the world resumes on the very next
    // frame rather than up to a full interval later (see TickAccumulator).
    public void TickAsSoonAsPossible() => _accumulator.TickAsSoonAsPossible();

    // Also called outside a tick, right after an order executes or is queued (see
    // OrderCoordinator.WorldChanged): a felled tree or a built store should not wait for the
    // next tick to leave the debug counts stale.
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
