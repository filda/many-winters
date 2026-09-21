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
    // One cursor, one highlighted thing - the invariant lives here, not in each view.
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
    // creating thousands of decoration views up front was the biggest chunk of startup time.
    // Kept here until its cell is explored. Only Growable entities ever pile up at decoration
    // scale, so only those go through pending.
    //
    // "Explored" never reverts, so once "Reveal Map" or normal play has explored the whole map,
    // that gate alone would build a node for every decoration on it in one pass - see
    // IsWithinViewOfCamera for the second, camera-distance gate that actually bounds how many
    // resource nodes exist at once.
    private readonly Dictionary<EntityId, Entity> _pendingResourceNodes = new();

    // Updated each RefreshExploration call. Simulation space (X, Y on the ground plane), not
    // render space, so comparing against Entity.Position needs no per-node WorldSpace
    // conversion. Radius starts at 0 so nothing is in view before the first update - the
    // constructor passes the camera's actual starting values instead.
    private Position _viewCenter;
    private double _viewRadiusSquared;

    // The radius a decoration must fall back outside of before its view is torn down again -
    // wider than the create radius, so a decoration sitting right at the edge does not flicker
    // in and out as the camera drifts by a meter.
    private const float ViewReleaseRadiusMultiplier = 1.25f;

    public WorldPresenter(
        Node3D container,
        WorldState world,
        RevealableExploration exploration,
        Vector3 initialCameraPosition,
        float initialViewRadius,
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
        _viewCenter = WorldSpace.ToSimulation(initialCameraPosition);
        _viewRadiusSquared = (double)initialViewRadius * initialViewRadius;

        world.PersonAdded += CreatePersonView;
        world.EntityAdded += CreateEntityView;
        world.GraveAdded += CreateGraveView;
        // Only a pile-category entity ever fires this: a felled or withered resource stays in
        // Entities with Growth.IsAlive false instead, and a building is never removed.
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

    // Every rendered frame, not once per tick: camera and people keep moving between ticks, so
    // whether the cursor is still on the lit thing changes continuously.
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
    // the drawn silhouette sits - a nominal half-height would float or sink depending on the
    // texture's own margins.
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
    // carries that in its static type.
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
        if (!IsWithinViewOfCamera(node.Position, _viewRadiusSquared))
        {
            _pendingResourceNodes[node.Id] = node;
            return;
        }

        CreateResourceNodeViewNow(node);
    }

    // Explored (fog of war, never reverts) and close enough to the camera to be worth a node
    // right now.
    private bool IsWithinViewOfCamera(Position position, double radiusSquared)
    {
        if (!_exploration.IsExplored(ExplorationState.CellFor(position)))
        {
            return false;
        }

        var dx = position.X - _viewCenter.X;
        var dy = position.Y - _viewCenter.Y;
        return dx * dx + dy * dy <= radiusSquared;
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

    // Once per simulation tick and when the "Reveal Map" toggle flips - a HashSet lookup per
    // view at that cadence is cheap even at decoration scale, and each view's early-out ends
    // most calls at once. Every family of view goes through here, so a grave, hut or corpse the
    // group walked away from dims with the trees; what the view does with it is its own
    // business.
    //
    // cameraPosition/viewRadius refresh the view-distance gate resource nodes check themselves
    // against - other view families are few enough in practice to skip the same treatment.
    public void RefreshExploration(Vector3 cameraPosition, float viewRadius)
    {
        _viewCenter = WorldSpace.ToSimulation(cameraPosition);
        _viewRadiusSquared = (double)viewRadius * viewRadius;

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

    // Resource nodes' two extra jobs: promote a pending node now explored and in view to a real
    // view, and send a view that fell out of either back to pending. The latter happens both
    // when "Reveal Map" is switched off again (a cell never un-explores: the fog shaders assume
    // nothing is instantiated under unexplored ground, so a view left there shows through as a
    // fogged silhouette) and continuously as the camera moves away from an already-explored
    // decoration. Graves and buildings need neither: built by the group's own hands, their cell
    // is explored before they exist and stays so, and there are never enough of them to threaten
    // node count the way decorations can.
    private void RefreshResourceNodeExploration()
    {
        if (_pendingResourceNodes.Count > 0)
        {
            List<EntityId>? newlyInView = null;
            foreach (var (id, node) in _pendingResourceNodes)
            {
                if (IsWithinViewOfCamera(node.Position, _viewRadiusSquared))
                {
                    (newlyInView ??= new List<EntityId>()).Add(id);
                }
            }

            if (newlyInView is not null)
            {
                foreach (var id in newlyInView)
                {
                    var node = _pendingResourceNodes[id];
                    _pendingResourceNodes.Remove(id);
                    CreateResourceNodeViewNow(node);
                }
            }
        }

        // Wider than the create radius, so a decoration right at the create boundary does not
        // tear its view down again next tick.
        var releaseRadiusSquared = _viewRadiusSquared * ViewReleaseRadiusMultiplier * ViewReleaseRadiusMultiplier;

        List<EntityId>? backToPending = null;
        foreach (var (id, view) in _resourceNodeViews)
        {
            if (!IsWithinViewOfCamera(view.Node.Position, releaseRadiusSquared))
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
    // shrinks and vanishes from an ordinary command, not a special one Main has to recognise, so
    // the event is enough.
    private void RemoveItemPileView(EntityId id)
    {
        if (_itemPileViews.TryGetValue(id, out var view))
        {
            view.QueueFree();
            _itemPileViews.Remove(id);
        }
    }
}
