using Godot;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public sealed class WorldPresenter
{
    private readonly Node3D _container;
    private readonly Action<PersonId, MouseButton> _onPersonClicked;
    private readonly Action<ResourceNodeId> _onResourceNodeSelected;
    private readonly Action<GraveId> _onGraveSelected;
    private readonly CollisionObject3D.InputEventEventHandler _onMissedClick;
    private readonly Func<float, float, float> _sampleHeight;
    private readonly ResourceCatalog _resourceCatalog;
    private readonly ExplorationState _exploration;
    private readonly Dictionary<PersonId, PersonView> _personViews = new();
    private readonly Dictionary<ResourceNodeId, ResourceNodeView> _resourceNodeViews = new();

    // Fog of war (todo #13): a node outside anyone's ever-explored area gets no Godot view at
    // all yet, not just a hidden one - creating a ResourceNodeView for all ~17,000+ decoration-
    // turned-resource nodes at once (MapLoader.ScatterDecorations) up front was itself the
    // single biggest chunk of the game's startup time. Kept here until its own cell is explored
    // (see RefreshExploration), then created for real exactly like any other node.
    private readonly Dictionary<ResourceNodeId, ResourceNode> _pendingResourceNodes = new();
    private readonly Dictionary<BuildingId, BuildingView> _buildingViews = new();
    private readonly Dictionary<GraveId, GraveView> _graveViews = new();

    public WorldPresenter(
        Node3D container,
        WorldState world,
        Action<PersonId, MouseButton> onPersonClicked,
        Action<ResourceNodeId> onResourceNodeSelected,
        Action<GraveId> onGraveSelected,
        CollisionObject3D.InputEventEventHandler onMissedClick,
        Func<float, float, float>? sampleHeight = null)
    {
        _container = container;
        _onPersonClicked = onPersonClicked;
        _onResourceNodeSelected = onResourceNodeSelected;
        _onGraveSelected = onGraveSelected;
        _onMissedClick = onMissedClick;
        _sampleHeight = sampleHeight ?? ((x, z) => 0f);
        _resourceCatalog = world.ResourceCatalog;
        _exploration = world.Exploration;

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
            view.SetTargetPosition(ToVector3(position, PersonView.Height / 2f), overSeconds);
        }
    }

    public Vector3? GetPersonGlobalPosition(PersonId id) =>
        _personViews.TryGetValue(id, out var view) ? view.GlobalPosition : null;

    // For Main.cs's screen-space selection marker overlay - the real head height above that
    // position (see PersonView.HeadHeightOffset's own doc comment).
    public float? GetPersonHeadHeightOffset(PersonId id) =>
        _personViews.TryGetValue(id, out var view) ? view.HeadHeightOffset : null;

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
        var view = new PersonView(person.Id, _onPersonClicked, _onMissedClick)
        {
            Name = person.Name,
            Position = ToVector3(person.Position, PersonView.Height / 2f),
        };
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
        var view = new ResourceNodeView(node.Id, node.Kind, canFell, _onResourceNodeSelected, _onMissedClick);
        view.Position = ToVector3(node.Position, view.Size / 2f);
        view.SetRemembered(!_exploration.IsVisible(ExplorationState.CellFor(node.Position)));
        _container.AddChild(view);
        _resourceNodeViews[node.Id] = view;
    }

    // Called once per simulation tick (Main._Process's tick block) - cheap enough at that
    // cadence (a HashSet lookup per pending/created node, not per frame) even at decoration
    // scale. Two jobs: promote any still-pending node whose cell has now been explored to a
    // real view, and keep every already-created view's "remembered" (explored, not currently
    // visible) tint in sync as the group wanders in and out of sight of it.
    public void RefreshExploration()
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

        foreach (var view in _resourceNodeViews.Values)
        {
            var cell = ExplorationState.CellFor(new Position(view.Position.X, view.Position.Z));
            view.SetRemembered(!_exploration.IsVisible(cell));
        }
    }

    private void CreateBuildingView(Building building)
    {
        var view = new BuildingView(building.Id, building.Kind)
        {
            Position = ToVector3(building.Position, BuildingView.Size / 2f),
        };
        _container.AddChild(view);
        _buildingViews[building.Id] = view;
    }

    private void CreateGraveView(Grave grave)
    {
        var view = new GraveView(grave.Id, grave.IsMarked, _onGraveSelected, _onMissedClick)
        {
            Position = ToVector3(grave.Position, GraveView.Size / 2f),
        };
        _container.AddChild(view);
        _graveViews[grave.Id] = view;
    }

    // Position is double (real-world meters, see docs/terrain-and-world-scale-architecture.md);
    // Godot's render space stays float. Safe as long as everything stays within one small local
    // patch - true continent-scale coordinates would need a floating-origin conversion here instead.
    // The ground itself is real terrain (or flat, if no height sampler was given), so the vertical
    // offset is measured from the actual terrain height under (x, z), not from a flat y = 0.
    private Vector3 ToVector3(Position position, float y)
    {
        var x = (float)position.X;
        var z = (float)position.Y;
        return new Vector3(x, _sampleHeight(x, z) + y, z);
    }
}
