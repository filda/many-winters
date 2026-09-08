using Godot;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Views;

public sealed class WorldPresenter
{
    private readonly Node3D _container;
    private readonly Action<Person, MouseButton> _onPersonClicked;
    private readonly Action<ResourceNode> _onResourceNodeSelected;
    private readonly Action<Grave> _onGraveSelected;
    private readonly CollisionObject3D.InputEventEventHandler _onMissedClick;
    private readonly Func<float, float, float> _sampleHeight;
    private readonly ResourceCatalog _resourceCatalog;
    private readonly RevealableExploration _exploration;
    // One cursor, one highlighted thing - and the invariant lives here rather than in each
    // view, which is what stopped highlights getting stuck on (see HoverArbiter).
    private readonly HoverArbiter _hover = new();
    private readonly Dictionary<PersonId, PersonView> _personViews = new();
    private readonly Dictionary<ResourceNodeId, ResourceNodeView> _resourceNodeViews = new();
    private readonly Dictionary<GraveId, GraveView> _graveViews = new();
    private readonly Dictionary<BuildingId, BuildingView> _buildingViews = new();

    // Read every tick by RefreshExploration to ask where each entity currently stands
    // relative to the fog. The world's own live collections, not copies - a view created
    // later (a new grave, someone born) is in here the moment the simulation adds it.
    private readonly IReadOnlyList<Person> _people;
    private readonly IReadOnlyList<Grave> _graves;
    private readonly IReadOnlyList<Building> _buildings;

    // Fog of war: a node outside anyone's ever-explored area gets no Godot view at
    // all yet, not just a hidden one - creating a ResourceNodeView for all ~17,000+ decoration-
    // turned-resource nodes at once (MapLoader.ScatterDecorations) up front was itself the
    // single biggest chunk of the game's startup time. Kept here until its own cell is explored
    // (see RefreshExploration), then created for real exactly like any other node.
    private readonly Dictionary<ResourceNodeId, ResourceNode> _pendingResourceNodes = new();

    public WorldPresenter(
        Node3D container,
        WorldState world,
        RevealableExploration exploration,
        Action<Person, MouseButton> onPersonClicked,
        Action<ResourceNode> onResourceNodeSelected,
        Action<Grave> onGraveSelected,
        CollisionObject3D.InputEventEventHandler onMissedClick,
        Func<float, float, float> sampleHeight)
    {
        _container = container;
        _onPersonClicked = onPersonClicked;
        _onResourceNodeSelected = onResourceNodeSelected;
        _onGraveSelected = onGraveSelected;
        _onMissedClick = onMissedClick;
        _sampleHeight = sampleHeight;
        _resourceCatalog = world.Configuration.ResourceCatalog;
        _exploration = exploration;
        _people = world.People;
        _graves = world.Graves;
        _buildings = world.Buildings;

        world.PersonAdded += CreatePersonView;
        world.ResourceNodeAdded += CreateResourceNodeView;
        world.BuildingAdded += CreateBuildingView;
        world.GraveAdded += CreateGraveView;

        foreach (var person in world.People)
        {
            CreatePersonView(person);
        }

        foreach (var node in world.ResourceNodes)
        {
            CreateResourceNodeView(node);
        }

        foreach (var building in world.Buildings)
        {
            CreateBuildingView(building);
        }

        foreach (var grave in world.Graves)
        {
            CreateGraveView(grave);
        }
    }

    // Every rendered frame (Main._Process), not once per tick: whether the cursor is still on
    // whatever is lit changes continuously, since both the camera and everyone in the world
    // keep moving between ticks.
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

    // For Main.cs's screen-space selection marker overlay - how far above this person's own
    // position the top of their drawn silhouette sits (see SpriteEntityView.TopHeightOffset:
    // content is not necessarily centred in its canvas, so a nominal half-height would float
    // above or sink below a real head depending on the texture's own margins).
    public float? GetPersonHeadHeightOffset(PersonId id) =>
        _personViews.TryGetValue(id, out var view) ? view.TopHeightOffset : null;

    // For Main.cs's occlusion fade, to exclude the selection's own sprites from being
    // treated as blocking the view of themselves.
    public Node3D? GetPersonNode(PersonId id) => _personViews.GetValueOrDefault(id);

    public void RemovePersonView(PersonId id)
    {
        if (_personViews.TryGetValue(id, out var view))
        {
            view.QueueFree();
            _personViews.Remove(id);
        }
    }

