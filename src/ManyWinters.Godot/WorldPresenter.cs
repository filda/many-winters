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
    private readonly Dictionary<PersonId, PersonView> _personViews = new();
    private readonly Dictionary<ResourceNodeId, ResourceNodeView> _resourceNodeViews = new();
    private readonly Dictionary<BuildingId, BuildingView> _buildingViews = new();
    private readonly Dictionary<GraveId, GraveView> _graveViews = new();

    public WorldPresenter(
        Node3D container,
        WorldState world,
        Action<PersonId, MouseButton> onPersonClicked,
        Action<ResourceNodeId> onResourceNodeSelected,
        Action<GraveId> onGraveSelected)
    {
        _container = container;
        _onPersonClicked = onPersonClicked;
        _onResourceNodeSelected = onResourceNodeSelected;
        _onGraveSelected = onGraveSelected;

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

    public void SetPersonPosition(PersonId id, Position position)
    {
        if (_personViews.TryGetValue(id, out var view))
        {
            view.Position = new Vector3(position.X, PersonView.Height / 2f, position.Y);
        }
    }

    public void RemovePersonView(PersonId id)
    {
        if (_personViews.TryGetValue(id, out var view))
        {
            view.QueueFree();
            _personViews.Remove(id);
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
            Position = new Vector3(person.Position.X, PersonView.Height / 2f, person.Position.Y),
        };
        _container.AddChild(view);
        _personViews[person.Id] = view;
    }

    private void CreateResourceNodeView(ResourceNode node)
    {
        var view = new ResourceNodeView(node.Id, node.Kind, _onResourceNodeSelected)
        {
            Position = new Vector3(node.Position.X, ResourceNodeView.Size / 2f, node.Position.Y),
        };
        _container.AddChild(view);
        _resourceNodeViews[node.Id] = view;
    }

    private void CreateBuildingView(Building building)
    {
        var view = new BuildingView(building.Id, building.Kind)
        {
            Position = new Vector3(building.Position.X, BuildingView.Size / 2f, building.Position.Y),
        };
        _container.AddChild(view);
        _buildingViews[building.Id] = view;
    }

    private void CreateGraveView(Grave grave)
    {
        var view = new GraveView(grave.Id, grave.IsMarked, _onGraveSelected)
        {
            Position = new Vector3(grave.Position.X, GraveView.Size / 2f, grave.Position.Y),
        };
        _container.AddChild(view);
        _graveViews[grave.Id] = view;
    }
}
