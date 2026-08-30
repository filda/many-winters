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
    public static readonly ResourceKindId Grass = new("grass");

    // Former terrain decoration, now real ResourceNodes (MapLoader.ScatterDecorations).
    public static readonly ResourceKindId ConiferTree = new("conifer_tree");
    public static readonly ResourceKindId DeciduousTree = new("deciduous_tree");
    public static readonly ResourceKindId Bush = new("bush");
    public static readonly ResourceKindId Flower = new("flower");
    public static readonly ResourceKindId Fern = new("fern");
    public static readonly ResourceKindId RockPile = new("rock_pile");
    public static readonly ResourceKindId RockBoulder = new("rock_boulder");
    public static readonly ResourceKindId RockCluster = new("rock_cluster");
    public static readonly ResourceKindId TreeStump = new("tree_stump");
    public static readonly ResourceKindId FallenLog = new("fallen_log");

    public static readonly SkillTypeId Foraging = new("foraging");
    public static readonly SkillTypeId MushroomForaging = new("mushroom_foraging");
    public static readonly SkillTypeId RootDigging = new("root_digging");
    public static readonly SkillTypeId Woodcutting = new("woodcutting");
    public static readonly SkillTypeId Mining = new("mining");
    public static readonly SkillTypeId Burial = new("burial");

    public static readonly TechniqueId EfficientForaging = new("efficient_foraging");
    public static readonly TechniqueId EfficientMushroomForaging = new("efficient_mushroom_foraging");
    public static readonly TechniqueId EfficientRootDigging = new("efficient_root_digging");
    public static readonly TechniqueId EfficientWoodcutting = new("efficient_woodcutting");
    public static readonly TechniqueId EfficientMining = new("efficient_mining");
    public static readonly TechniqueId EfficientBurial = new("efficient_burial");

    public static readonly ItemKindId WoodItem = new("wood");
    public static readonly ItemKindId Axe = new("axe");
    public static readonly ItemKindId WarmClothing = new("warm_clothing");
    public static readonly ItemKindId AppleItem = new("apple");
    public static readonly ItemKindId PearItem = new("pear");
    public static readonly ItemKindId MushroomItem = new("mushroom");
    public static readonly ItemKindId PotatoItem = new("potato");
    public static readonly ItemKindId GrassItem = new("grass");
    public static readonly ItemKindId StoneItem = new("stone");
    public static readonly ItemKindId Basket = new("basket");
    public static readonly ItemKindId Bag = new("bag");

    public const float AxeHarvestBonus = 15f;
    public const int AxeInputAmount = 5;
    public const float WarmClothingInsulation = 1f;
    public const int WarmClothingInputAmount = 10;
    public const float FoodHungerRestoredPerUnit = 1f;
    public const float ItemWeight = 1f;
    public const float StoneWeight = 2f;
    public const float AxeWeight = 5f;
    public const float WarmClothingWeight = 3f;

    // Basket (wood, carried on the back) and bag (grass, lighter but holds less) - see
    // WorldState.MaxCarryWeightFor for how CarryCapacityBonus is applied.
    public const int BasketInputAmount = 8;
    public const float BasketWeight = 2f;
    public const float BasketCarryCapacityBonus = 20f;
    public const int BagInputAmount = 10;
    public const float BagWeight = 1f;
    public const float BagCarryCapacityBonus = 10f;
    public const float GrassRegenPerTick = 1f;

    public static readonly BuildingKindId StorageHut = new("storage_hut");
    public const int StorageHutInputAmount = 20;

    public const float ColdFoodYieldMultiplier = 0.4f;
    public const float FoodRegenPerTick = 1f;
    public const float WoodRegenPerTick = 0.5f;
    public const float FellWoodYield = 30f;

    // Former terrain decoration (see ConiferTree etc. above) - regenPerTick 0 for rocks/dead
    // wood (finite, never regrow) mirrors Content/resources/{kind}/{kind}.json exactly.
    public const float DecorationWoodRegenPerTick = 0.5f;
    public const float DecorationGroundCoverRegenPerTick = 1f;

    // Carry capacity (see CarryCapacity.BaseWeightFor) ramps up with age - most command tests
    // don't care about age at all, so they add people old enough to already be at the full
    // adult baseline rather than a newborn's reduced one.
    public const long AdultAgeTicks = WorldState.TicksPerYear * 4;

    private static IReadOnlyList<ClimateYield> ColdFoodYield => [new ClimateYield(Climate.Cold, ColdFoodYieldMultiplier)];

    public static ResourceCatalog CreateResourceCatalog() => new(new[]
    {
        new ResourceDefinition(Apple, "Apple", Foraging, AppleItem, ColdFoodYield, FoodRegenPerTick, CanFell: true, FellLeavesKind: Wood, FellLeavesAmount: FellWoodYield),
        new ResourceDefinition(Pear, "Pear", Foraging, PearItem, ColdFoodYield, FoodRegenPerTick, CanFell: true, FellLeavesKind: Wood, FellLeavesAmount: FellWoodYield),
        new ResourceDefinition(Mushroom, "Mushroom", MushroomForaging, MushroomItem, ColdFoodYield, FoodRegenPerTick),
        new ResourceDefinition(Potato, "Potato", RootDigging, PotatoItem, ColdFoodYield, FoodRegenPerTick),
        new ResourceDefinition(Wood, "Wood", Woodcutting, WoodItem, RegenPerTick: WoodRegenPerTick),
        new ResourceDefinition(Grass, "Wild Grass", Foraging, GrassItem, RegenPerTick: GrassRegenPerTick),
        new ResourceDefinition(ConiferTree, "Conifer Tree", Woodcutting, WoodItem, RegenPerTick: DecorationWoodRegenPerTick),
        new ResourceDefinition(DeciduousTree, "Deciduous Tree", Woodcutting, WoodItem, RegenPerTick: DecorationWoodRegenPerTick),
        new ResourceDefinition(Bush, "Bush", Woodcutting, WoodItem, RegenPerTick: DecorationWoodRegenPerTick),
        new ResourceDefinition(Flower, "Flower", Foraging, GrassItem, RegenPerTick: DecorationGroundCoverRegenPerTick),
        new ResourceDefinition(Fern, "Fern", Foraging, GrassItem, RegenPerTick: DecorationGroundCoverRegenPerTick),
        new ResourceDefinition(RockPile, "Rock Pile", Mining, StoneItem),
        new ResourceDefinition(RockBoulder, "Rock Boulder", Mining, StoneItem),
        new ResourceDefinition(RockCluster, "Rock Cluster", Mining, StoneItem),
        new ResourceDefinition(TreeStump, "Tree Stump", Woodcutting, WoodItem),
        new ResourceDefinition(FallenLog, "Fallen Log", Woodcutting, WoodItem),
    });

    public static SkillCatalog CreateSkillCatalog() => new(new[]
    {
        new SkillDefinition(Foraging, "Foraging", EfficientForaging),
        new SkillDefinition(MushroomForaging, "Mushroom Foraging", EfficientMushroomForaging),
        new SkillDefinition(RootDigging, "Root Digging", EfficientRootDigging),
        new SkillDefinition(Woodcutting, "Woodcutting", EfficientWoodcutting, Axe, AxeHarvestBonus),
        new SkillDefinition(Mining, "Mining", EfficientMining),
        new SkillDefinition(Burial, "Burial", EfficientBurial),
    });

    public static RecipeCatalog CreateRecipeCatalog() => new(new[]
    {
        new RecipeDefinition(Axe, WoodItem, AxeInputAmount),
        new RecipeDefinition(WarmClothing, WoodItem, WarmClothingInputAmount),
        new RecipeDefinition(Basket, WoodItem, BasketInputAmount),
        new RecipeDefinition(Bag, GrassItem, BagInputAmount),
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
        new ItemDefinition(GrassItem, "Grass", Weight: ItemWeight),
        new ItemDefinition(StoneItem, "Stone", Weight: StoneWeight),
        new ItemDefinition(Basket, "Basket", Weight: BasketWeight, CarryCapacityBonus: BasketCarryCapacityBonus),
        new ItemDefinition(Bag, "Bag", Weight: BagWeight, CarryCapacityBonus: BagCarryCapacityBonus),
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