    public void SetResourceNodeHasFruit(ResourceNodeId id, bool hasFruit)
    {
        if (_resourceNodeViews.TryGetValue(id, out var view))
        {
            view.SetHasFruit(hasFruit);
        }
    }

    public void RemoveResourceNodeView(ResourceNodeId id)
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
        // Snapped, not faded: whatever the fog does over a view's own spot, it was doing
        // before the view existed, so there is nothing to fade from. Called before the view
        // enters the tree, which is why SnapRemembered may not touch a node of its own.
        view.SnapRemembered(IsOutOfSight(person.Position));
        _container.AddChild(view);
        _personViews[person.Id] = view;
    }

    private void CreateResourceNodeView(ResourceNode node)
    {
        if (!_exploration.IsExplored(ExplorationState.CellFor(node.Position)))
        {
            _pendingResourceNodes[node.Id] = node;
            return;
        }

        CreateResourceNodeViewNow(node);
    }

    private void CreateResourceNodeViewNow(ResourceNode node)
    {
        var canFell = _resourceCatalog.Get(node.Kind).CanFell;
        var view = new ResourceNodeView(node, canFell, _hover, _onResourceNodeSelected, _onMissedClick);
        view.Position = WorldSpace.ToRender(node.Position, view.Size / 2f, _sampleHeight);
        view.SnapRemembered(IsOutOfSight(node.Position));
        _container.AddChild(view);
        _resourceNodeViews[node.Id] = view;
    }

    // Called once per simulation tick (Main._Process's tick block), and again the moment the
    // "Reveal Map" toggle flips - cheap enough at that cadence (a HashSet lookup per
    // pending/created view, not per frame) even at decoration scale, and each view's own
    // early-out means the overwhelming majority of these calls end there (see
    // RememberedFade.Retarget).
    //
    // Every family of view goes through here, not just resource nodes: a grave or a hut the
    // group has walked away from used to stay at full brightness in the middle of sepia
    // trees, and a corpse left where it fell stayed as bright as the living. What each view
    // then does with it - the fade, and how the tint composes with hover or with being dead -
    // is the view's own business.
    public void RefreshExploration()
    {
        RefreshResourceNodeExploration();

        // People are read from the world rather than from _personViews because a person
        // moves: the cell to ask about is wherever they are this tick. For anyone alive the
        // answer is always "in sight" - they are one of the eyes the fog is drawn from - so
        // this only ever really dims the dead.
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

        foreach (var building in _buildings)
        {
            if (_buildingViews.TryGetValue(building.Id, out var buildingView))
            {
                buildingView.SetRemembered(IsOutOfSight(building.Position));
            }
        }
    }

    private bool IsOutOfSight(Position position) =>
        !_exploration.IsVisible(ExplorationState.CellFor(position));

    // The resource nodes' own two extra jobs on top of the tint every view gets: promote any
    // still-pending node whose cell has now been explored to a real view, and send a view
    // whose cell is *not* explored back to pending. That last one only ever happens when the
    // "Reveal Map" toggle is switched off again (ExplorationState itself never un-explores a
    // cell): the fog shaders assume nothing is instantiated under unexplored ground - the
    // boundary is deliberately soft on the unexplored side, and pixels that reconstruct
    // implausibly far are skipped - so a view left standing there showed through as a fogged
    // silhouette instead of disappearing the way it never existed before the reveal. Graves
    // and buildings need none of this: both are built by the group's own hands, so their cell
    // is explored before they exist and stays that way.
    private void RefreshResourceNodeExploration()
    {
        if (_pendingResourceNodes.Count > 0)
        {
            List<ResourceNodeId>? newlyExplored = null;
            foreach (var (id, node) in _pendingResourceNodes)
            {
                if (_exploration.IsExplored(ExplorationState.CellFor(node.Position)))
                {
                    (newlyExplored ??= new List<ResourceNodeId>()).Add(id);
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

        List<ResourceNodeId>? backToPending = null;
        foreach (var (id, view) in _resourceNodeViews)
        {
            var cell = ExplorationState.CellFor(view.Node.Position);
            if (!_exploration.IsExplored(cell))
            {
                (backToPending ??= new List<ResourceNodeId>()).Add(id);
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

    private void CreateBuildingView(Building building)
    {
        var view = new BuildingView(building.Id, building.Kind)
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
}
