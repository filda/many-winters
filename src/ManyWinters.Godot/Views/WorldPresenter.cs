using Godot;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Views;

public sealed class WorldPresenter
{
    private readonly Node3D _container;
    private readonly Action<Person, MouseButton> _onPersonClicked;
    private readonly Action<Entity, MouseButton> _onResourceNodeClicked;
    private readonly Action<Entity, MouseButton> _onBuildingClicked;
    private readonly Action<Grave> _onGraveSelected;
    private readonly Action<Entity, MouseButton> _onItemPileClicked;
    private readonly CollisionObject3D.InputEventEventHandler _onMissedClick;
    private readonly Func<float, float, float> _sampleHeight;
    private readonly ResourceCatalog _resourceCatalog;
    private readonly RevealableExploration _exploration;
    // One cursor, one highlighted thing - the invariant lives here, not in each view (see
    // HoverArbiter).
    private readonly HoverArbiter _hover = new();
    private readonly Dictionary<PersonId, PersonView> _personViews = new();
    private readonly Dictionary<EntityId, ResourceNodeView> _resourceNodeViews = new();
    private readonly Dictionary<GraveId, GraveView> _graveViews = new();
    private readonly Dictionary<EntityId, BuildingView> _buildingViews = new();
    private readonly Dictionary<EntityId, ItemPileView> _itemPileViews = new();

    // Read every tick by RefreshExploration. The world's live collections, not copies, so a
    // view created later (a new grave, someone born) is in here as soon as the simulation adds it.
    private readonly IReadOnlyList<Person> _people;
    private readonly IReadOnlyList<Grave> _graves;
    private readonly IReadOnlyList<Entity> _entities;

    // Fog of war: a node outside the ever-explored area gets no view at all, not a hidden one -
    // creating thousands of decoration views (MapLoader.ScatterDecorations) up front was the
    // biggest chunk of startup time. Kept here until its cell is explored (RefreshExploration).
    // Only Growable entities ever pile up at decoration scale, so only those go through pending.
    private readonly Dictionary<EntityId, Entity> _pendingResourceNodes = new();

    public WorldPresenter(
        Node3D container,
        WorldState world,
        RevealableExploration exploration,
        Action<Person, MouseButton> onPersonClicked,
        Action<Entity, MouseButton> onResourceNodeClicked,
        Action<Entity, MouseButton> onBuildingClicked,
        Action<Grave> onGraveSelected,
        Action<Entity, MouseButton> onItemPileClicked,
        CollisionObject3D.InputEventEventHandler onMissedClick,
        Func<float, float, float> sampleHeight)
    {
        _container = container;
        _onPersonClicked = onPersonClicked;
        _onResourceNodeClicked = onResourceNodeClicked;
        _onBuildingClicked = onBuildingClicked;
        _onGraveSelected = onGraveSelected;
        _onItemPileClicked = onItemPileClicked;
        _onMissedClick = onMissedClick;
        _sampleHeight = sampleHeight;
        _resourceCatalog = world.Configuration.ResourceCatalog;
        _exploration = exploration;
        _people = world.People;
        _graves = world.Graves;
        _entities = world.Entities;

        world.PersonAdded += CreatePersonView;
        world.EntityAdded += CreateEntityView;
        world.GraveAdded += CreateGraveView;
        // Only a pile-category entity ever fires this (see WorldState.RemoveEntity): a felled or
        // withered resource stays in Entities with Growth.IsAlive false instead, and a building is
        // never removed.
        world.EntityRemoved += entity => RemoveItemPileView(entity.Id);

        foreach (var person in world.People)
        {
            CreatePersonView(person);
        }

        foreach (var entity in world.Entities)
        {
            CreateEntityView(entity);
        }

        foreach (var grave in world.Graves)
        {
            CreateGraveView(grave);
        }
    }

    // Every rendered frame (Main._Process), not once per tick: camera and people keep moving
    // between ticks, so whether the cursor is still on the lit thing changes continuously.
    public void RevalidateHover() => _hover.Revalidate();

    public void SetPersonAlive(PersonId id, bool isAlive)
    {
        if (_personViews.TryGetValue(id, out var view))
        {
            view.SetAlive(isAlive);
        }
    }

