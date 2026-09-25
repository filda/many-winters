using Godot;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Terrain;
using ManyWinters.Godot.Ui;
using ManyWinters.Godot.Views;

namespace ManyWinters.Godot.Interaction;

// Everything the mouse means to the rendered world: what a click on a person, a resource, a
// pile, a building or bare ground does, what the right button's press-and-release opens, and
// when the free camera gets to see an event instead. It reads who is acting through
// SelectionController and carries every order out through OrderCoordinator; it never touches
// simulation state itself.
internal sealed class WorldInputController
{
    // The one thing the game says out loud about an order nobody can carry out. Every order the
    // player gives goes through Acting(), so it is said in exactly one way.
    private const string NobodySelected = "Select someone first, then tell them what to do.";

    private readonly WorldState _world;
    private readonly FreeCameraRig _cameraRig;
    private readonly SelectionController _selection;
    private readonly OrderCoordinator _orders;
    private readonly StatusBar _statusBar;
    private readonly PresentationSettings _presentation;
    private readonly ContextMenu _contextMenu;
    private readonly WorldPresenter _presenter;

    // Telling a right-click apart from the right-drag that turns the camera, and what the press
    // landed on until the button comes up (see HandleRightButton). The world's views report the
    // press; only the release decides whether a menu opens.
    private readonly RightClickGesture _rightClick = new();
    private Func<Person, TargetMenu>? _pointedAt;

    public WorldInputController(
        ContextMenu contextMenu,
        WorldState world,
        FreeCameraRig cameraRig,
        WorldPresenter presenter,
        TerrainRenderer terrain,
        SelectionController selection,
        OrderCoordinator orders,
        StatusBar statusBar,
        PresentationSettings presentation)
    {
        _world = world;
        _cameraRig = cameraRig;
        _presenter = presenter;
        _selection = selection;
        _orders = orders;
        _statusBar = statusBar;
        _presentation = presentation;

        _contextMenu = contextMenu;
        _contextMenu.ActionInvoked += PerformAction;
        _contextMenu.OpenRequested += ShowContextMenu;

        presenter.PersonClicked += OnPersonClicked;
        presenter.ResourceNodeClicked += OnResourceNodeClicked;
        presenter.BuildingClicked += OnBuildingClicked;
        presenter.GraveSelected += OnGraveSelected;
        presenter.ItemPileClicked += OnItemPileClicked;
        presenter.MissedClick += OnMissedClick;
        terrain.GroundInputEvent += OnGroundInputEvent;
    }

    // A line pressed on the selected person's card, the detail page, or the contextual menu -
    // all draw offers for whoever is selected, so that is who carries it out.
    public void PerformAction(ActionOffer offer)
    {
        if (_selection.Person is not { } person)
        {
            return;
        }

        _contextMenu.Close();
        _orders.Perform(person, offer);
    }

    public void CloseContextMenu() => _contextMenu.Close();

