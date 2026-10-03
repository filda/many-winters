using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Presentation.Interaction;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Ui;

// Everything the person's page does, from the card that opens it to naming a thing nobody has a
// word for yet: opening and closing the page, picked-item offers, recipes, attempts, eating,
// dropping, the naming question laid over it, and redrawing the person's half after anything the
// bench changes. A caller only ever asks to open or close the page for a person; how the bench
// decides what a pick can do stays in WorkshopActions.
public sealed class PersonPageController
{
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

    private readonly WorldState _world;
    private readonly OrderCoordinator _orders;
    private readonly PersonDetailPanel _page;
    private readonly WorkshopBench _bench;
    private readonly NamingPanel _namingPanel;

    // Who the currently open page was opened for - remembered rather than re-asked of a
    // selection that may have moved on by the time a recipe or naming callback fires.
    private Person? _person;

    // What the last attempt turned out, held only long enough for the player to name it.
    private Assembly? _justMade;

    // Like the pause page the person's page holds the clock while it is up: working a thing over
    // is meant to be unhurried.
    public PersonPageController(PersonPageUi ui, WorldState world, OrderCoordinator orders)
    {
        _world = world;
        _orders = orders;

        // MainUi attaches the page and shows and hides the shield with it - the clock is stopped
        // while the page is out, and an order given into a stopped clock lands the moment it
        // starts again. The shield draws nothing: the world is what the player is working in the
        // middle of, and the camera keeps turning over it.
        _page = ui.Page;
        _page.Closed += OnPageClosed;

        _bench = _page.Bench;
        _bench.Attempted += OnAttempt;
        _bench.EatRequested += OnEat;
        _bench.DropRequested += OnDrop;
        _bench.PickChanged += RefreshOffer;
        _bench.RecipeInvoked += OnRecipe;

        _namingPanel = ui.NamingPanel;
        _namingPanel.Named += OnNamed;
        _namingPanel.Cancelled += () => _justMade = null;
    }

    // Letting the page go primes the tick accumulator, so the world starts again on the next
    // frame rather than a full interval later - as dismissing the controls page does. Raised
    // rather than done here: priming the accumulator is Main's clock to hold, not this one's.
    public event Action? Closed;

    // A word the band coined outlives whoever coined it, so it goes in the chronicle rather than
    // only into the panel that asked for it. Raised rather than recorded here: the chronicle is
    // not this controller's to know about.
    public event Action<Inscription>? InscriptionRecorded;

    public void Open(Person person)
    {
        _person = person;

        // It puts itself in the middle of the screen and stays there: the world stands still
        // while this is open, so it is the thing being done rather than a card to read beside it.
        _page.Open(SelectionCard.For(_world, person));
        _bench.Open(WorkshopActions.Carried(_world, person), WorkshopActions.Recipes(_world, person));

        // A verbose session follows the game from its log alone; the bench coming up says so, and
        // for whom.
        if (LaunchOptions.Verbose)
        {
            GD.Print($"Workshop opened for {person.Name}.");
        }

        RefreshOffer();
    }

    public void Close() => _page.Close();

    // The naming question sits over the page rather than beside it, so Escape only reaches the
    // page underneath once there is no question sitting on top of it to answer first.
    public void HandleEscape()
    {
        if (_namingPanel.Visible)
        {
            _namingPanel.Close();
        }
        else
        {
            _page.Close();
        }
    }

    // A question left hanging over a page that is gone would be answered about a person nobody is
    // looking at, and what the player did on the bench is not theirs to name any more.
    private void OnPageClosed()
    {
        _namingPanel.Close();
        _person = null;
        _justMade = null;
        Closed?.Invoke();
    }

    // Pressed a "Make X" line rather than picked something to try - the recipe list has its own
    // event because a successful one changes the pack the same attempt does, and the panel needs
    // both the pack and the recipes redrawn.
    private void OnRecipe(ActionOffer offer)
    {
        if (_person is not { } person)
        {
            return;
        }

        _orders.Perform(person, offer);
        RefreshPack(person);
    }