    public void SetPersonPosition(PersonId id, Position position, float overSeconds)
    {
        if (_personViews.TryGetValue(id, out var view))
        {
            view.SetTargetPosition(WorldSpace.ToRender(position, PersonView.Height / 2f, _sampleHeight), overSeconds);
        }
    }

    public Vector3? GetPersonGlobalPosition(PersonId id) =>
        _personViews.TryGetValue(id, out var view) ? view.GlobalPosition : null;

    // For Main's screen-space selection marker: how far above the person's position the top of
    // the drawn silhouette sits (SpriteEntityView.TopHeightOffset) - a nominal half-height
    // would float or sink depending on the texture's own margins.
    public float? GetPersonHeadHeightOffset(PersonId id) =>
        _personViews.TryGetValue(id, out var view) ? view.TopHeightOffset : null;

    // For Main's occlusion fade, so the selection's own sprites are not treated as blocking
    // the view of themselves.
    public Node3D? GetPersonNode(PersonId id) => _personViews.GetValueOrDefault(id);

    public void RemovePersonView(PersonId id)
    {
        if (_personViews.TryGetValue(id, out var view))
        {
            view.QueueFree();
            _personViews.Remove(id);
        }
    }

    public void SetResourceNodeHasFruit(EntityId id, bool hasFruit)
    {
        if (_resourceNodeViews.TryGetValue(id, out var view))
        {
            view.SetHasFruit(hasFruit);
        }
    }

    public void RemoveResourceNodeView(EntityId id)
    {
        if (_resourceNodeViews.TryGetValue(id, out var view))
        {
            view.QueueFree();
            _resourceNodeViews.Remove(id);
        }
    }

    private void CreatePersonView(Person person)
    {
        var view = new PersonView(person, _hover, _onPersonClicked, _onMissedClick)
        {
            Name = person.Name,
            Position = WorldSpace.ToRender(person.Position, PersonView.Height / 2f, _sampleHeight),
        };
        // Snapped, not faded: there is nothing on screen to fade from. Called before the view
        // enters the tree, which is why SnapRemembered may not touch a node.
        view.SnapRemembered(IsOutOfSight(person.Position));
        _container.AddChild(view);
        _personViews[person.Id] = view;
    }

