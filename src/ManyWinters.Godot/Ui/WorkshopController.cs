using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Interaction;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Ui;

// Everything the workbench does, from the pack line that opens it to naming a thing nobody has a
// word for yet: panel construction, picked-item offers, recipes, attempts, eating, dropping, and
// the naming question laid over it. A caller only ever asks to open or close a workshop for a
// person; how the bench decides what a pick can do stays in WorkshopActions.
internal sealed class WorkshopController
{
    private readonly WorldState _world;
    private readonly OrderCoordinator _orders;
    private readonly WorkshopPanel _workshop;
    private readonly NamingPanel _namingPanel;

    // Who the currently open workshop was opened for - remembered rather than re-asked of a
    // selection that may have moved on by the time a recipe or naming callback fires.
    private Person? _person;

    // What the last attempt turned out, held only long enough for the player to name it.
    private Assembly? _justMade;

    // Said whenever a pick turns out to lead nowhere - several ways to say the same nothing, so
    // trying a few unworkable pairs in a row does not read as the game reciting one stock line
    // back at the player.
    private static readonly string[] NothingComesOfIt =
    [
        "Nothing comes of it.",
        "Nothing comes of that.",
        "No good comes of it.",
        "It comes to nothing.",
    ];

    // Letting the workshop go primes the tick accumulator, so the world starts again on the next
    // frame rather than a full interval later - as dismissing the controls page does. Raised
    // rather than done here: priming the accumulator is Main's clock to hold, not this one's.
    public event Action? Closed;

    // A word the band coined outlives whoever coined it, so it goes in the chronicle rather than
    // only into the panel that asked for it. Raised rather than recorded here: the chronicle is
    // not this controller's to know about.
    public event Action<Inscription>? InscriptionRecorded;

    // The workbench, opened from the pack line on the selected person's card. Like the pause page
    // it holds the clock while it is up (see SimulationLoop.Update): working a thing over is
    // meant to be unhurried.
    public WorkshopController(WorkshopUi ui, WorldState world, OrderCoordinator orders)
    {
        _world = world;
        _orders = orders;

        // ui.Shield is MainUi's to attach and show/hide alongside the panel (see MainUi) - the
        // clock is stopped while the bench is out, and an order given into a stopped clock lands
        // the moment it starts again (the same reasoning as InscriptionOverlay). It draws
        // nothing: the world is what the player is working in the middle of, and the camera keeps
        // turning over it.
        _workshop = ui.Panel;
        _workshop.Closed += () => Closed?.Invoke();
        _workshop.Attempted += OnAttempt;
        _workshop.EatRequested += OnEat;
        _workshop.DropRequested += OnDrop;
        _workshop.PickChanged += RefreshOffer;
        _workshop.RecipeInvoked += OnRecipe;

        _namingPanel = ui.NamingPanel;
        _namingPanel.Named += OnNamed;
        _namingPanel.Cancelled += () => _justMade = null;
    }

    public void Toggle(Person person)
    {
        // Pressing the pack line again puts the workbench away: the way in is the way out.
        if (_workshop.Visible)
        {
            _workshop.Close();
            return;
        }

        _person = person;

        // It puts itself in the middle of the screen and stays there (PanelPlacement.Centred):
        // the world stands still while this is open, so it is the thing being done rather than a
        // card to read beside it.
        _workshop.Open(WorkshopActions.Carried(_world, person), WorkshopActions.Recipes(_world, person));

        // A verbose session follows the game from its log alone; the bench coming up says so, and
        // for whom.
        if (LaunchOptions.Verbose)
        {
            GD.Print($"Workshop opened for {person.Name}.");
        }

        RefreshOffer();
    }

    public void Close() => _workshop.Close();

    // The naming question sits over the workshop rather than beside it, so Escape only reaches
    // the workshop underneath once there is no question sitting on top of it to answer first.
    public void HandleEscape()
    {
        if (_namingPanel.Visible)
        {
            _namingPanel.Close();
        }
        else
        {
            _workshop.Close();
        }
    }

    // Pressed a "Make X" line rather than picked something to try - the recipe list has its own
    // event because a successful one changes the pack the same attempt does, and the panel needs
    // both redrawn (WorkshopActions.Carried, WorkshopActions.Recipes).
    private void OnRecipe(ActionOffer offer)
    {
        if (_person is not { } person)
        {
            return;
        }

        _orders.Perform(person, offer);
        RefreshPack(person);
    }

