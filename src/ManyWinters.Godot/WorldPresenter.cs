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
    private readonly Func<float, float, float> _sampleHeight;
    private readonly ResourceCatalog _resourceCatalog;
    private readonly Dictionary<PersonId, PersonView> _personViews = new();
    private readonly Dictionary<ResourceNodeId, ResourceNodeView> _resourceNodeViews = new();
    private readonly Dictionary<BuildingId, BuildingView> _buildingViews = new();
    private readonly Dictionary<GraveId, GraveView> _graveViews = new();
    private PersonId? _selectedPersonId;

    public WorldPresenter(
        Node3D container,
        WorldState world,
        Action<PersonId, MouseButton> onPersonClicked,
        Action<ResourceNodeId> onResourceNodeSelected,
        Action<GraveId> onGraveSelected,
        Func<float, float, float>? sampleHeight = null)
    {
        _container = container;
        _onPersonClicked = onPersonClicked;
        _onResourceNodeSelected = onResourceNodeSelected;
        _onGraveSelected = onGraveSelected;
        _sampleHeight = sampleHeight ?? ((x, z) => 0f);
        _resourceCatalog = world.ResourceCatalog;

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

    public void RemovePersonView(PersonId id)
    {
        if (_personViews.TryGetValue(id, out var view))
        {
            view.QueueFree();
            _personViews.Remove(id);
        }

        if (_selectedPersonId == id)
        {
            _selectedPersonId = null;
        }
    }

    public void SetSelectedPerson(PersonId? id)
    {
        if (_selectedPersonId is { } previousId && _personViews.TryGetValue(previousId, out var previousView))
        {
            previousView.SetSelected(false);
        }

        _selectedPersonId = id;

        if (id is { } newId && _personViews.TryGetValue(newId, out var newView))
        {
            newView.SetSelected(true);
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
        var view = new PersonView(person.Id, _onPersonClicked)
        {
            Name = person.Name,
            Position = ToVector3(person.Position, PersonView.Height / 2f),
        };
        _container.AddChild(view);
        _personViews[person.Id] = view;
    }

    private void CreateResourceNodeView(ResourceNode node)
    {
        var canFell = _resourceCatalog.Get(node.Kind).CanFell;
        var view = new ResourceNodeView(node.Id, node.Kind, canFell, _onResourceNodeSelected);
        view.Position = ToVector3(node.Position, view.Size / 2f);
        _container.AddChild(view);
        _resourceNodeViews[node.Id] = view;
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
        var view = new GraveView(grave.Id, grave.IsMarked, _onGraveSelected)
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