    // Pointer events Main._Input hands over once the global keyboard shortcuts have had their
    // turn.
    public void Handle(InputEvent @event, Viewport viewport)
    {
        HandleRightButton(@event);

        // Ahead of Godot's physics picking (which runs later, from unhandled input) so it wins
        // even when the pick would land on something opaque in front of a person - see
        // PresentationSettings.PersonClickScreenRadius.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton
            && viewport.GuiGetHoveredControl() is null)
        {
            // A click out in the world puts the menu away, as any menu closes when the player
            // looks elsewhere. Only out in the world: over the UI the press has to reach whatever
            // it landed on, and a button of the menu's own only fires when it comes back up.
            _contextMenu.Close();

            if (FindNearestPersonOnScreen(mouseButton.Position) is { } person)
            {
                // A verbose session follows the game from its log alone, so a selection says so.
                if (LaunchOptions.Verbose)
                {
                    GD.Print($"Selected {person.Name}.");
                }

                OnPersonClicked(person, MouseButton.Left);
                viewport.SetInputAsHandled();
            }
        }
    }

    // _UnhandledInput, not _Input: _Input fires before the UI gets the event, so wheel/drag over
    // a Control would also zoom/rotate the camera underneath. That alone is not enough - a
    // ScrollContainer with nothing left to scroll lets the wheel fall through - so the camera also
    // ignores everything while the cursor is over any Control at all.
    public void HandleUnhandled(InputEvent @event, Viewport viewport)
    {
        if (viewport.GuiGetHoveredControl() is not null)
        {
            return;
        }

        _cameraRig.HandleMouseInput(@event);
    }

    private void OnPersonClicked(Person person, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            _pointedAt = actor => TargetActions.For(_world, actor, person);
            return;
        }

        _selection.Select(person);
    }

    private void OnGraveSelected(Grave grave) => _selection.Select(grave);

    // A left click on a resource is the one shortcut kept from before there was a menu: "gather
    // that" is the only thing anybody means by pointing at a bush, and it is how the game is
    // played. Everything else aimed at a target is asked for by name, on the right button.
    //
    // Depleting a node to zero keeps its view - the plant is still there, fruitless until
    // RegenPerTick refills it. Only IsAlive turning false (felled or withered) removes it.
    private void OnResourceNodeClicked(Entity node, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            _pointedAt = actor => TargetActions.For(_world, actor, node);
            return;
        }

        if (Acting() is { } person)
        {
            _orders.Perform(person, TargetActions.Gather(_world, person, node));
        }
    }

    // A left click on a pile is the one shortcut kept, the same as a resource's Gather: "pick
    // that up" is the only thing anybody means by pointing at it.
    private void OnItemPileClicked(Entity pile, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            _pointedAt = actor => TargetActions.For(_world, actor, pile);
            return;
        }

        if (Acting() is { } person)
        {
            _orders.Perform(person, TargetActions.PickUp(_world, person, pile));
        }
    }

    // Either button opens the store's menu: a hut has no one obvious thing to do with it, so
    // putting something in, taking something out and mending it are equally the point.
    private void OnBuildingClicked(Entity building, MouseButton button)
    {
        _pointedAt = actor => TargetActions.For(_world, actor, building);

        if (button == MouseButton.Left)
        {
            ShowContextMenu(_contextMenu.GetViewport().GetMousePosition());
        }
    }

    // The view has already tried HoverRescue.TryClickElsewhere (a full re-cast of the ray past
    // everything ruled out) before forwarding here, so this genuinely is a ground click.
    //
    // The position handed over is the ray's hit on the view's collision box - up in the air on a
    // tree-sized box's front face, tens of meters off the ground under the cursor (see
    // GroundPick) - so only the screen position is reused and the ground re-derived. A click
    // that finds no ground (sky past the terrain's edge) is dropped rather than guessed.
    // ReSharper disable UnusedParameter.Global - position, normal and shapeIndex are unused here,
    // but the method must match CollisionObject3D.InputEventEventHandler to be wired as a view's
    // InputEvent handler.
    private void OnMissedClick(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIndex)
    {
        if (camera is not Camera3D camera3D
            || @event is not InputEventMouseButton { Pressed: true } mouseButton)
        {
            return;
        }

        if (GroundPick.FindGround(camera3D, mouseButton.Position) is { } groundPosition)
        {
            OnGroundClicked(groundPosition, mouseButton.ButtonIndex);
        }
    }

    // camera, normal and shapeIndex are unused for the same reason as OnMissedClick above.
    private void OnGroundInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIndex)
    {
        if (@event is InputEventMouseButton { Pressed: true } mouseButton)
        {
            OnGroundClicked(position, mouseButton.ButtonIndex);
        }
    }
    // ReSharper restore UnusedParameter.Global

    // The right button does two jobs: dragged it turns the camera (FreeCameraRig), pressed and
    // released in one spot it asks what may be done with whatever is under the cursor. So the
    // menu waits for the release (RightClickGesture), and what the cursor was over is recorded on
    // the press - the only half of it a view ever sees, since Godot delivers presses to colliders
    // through physics picking, which runs after this.
    private void HandleRightButton(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } pressed:
                _pointedAt = null;
                _contextMenu.Close();
                _rightClick.Press(pressed.Position);
                break;
            case InputEventMouseMotion motion:
                _rightClick.Moved(motion.Position);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false } released:
                if (_rightClick.Release())
                {
                    _contextMenu.RequestOpen(released.Position);
                }

                break;
        }
    }

    // Opens the menu for whatever the press landed on. An empty one is not opened at all: the one
    // target with nothing to offer is the selected person themselves, whose own card is already on
    // screen (see TargetActions).
    private void ShowContextMenu(Vector2 screenPosition)
    {
        if (_pointedAt is not { } menuFor || Acting() is not { } person)
        {
            return;
        }

        var menu = menuFor(person);
        if (menu.Offers.Count > 0)
        {
            _contextMenu.Open(menu.Heading, menu.Offers, screenPosition);
        }
    }

    // Whoever an order is for. Every order the player gives needs somebody to carry it out, and
    // this is the one place that says so when there is nobody.
    private Person? Acting()
    {
        if (_selection.Person is { } person)
        {
            return person;
        }

        _statusBar.Notify(NobodySelected);
        return null;
    }

    // The already-selected person is never a candidate: re-selecting is a no-op, and the radius
    // around them swallowed every "step over there" click on the ground at their feet. Excluded,
    // the click falls through to picking; their own opaque pixels still re-select them.
    private Person? FindNearestPersonOnScreen(Vector2 screenPosition)
    {
        var camera = _cameraRig.Camera;
        Person? nearest = null;
        var nearestDistance = float.MaxValue;

        foreach (var person in _world.People)
        {
            if (person == _selection.Person
                || _presenter.GetPersonGlobalPosition(person.Id) is not { } personGlobalPosition
                || camera.IsPositionBehind(personGlobalPosition))
            {
                continue;
            }

            var distance = camera.UnprojectPosition(personGlobalPosition).DistanceTo(screenPosition);
            if (distance <= _presentation.PersonClickScreenRadius && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = person;
            }
        }

        return nearest;
    }

    // Left means walk there; right asks what else could be done on that spot, which is where
    // building belongs - it needs a place chosen rather than a thing pointed at.
    //
    // Reached straight from the terrain's own collider as well as from a view that declined the
    // click, so the wheel has to be turned away here too (see OrderButtons).
    private void OnGroundClicked(Vector3 groundPosition, MouseButton button)
    {
        if (!OrderButtons.Includes(button))
        {
            return;
        }

        var ground = WorldSpace.ToSimulation(groundPosition);

        if (button == MouseButton.Right)
        {
            _pointedAt = actor => TargetActions.For(_world, actor, ground);
            return;
        }

        if (button == MouseButton.Left && Acting() is { } person)
        {
            _orders.Perform(person, TargetActions.WalkTo(_world, person, ground));
        }
    }
}
