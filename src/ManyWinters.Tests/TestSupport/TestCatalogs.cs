using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
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
    public static readonly ResourceKindId Wood = new("wood");

    public static readonly SkillTypeId Foraging = new("foraging");
    public static readonly SkillTypeId MushroomForaging = new("mushroom_foraging");
    public static readonly SkillTypeId RootDigging = new("root_digging");
    public static readonly SkillTypeId Woodcutting = new("woodcutting");

    public static readonly TechniqueId EfficientForaging = new("efficient_foraging");
    public static readonly TechniqueId EfficientMushroomForaging = new("efficient_mushroom_foraging");
    public static readonly TechniqueId EfficientRootDigging = new("efficient_root_digging");
    public static readonly TechniqueId EfficientWoodcutting = new("efficient_woodcutting");

    public static readonly ItemKindId WoodItem = new("wood");
    public static readonly ItemKindId Axe = new("axe");

    public const float AxeHarvestBonus = 15f;
    public const int AxeInputAmount = 5;

    public static readonly BuildingKindId StorageHut = new("storage_hut");
    public const int StorageHutInputAmount = 20;

    public const float WinterFoodYieldMultiplier = 0.4f;

    public static ResourceCatalog CreateResourceCatalog() => new(new[]
    {
        new ResourceDefinition(Apple, "Apple", Foraging, WinterYieldMultiplier: WinterFoodYieldMultiplier),
        new ResourceDefinition(Pear, "Pear", Foraging, WinterYieldMultiplier: WinterFoodYieldMultiplier),
        new ResourceDefinition(Mushroom, "Mushroom", MushroomForaging, WinterYieldMultiplier: WinterFoodYieldMultiplier),
        new ResourceDefinition(Potato, "Potato", RootDigging, WinterYieldMultiplier: WinterFoodYieldMultiplier),
        new ResourceDefinition(Wood, "Wood", Woodcutting, WoodItem),
    });

    public static SkillCatalog CreateSkillCatalog() => new(new[]
    {
        new SkillDefinition(Foraging, "Foraging", EfficientForaging),
        new SkillDefinition(MushroomForaging, "Mushroom Foraging", EfficientMushroomForaging),
        new SkillDefinition(RootDigging, "Root Digging", EfficientRootDigging),
        new SkillDefinition(Woodcutting, "Woodcutting", EfficientWoodcutting, Axe, AxeHarvestBonus),
    });

    public static RecipeCatalog CreateRecipeCatalog() => new(new[]
    {
        new RecipeDefinition(Axe, WoodItem, AxeInputAmount),
    });

    public static BuildingCatalog CreateBuildingCatalog() => new(new[]
    {
        new BuildingDefinition(StorageHut, "Storage Hut", WoodItem, StorageHutInputAmount),
    });

    public static WorldState CreateWorld() => new(
        CreateResourceCatalog(),
        CreateSkillCatalog(),
        CreateRecipeCatalog(),
        CreateBuildingCatalog());
}
