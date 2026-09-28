using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Views;

namespace ManyWinters.Godot.Interaction;

// Every player-issued ActionOffer - pressed on a card, picked off the menu, or meant by a click
// in the world - ends up here. The offer carries both the command and the world's own answer
// about whether it can run, so nothing is re-checked: this is the one place that turns "yes, and
// here is how" into a command, and the one place that remembers an order somebody has to walk to
// before it fires.
internal sealed class OrderCoordinator(
    WorldState world,
    WorldPresenter presenter,
    PresentationSettings presentation)
{
    private readonly PendingOrders _pendingOrders = new();

    // Lets composition code refresh selection, buildings, and graves without this coordinator
    // knowing those panels exist.
    public event Action? WorldChanged;

    // The wording a failed delayed order is told to the player in, carried up rather than
    // notified directly so this stays free of StatusBar.
    public event Action<string>? OrderFailed;

    // Every action the player asks for arrives here - pressed on a card, picked off the menu, or
    // meant by a left click on something in the world.
    public void Perform(Person person, ActionOffer offer)
    {
        if (!offer.IsAvailable)
        {
            return;
        }

        // A verbose session follows the game from its log alone, so every order says what it
        // turned into.
        if (LaunchOptions.Verbose)
        {
            GD.Print($"Order by {person.Name}: {offer.Label}.");
        }

        // Nobody starts knowing anything: being directed is how a person is shown the way, so an
        // action that teaches grants its base technique first. Granted when the order is given
        // rather than when it is carried out, so somebody sent off to a tree already knows what
        // to do with it by the time they get there.
        if (offer.TeachFirst is { } skill)
        {
            TeachBaseTechniqueIfNeeded(person, skill);
        }

        switch (OrderPlan.For(offer))
        {
            case OrderDispatch.InstallPursuit:
                // Hunt and Butcher both work this way: the task walks (or does not need to) and
                // the simulation's own advance step runs the command itself every tick the task
                // stays current, so there is nothing here to remember and re-check on arrival -
                // unlike every other directed action, which is a single attempt once the walk ends.
                _pendingOrders.Forget(person);
                person.Tasks.Interrupt(offer.Pursuit!);
                break;

            case OrderDispatch.WalkThenExecute:
                // A pile is taken from at the tighter PileReachDistance, so the walk has to stop
                // closer too, or the order would arrive out of reach and never fire.
                var approachDistance = offer.Command is EatFromPileCommand or PickUpItemCommand
                    ? presentation.PileApproachDistance
                    : presentation.ApproachDistance;
                _pendingOrders.Add(person, offer);
                world.Execute(new MoveCommand(person, Position.Approach(person.Position, offer.Target!.Value, approachDistance)));
                break;

            default:
                // A new order replaces whatever they were on their way to do - including a plain
                // walk, which is the player changing their mind.
                _pendingOrders.Forget(person);
                Execute(offer.Command);
                break;
        }

        WorldChanged?.Invoke();
    }

    // Fires the order of everyone who has arrived where they were sent.
    public void ResolvePending()
    {
        foreach (var offer in _pendingOrders.Ready(world))
        {
            // A verbose session follows the game from its log alone; the tick that fires a
            // pending order says so.
            if (LaunchOptions.Verbose)
            {
                GD.Print($"Resolved '{offer.Label}'.");
            }

            Execute(offer.Command);
        }

        foreach (var failed in _pendingOrders.Failed)
        {
            OrderFailed?.Invoke($"{failed.Person.Name} arrived too late to {failed.Label.ToLowerInvariant()}.");
        }
    }

    // Two commands take something off the map, and views are pushed to the presenter rather than
    // reconciled from world state, so both have to say so. The per-tick sweep would catch a felled
    // node a moment later; a buried person it would never catch at all.
    private void Execute(ICommand command)
    {
        world.Execute(command);

        switch (command)
        {
            case FellCommand fell:
                presenter.RemoveResourceNodeView(fell.Node.Id);
                break;
            case BuryCommand bury:
                presenter.RemovePersonView(bury.Deceased.Id);
                break;
        }
    }

    private void TeachBaseTechniqueIfNeeded(Person person, SkillTypeId skill)
    {
        var baseTechnique = world.Configuration.SkillCatalog.Get(skill).BaseTechnique;
        if (!person.KnownTechniques.Contains(baseTechnique))
        {
            world.Execute(new GrantTechniqueCommand(person, baseTechnique));
        }
    }
}
