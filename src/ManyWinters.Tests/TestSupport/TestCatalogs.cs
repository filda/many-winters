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
    public static readonly SkillTypeId Burial = new("burial");

    public static readonly TechniqueId EfficientForaging = new("efficient_foraging");
    public static readonly TechniqueId EfficientMushroomForaging = new("efficient_mushroom_foraging");
    public static readonly TechniqueId EfficientRootDigging = new("efficient_root_digging");
    public static readonly TechniqueId EfficientWoodcutting = new("efficient_woodcutting");
    public static readonly TechniqueId EfficientBurial = new("efficient_burial");

    public static readonly ItemKindId WoodItem = new("wood");
    public static readonly ItemKindId Axe = new("axe");
    public static readonly ItemKindId WarmClothing = new("warm_clothing");
    public static readonly ItemKindId AppleItem = new("apple");
    public static readonly ItemKindId PearItem = new("pear");
    public static readonly ItemKindId MushroomItem = new("mushroom");
    public static readonly ItemKindId PotatoItem = new("potato");

    public const float AxeHarvestBonus = 15f;
    public const int AxeInputAmount = 5;
    public const float WarmClothingInsulation = 1f;
    public const int WarmClothingInputAmount = 10;
    public const float FoodHungerRestoredPerUnit = 1f;
    public const float ItemWeight = 1f;
    public const float AxeWeight = 5f;
    public const float WarmClothingWeight = 3f;

    public static readonly BuildingKindId StorageHut = new("storage_hut");
    public const int StorageHutInputAmount = 20;

    public const float ColdFoodYieldMultiplier = 0.4f;
    public const float FoodRegenPerTick = 1f;
    public const float WoodRegenPerTick = 0.5f;
    public const float FellWoodYield = 30f;

    private static IReadOnlyList<ClimateYield> ColdFoodYield => [new ClimateYield(Climate.Cold, ColdFoodYieldMultiplier)];

    public static ResourceCatalog CreateResourceCatalog() => new(new[]
    {
        new ResourceDefinition(Apple, "Apple", Foraging, AppleItem, ColdFoodYield, FoodRegenPerTick, CanFell: true, FellLeavesKind: Wood, FellLeavesAmount: FellWoodYield),
        new ResourceDefinition(Pear, "Pear", Foraging, PearItem, ColdFoodYield, FoodRegenPerTick, CanFell: true, FellLeavesKind: Wood, FellLeavesAmount: FellWoodYield),
        new ResourceDefinition(Mushroom, "Mushroom", MushroomForaging, MushroomItem, ColdFoodYield, FoodRegenPerTick),
        new ResourceDefinition(Potato, "Potato", RootDigging, PotatoItem, ColdFoodYield, FoodRegenPerTick),
        new ResourceDefinition(Wood, "Wood", Woodcutting, WoodItem, RegenPerTick: WoodRegenPerTick),
    });

    public static SkillCatalog CreateSkillCatalog() => new(new[]
    {
        new SkillDefinition(Foraging, "Foraging", EfficientForaging),
        new SkillDefinition(MushroomForaging, "Mushroom Foraging", EfficientMushroomForaging),
        new SkillDefinition(RootDigging, "Root Digging", EfficientRootDigging),
        new SkillDefinition(Woodcutting, "Woodcutting", EfficientWoodcutting, Axe, AxeHarvestBonus),
        new SkillDefinition(Burial, "Burial", EfficientBurial),
    });

    public static RecipeCatalog CreateRecipeCatalog() => new(new[]
    {
        new RecipeDefinition(Axe, WoodItem, AxeInputAmount),
        new RecipeDefinition(WarmClothing, WoodItem, WarmClothingInputAmount),
    });

    public static BuildingCatalog CreateBuildingCatalog() => new(new[]
    {
        new BuildingDefinition(StorageHut, "Storage Hut", WoodItem, StorageHutInputAmount),
    });

    public static ItemCatalog CreateItemCatalog() => new(new[]
    {
        new ItemDefinition(WarmClothing, "Warm Clothing", WarmClothingInsulation, WarmClothingWeight),
        new ItemDefinition(WoodItem, "Wood", Weight: ItemWeight),
        new ItemDefinition(Axe, "Axe", Weight: AxeWeight),
        new ItemDefinition(AppleItem, "Apple", Weight: ItemWeight, HungerRestoredPerUnit: FoodHungerRestoredPerUnit),
        new ItemDefinition(PearItem, "Pear", Weight: ItemWeight, HungerRestoredPerUnit: FoodHungerRestoredPerUnit),
        new ItemDefinition(MushroomItem, "Mushroom", Weight: ItemWeight, HungerRestoredPerUnit: FoodHungerRestoredPerUnit),
        new ItemDefinition(PotatoItem, "Potato", Weight: ItemWeight, HungerRestoredPerUnit: FoodHungerRestoredPerUnit),
    });

    public static WorldConfiguration CreateConfiguration() => new(
        CreateResourceCatalog(),
        CreateSkillCatalog(),
        CreateRecipeCatalog(),
        CreateBuildingCatalog(),
        CreateItemCatalog(),
        SeasonParameters.Default);

    public static WorldState CreateWorld() => new(CreateConfiguration());
}