    // Picks which kind of view an Entity gets from its Category, since the model no longer
    // carries that in its static type (see Entity).
    private void CreateEntityView(Entity entity)
    {
        switch (entity.Category)
        {
            case EntityCategory.Growable:
                CreateResourceNodeView(entity);
                break;
            case EntityCategory.Pile:
                CreateItemPileView(entity);
                break;
            case EntityCategory.Building:
                CreateBuildingView(entity);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entity), entity.Category, "Unknown entity category.");
        }
    }

    private void CreateResourceNodeView(Entity node)
    {
        if (!_exploration.IsExplored(ExplorationState.CellFor(node.Position)))
        {
            _pendingResourceNodes[node.Id] = node;
            return;
        }

        CreateResourceNodeViewNow(node);
    }

    private void CreateResourceNodeViewNow(Entity node)
    {
        var canFell = _resourceCatalog.Get(node.Kind).CanFell;
        var view = new ResourceNodeView(node, canFell, _hover, _onResourceNodeClicked, _onMissedClick);
        view.Position = WorldSpace.ToRender(node.Position, view.Size / 2f, _sampleHeight);
        view.SnapRemembered(IsOutOfSight(node.Position));
        _container.AddChild(view);
        _resourceNodeViews[node.Id] = view;
    }

    // Once per simulation tick (Main._Process) and when the "Reveal Map" toggle flips - a
    // HashSet lookup per view at that cadence is cheap even at decoration scale, and each view's
    // early-out (RememberedFade.Retarget) ends most calls at once. Every family of view goes
    // through here, so a grave, hut or corpse the group walked away from dims with the trees;
    // what the view does with it is its own business.
    public void RefreshExploration()
    {
        RefreshResourceNodeExploration();

        // People are read from the world, not _personViews, because the cell to ask about is
        // wherever they are this tick. Anyone alive is always in sight - they are the eyes the
        // fog is drawn from - so this only ever dims the dead.
        foreach (var person in _people)
        {
            if (_personViews.TryGetValue(person.Id, out var personView))
            {
                personView.SetRemembered(IsOutOfSight(person.Position));
            }
        }

        foreach (var grave in _graves)
        {
            if (_graveViews.TryGetValue(grave.Id, out var graveView))
            {
                graveView.SetRemembered(IsOutOfSight(grave.Position));
            }
        }

        foreach (var entity in _entities)
        {
            switch (entity.Category)
            {
                case EntityCategory.Building when _buildingViews.TryGetValue(entity.Id, out var buildingView):
                    buildingView.SetRemembered(IsOutOfSight(entity.Position));
                    break;
                case EntityCategory.Pile when _itemPileViews.TryGetValue(entity.Id, out var pileView):
                    pileView.SetRemembered(IsOutOfSight(entity.Position));
                    break;
            }
        }
    }

    private bool IsOutOfSight(Position position) =>
        !_exploration.IsVisible(ExplorationState.CellFor(position));

    // Resource nodes' two extra jobs: promote a pending node whose cell is now explored to a
    // real view, and send a view whose cell is *not* explored back to pending. The latter only
    // happens when "Reveal Map" is switched off again (ExplorationState never un-explores a
    // cell): the fog shaders assume nothing is instantiated under unexplored ground, so a view
    // left there shows through as a fogged silhouette. Graves and buildings need neither: built
    // by the group's own hands, their cell is explored before they exist and stays so.
    private void RefreshResourceNodeExploration()
    {
        if (_pendingResourceNodes.Count > 0)
        {
            List<EntityId>? newlyExplored = null;
            foreach (var (id, node) in _pendingResourceNodes)
            {
                if (_exploration.IsExplored(ExplorationState.CellFor(node.Position)))
                {
                    (newlyExplored ??= new List<EntityId>()).Add(id);
                }
            }

            if (newlyExplored is not null)
            {
                foreach (var id in newlyExplored)
                {
                    var node = _pendingResourceNodes[id];
                    _pendingResourceNodes.Remove(id);
                    CreateResourceNodeViewNow(node);
                }
            }
        }

        List<EntityId>? backToPending = null;
        foreach (var (id, view) in _resourceNodeViews)
        {
            var cell = ExplorationState.CellFor(view.Node.Position);
            if (!_exploration.IsExplored(cell))
            {
                (backToPending ??= new List<EntityId>()).Add(id);
                continue;
            }

            view.SetRemembered(IsOutOfSight(view.Node.Position));
        }

        if (backToPending is not null)
        {
            foreach (var id in backToPending)
            {
                var view = _resourceNodeViews[id];
                _pendingResourceNodes[id] = view.Node;
                RemoveResourceNodeView(id);
            }
        }
    }

    private void CreateBuildingView(Entity building)
    {
        var view = new BuildingView(building, _hover, _onBuildingClicked, _onMissedClick)
        {
            Position = WorldSpace.ToRender(building.Position, BuildingView.Size / 2f, _sampleHeight),
        };
        view.SnapRemembered(IsOutOfSight(building.Position));
        _container.AddChild(view);
        _buildingViews[building.Id] = view;
    }

    private void CreateGraveView(Grave grave)
    {
        var view = new GraveView(grave, _onGraveSelected, _onMissedClick)
        {
            Position = WorldSpace.ToRender(grave.Position, GraveView.Size / 2f, _sampleHeight),
        };
        view.SnapRemembered(IsOutOfSight(grave.Position));
        _graveViews[grave.Id] = view;
        _container.AddChild(view);
    }

    private void CreateItemPileView(Entity pile)
    {
        var view = new ItemPileView(pile, _hover, _onItemPileClicked, _onMissedClick)
        {
            Position = WorldSpace.ToRender(pile.Position, ItemPileView.Size / 2f, _sampleHeight),
        };
        view.SnapRemembered(IsOutOfSight(pile.Position));
        _container.AddChild(view);
        _itemPileViews[pile.Id] = view;
    }

    // Driven by WorldState.EntityRemoved, unlike a felled resource or a buried person - a pile
    // shrinks and vanishes from an ordinary command (PickUpItemCommand), not a special one Main
    // has to recognise, so the event is enough.
    private void RemoveItemPileView(EntityId id)
    {
        if (_itemPileViews.TryGetValue(id, out var view))
        {
            view.QueueFree();
            _itemPileViews.Remove(id);
        }
    }
}