    // Eat or Drop, pressed on whatever is picked. Neither is an attempt - there is no dice roll
    // and no cost to the clock, the same as pressing either off the person's own card - so this
    // only carries the command out and redraws the pack underneath it, the way a recipe does.
    private void OnEat()
    {
        if (_person is not { } person || WorkshopActions.Eat(_world, person, _bench.Picked) is not { } offer)
        {
            return;
        }

        _orders.Perform(person, offer);
        RefreshPack(person);
    }

    private void OnDrop()
    {
        if (_person is not { } person || WorkshopActions.Drop(_world, person, _bench.Picked) is not { } offer)
        {
            return;
        }

        _orders.Perform(person, offer);
        RefreshPack(person);
    }

    // Redrawn after anything that could have changed what is carried - the pack, the recipes it
    // makes room for, what the current pick can now do, and the person's own half of the page,
    // which nothing else redraws while the clock stands still.
    private void RefreshPack(Person person)
    {
        _bench.Show(WorkshopActions.Carried(_world, person));
        _bench.ShowRecipes(WorkshopActions.Recipes(_world, person));
        _page.Show(SelectionCard.For(_world, person));
        RefreshOffer();
    }

    // What the current pick would do, asked of the world rather than of the panel: the panel
    // holds no world and no opinion about what works.
    private void RefreshOffer()
    {
        if (_person is not { } person)
        {
            return;
        }

        var offer = WorkshopActions.Attempt(_world, person, _bench.Picked);
        _bench.Offer(offer, RefusalFor(offer), WorkshopActions.WordsFor(_world, person, _bench.Picked));
        _bench.OfferItemActions(
            WorkshopActions.Eat(_world, person, _bench.Picked),
            WorkshopActions.Drop(_world, person, _bench.Picked));
    }

    // Nothing is said about a pick that leads nowhere until the player has picked something: an
    // empty workbench that already says "nothing comes of it" is answering a question nobody
    // asked.
    private string? RefusalFor(ActionOffer? offer) => (offer, _bench.Picked.Count) switch
    {
        (null, 0) => null,
        (null, _) => NothingComesOfIt[Random.Shared.Next(NothingComesOfIt.Length)],
        ({ IsAvailable: false }, _) => ActionBlockerText.For(offer.Value),
        _ => null,
    };

    private void OnAttempt()
    {
        if (_person is not { } person
            || WorkshopActions.Attempt(_world, person, _bench.Picked) is not { } offer)
        {
            return;
        }

        var before = person.Inventory.Assemblies.ToList();
        _orders.Perform(person, offer);

        // An attempt costs time whether or not it came off - the clock is held while the bench is
        // open, so this is the only thing that moves it, and it is what stops a player pressing
        // until the dice land.
        _world.Advance(_world.Configuration.Rules.TicksPerWorkAttempt);

        var made = person.Inventory.Assemblies.FirstOrDefault(held => !before.Remove(held));

        // Before ReportOutcome, not after: refreshing the offer recomputes the status line from
        // what is still picked, and would otherwise overwrite the very sentence this method is
        // about to report.
        RefreshPack(person);
        _bench.ReportOutcome(made is null
            ? "It comes apart in your hands."
            : $"It comes out {InspectorText.ForWorkedThing(made, _world)}.");

        // A shape nobody in the band has a word for is a thing worth naming, and this is the
        // moment to ask: they are looking at what they just made.
        if (made is not null && !_world.Vocabulary.HasAWordFor(made))
        {
            _justMade = made;
            _namingPanel.Open(InspectorText.ForWorkedThing(made, _world), WorkshopBench.IconFor(new CarriedThing.Worked(made)));
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
            // meant only for a band with nobody left.
            "The word is passed along"));

        RefreshPack(person);
        _bench.ReportOutcome($"They are calling it {word}.");
    }
}
