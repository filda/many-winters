using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.TestSupport;

// Mirrors the content files under src/ManyWinters.Godot/Content/ so tests exercise the same ids without touching disk.
public static class TestCatalogs
{
    public static readonly ResourceKindId Apple = new("apple");
    public static readonly ResourceKindId Pear = new("pear");
    public static readonly ResourceKindId Mushroom = new("mushroom");
    public static readonly ResourceKindId Potato = new("potato");

    public static readonly SkillTypeId Foraging = new("foraging");
    public static readonly SkillTypeId MushroomForaging = new("mushroom_foraging");
    public static readonly SkillTypeId RootDigging = new("root_digging");

    public static readonly TechniqueId EfficientForaging = new("efficient_foraging");
    public static readonly TechniqueId EfficientMushroomForaging = new("efficient_mushroom_foraging");
    public static readonly TechniqueId EfficientRootDigging = new("efficient_root_digging");

    public static ResourceCatalog CreateResourceCatalog() => new(new[]
    {
        new ResourceDefinition(Apple, "Apple", Foraging),
        new ResourceDefinition(Pear, "Pear", Foraging),
        new ResourceDefinition(Mushroom, "Mushroom", MushroomForaging),
        new ResourceDefinition(Potato, "Potato", RootDigging),
    });

    public static SkillCatalog CreateSkillCatalog() => new(new[]
    {
        new SkillDefinition(Foraging, "Foraging", EfficientForaging),
        new SkillDefinition(MushroomForaging, "Mushroom Foraging", EfficientMushroomForaging),
        new SkillDefinition(RootDigging, "Root Digging", EfficientRootDigging),
    });

    public static WorldState CreateWorld() => new(CreateResourceCatalog(), CreateSkillCatalog());
}