    // Eat or Drop, pressed on whatever is picked. Neither is an attempt (WorkshopActions.Attempt)
    // - there is no dice roll and no cost to the clock, the same as pressing either off the
    // person's own card - so this only carries the command out and redraws the pack underneath
    // it, the way a recipe does (OnRecipe).
    private void OnEat()
    {
        if (_person is not { } person || WorkshopActions.Eat(_world, person, _workshop.Picked) is not { } offer)
        {
            return;
        }

        _orders.Perform(person, offer);
        RefreshPack(person);
    }

    private void OnDrop()
    {
        if (_person is not { } person || WorkshopActions.Drop(_world, person, _workshop.Picked) is not { } offer)
        {
            return;
        }

        _orders.Perform(person, offer);
        RefreshPack(person);
    }

    // Redrawn after anything that could have changed what is carried - the pack, the recipes it
    // makes room for, and what the current pick can now do.
    private void RefreshPack(Person person)
    {
        _workshop.Show(WorkshopActions.Carried(_world, person));
        _workshop.ShowRecipes(WorkshopActions.Recipes(_world, person));
        RefreshOffer();
    }

    // What the current pick would do, asked of the world rather than of the panel: the panel
    // holds no world and no opinion about what works (see WorkshopActions).
    private void RefreshOffer()
    {
        if (_person is not { } person)
        {
            return;
        }

        var offer = WorkshopActions.Attempt(_world, person, _workshop.Picked);
        _workshop.Offer(offer, RefusalFor(offer), WorkshopActions.WordsFor(_world, person, _workshop.Picked));
        _workshop.OfferItemActions(
            WorkshopActions.Eat(_world, person, _workshop.Picked),
            WorkshopActions.Drop(_world, person, _workshop.Picked));
    }

    // Nothing is said about a pick that leads nowhere until the player has picked something: an
    // empty workbench that already says "nothing comes of it" is answering a question nobody
    // asked.
    private string? RefusalFor(ActionOffer? offer) => (offer, _workshop.Picked.Count) switch
    {
        (null, 0) => null,
        (null, _) => NothingComesOfIt[Random.Shared.Next(NothingComesOfIt.Length)],
        ({ IsAvailable: false }, _) => ActionBlockerText.For(offer.Value),
        _ => null,
    };

    private void OnAttempt()
    {
        if (_person is not { } person
            || WorkshopActions.Attempt(_world, person, _workshop.Picked) is not { } offer)
        {
            return;
        }

        var before = person.Inventory.Assemblies.ToList();
        _orders.Perform(person, offer);

        // An attempt costs time whether or not it came off - the clock is held while the bench is
        // open, so this is the only thing that moves it, and it is what stops a player pressing
        // until the dice land (see WorkAttempt, SimulationRules.TicksPerWorkAttempt).
        _world.Advance(_world.Configuration.Rules.TicksPerWorkAttempt);

        var made = person.Inventory.Assemblies.FirstOrDefault(held => !before.Remove(held));
        _workshop.Show(WorkshopActions.Carried(_world, person));
        _workshop.ShowRecipes(WorkshopActions.Recipes(_world, person));

        // Before ReportOutcome, not after: refreshing the offer recomputes the status line from
        // the pick (now empty, the thing just picked having been consumed or come apart), and
        // would otherwise overwrite the very sentence this method is about to report.
        RefreshOffer();
        _workshop.ReportOutcome(made is null
            ? "It comes apart in your hands."
            : $"It comes out {InspectorText.ForWorkedThing(made, _world)}.");

        // A shape nobody in the band has a word for is a thing worth naming, and this is the
        // moment to ask: they are looking at what they just made (see Vocabulary).
        if (made is not null && !_world.Vocabulary.HasAWordFor(made))
        {
            _justMade = made;
            _namingPanel.Open(InspectorText.ForWorkedThing(made, _world), WorkshopPanel.IconFor(new CarriedThing.Worked(made)));
        }
    }

    private void OnNamed(string word)
    {
        if (_justMade is not { } made || _person is not { } person)
        {
            return;
        }

        _world.Vocabulary.Name(made, word);
        _justMade = null;

        InscriptionRecorded?.Invoke(new Inscription(
            "A name for it",
            [$"{person.Name} made a thing the band had no word for.", $"They are calling it {word}."],
            // Carried even though the chronicle leaves closing words off the page: an
            // inscription without them is one the overlay cannot be dismissed from, and that is
            // meant only for a band with nobody left (see InscriptionOverlay.Show).
            "The word is passed along"));

        _workshop.ReportOutcome($"They are calling it {word}.");
        _workshop.Show(WorkshopActions.Carried(_world, person));
        _workshop.ShowRecipes(WorkshopActions.Recipes(_world, person));
    }
}
