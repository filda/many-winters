using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.TestSupport;

// Mirrors src/ManyWinters.Godot/Content/ so tests use the same ids without touching disk.
public static class TestCatalogs
{
    public const int AxeInputAmount = 5;
    public const int GrassPerCord = 3;

    // One lump makes one wedge: knapping takes a stone whole rather than a handful, the way
    // twisting takes several blades of grass.
    public const int StonePerWedge = 1;
    // Two hide - a deer gives one, so it takes two deer to clothe one person.
    public const int WarmClothingInputAmount = 2;

    // Mirrors the shelf life each perishable material's json carries. Hide (tanned), bone, wood,
    // stone and plant_fibre have none - rawhide is what spoils, hide is what tanning turns it into.
    public const long MeatShelfLifeTicks = 30;
    public const long RawhideShelfLifeTicks = 75;
    public const long AppleShelfLifeTicks = 150;
    public const int SinewPerCord = 3;
    public const int StorageHutInputAmount = 20;

    public const float ColdFoodYieldMultiplier = 0.4f;
    public const float FoodRegenPerTick = 1f;
    public const float FellWoodYield = 30f;

    // Mirrors Content/species/human/human.json: the one species every test world defines.
    public const long WeaningAgeYears = 1;
    public const long AdultAgeYears = 4;
    public const long ElderAgeYears = 7;

    public const float HumanCollisionRadius = 0.35f;
    public const float HumanHungerPerTickMultiplier = 1f;

    public const float DeerCollisionRadius = 0.6f;
    public const int DeerHerdMinSize = 6;
    public const int DeerHerdMaxSize = 10;

    // The shipped map's winter reserve: the largest multiplier at which the same herd, run
    // through the shipped map's own year, still ends up at or above where it started (start 17,
    // 5 births, end 18 at 0.28; end 16 already at 0.29 - a sharp cutoff, not a knife-edge value,
    // since 0.1 through 0.28 all land on the same 18).
    public const float DeerHungerPerTickMultiplier = 0.28f;

    // Faster than a person's fastest walk (a player-directed move is 1f per tick); a deer clearly
    // outrunning that on FleeDistance/SafeDistance mirrors deer.json.
    public const float DeerFleeDistance = 8f;

    // Mirrors deer.json's carcass block: what ButcherCommand finds in a dead deer's Inventory.
    public const int DeerCarcassMeat = 30;
    public const int DeerCarcassHide = 1;
    public const int DeerCarcassBone = 4;
    public const int DeerCarcassSinew = 2;

    public static readonly EntityKindId Apple = new("apple");
    public static readonly EntityKindId Pear = new("pear");
    public static readonly EntityKindId Mushroom = new("mushroom");
    public static readonly EntityKindId Potato = new("potato");
    public static readonly EntityKindId Wood = new("wood");
    public static readonly EntityKindId Grass = new("grass");

    // Scattered across the map as decoration rather than placed individually.
    public static readonly EntityKindId ConiferTree = new("conifer_tree");
    public static readonly EntityKindId DeciduousTree = new("deciduous_tree");
    public static readonly EntityKindId Bush = new("bush");
    public static readonly EntityKindId Flower = new("flower");
    public static readonly EntityKindId Fern = new("fern");
    public static readonly EntityKindId RockPile = new("rock_pile");
    public static readonly EntityKindId RockBoulder = new("rock_boulder");
    public static readonly EntityKindId RockCluster = new("rock_cluster");
    public static readonly EntityKindId TreeStump = new("tree_stump");
    public static readonly EntityKindId FallenLog = new("fallen_log");

    public static readonly SkillTypeId Foraging = new("foraging");
    public static readonly SkillTypeId MushroomForaging = new("mushroom_foraging");
    public static readonly SkillTypeId Woodcutting = new("woodcutting");
    public static readonly SkillTypeId Burial = new("burial");

    // Never self-taught: only GrantTechniqueCommand or TeachCommand ever puts one of these into a
    // person's KnownTechniques.
    public static readonly TechniqueId BasicForaging = new("basic_foraging");
    public static readonly TechniqueId BasicMushroomForaging = new("basic_mushroom_foraging");
    public static readonly TechniqueId BasicWoodcutting = new("basic_woodcutting");
    public static readonly TechniqueId BasicTwisting = new("basic_twisting");
    public static readonly TechniqueId BasicBinding = new("basic_binding");
    public static readonly TechniqueId BasicKnapping = new("basic_knapping");
    public static readonly TechniqueId BasicTanning = new("basic_tanning");
    public static readonly TechniqueId BasicSharpening = new("basic_sharpening");
    public static readonly TechniqueId BasicMining = new("basic_mining");
    public static readonly TechniqueId BasicEating = new("basic_eating");
    public static readonly TechniqueId BasicTeaching = new("basic_teaching");
    public static readonly TechniqueId BasicButchering = new("basic_butchering");
    public static readonly TechniqueId BasicHunting = new("basic_hunting");

    public static readonly TechniqueId EfficientForaging = new("efficient_foraging");
    public static readonly TechniqueId EfficientMushroomForaging = new("efficient_mushroom_foraging");
    public static readonly TechniqueId EfficientWoodcutting = new("efficient_woodcutting");
    public static readonly TechniqueId EfficientBurial = new("efficient_burial");
    public static readonly TechniqueId EfficientEating = new("efficient_eating");
    public static readonly TechniqueId EfficientTeaching = new("efficient_teaching");
    public static readonly TechniqueId EfficientButchering = new("efficient_butchering");
    public static readonly TechniqueId EfficientHunting = new("efficient_hunting");

    public static readonly ItemKindId WoodItem = new("wood");
    public static readonly ItemKindId Axe = new("axe");
    public static readonly ItemKindId WarmClothing = new("warm_clothing");
    public static readonly ItemKindId RawhideClothing = new("rawhide_clothing");
    public static readonly ItemKindId AppleItem = new("apple");
    public static readonly ItemKindId GrassItem = new("grass");
    public static readonly ItemKindId StoneItem = new("stone");
    public static readonly ItemKindId StorageHutItem = new("storage_hut");

    // A dead animal's carcass: one item kind per thing ButcherCommand takes off it.
    public static readonly ItemKindId MeatItem = new("meat");
    public static readonly ItemKindId HideItem = new("hide");
    // Raw off the animal, not the tanned hide warm_clothing is made from - the only one of the
    // two that spoils.
    public static readonly ItemKindId RawhideItem = new("rawhide");
    public static readonly ItemKindId BoneItem = new("bone");
    public static readonly ItemKindId SinewItem = new("sinew");
    public static readonly FormId Wedge = new("wedge");
    public static readonly FormId Cord = new("cord");

    public static readonly EntityKindId StorageHut = new("storage_hut");

    // Carry capacity ramps up with age; command tests that don't care about age spawn people
    // already at the adult baseline.
    public static readonly long AdultAgeTicks = SimulationRules.Default.TicksPerYear * 4;

    public static readonly LifeCycle HumanLifeCycle = new(WeaningAgeYears, AdultAgeYears, ElderAgeYears, HumanMaxLifespanYears);

    public static readonly LifeCycle DeerLifeCycle = new(DeerWeaningAgeYears, DeerAdultAgeYears, DeerElderAgeYears, DeerMaxLifespanYears);

    public static readonly SpeciesId DeerSpeciesId = new("deer");
    // One rawhide cures into one hide - curing changes the substance, not the amount.
    private const int RawhidePerHide = 1;
    private const float FoodHungerRestoredPerUnit = 1f;

    // Mirrors Content/materials/{id}/{id}.json. Weight is density times volume.
    private const float WoodDensity = 0.5f;
    private const float WoodHardness = 0.4f;
    private const float WoodToughness = 0.7f;
    private const float WoodFlexibility = 0.35f;
    private const float WoodFibrousness = 0.5f;
    private const float StoneDensity = 2f;
    private const float StoneHardness = 1f;
    // Hard and brittle is what makes stone knappable; 0.15 rather than 0 avoids reading as
    // unspecified to MaterialWords while still counting as brittle for CanKnap.
    private const float StoneToughness = 0.15f;
    private const float PlantFibreDensity = 0.2f;
    private const float PlantFibreToughness = 0.5f;
    private const float PlantFibreFlexibility = 0.7f;
    private const float PlantFibreFibrousness = 0.9f;
    private const float HideDensity = 0.75f;
    private const float HideToughness = 0.6f;
    private const float HideFlexibility = 0.8f;
    private const float HideElasticity = 0.15f;
    private const float HideFibrousness = 0.4f;
    private const float FoodDensity = 1f;
    private const float HideInsulation = 1f;
    private const long PearShelfLifeTicks = 150;
    private const long MushroomShelfLifeTicks = 40;
    private const long PotatoShelfLifeTicks = 300;

    // Mirrors grass.json's hungerRestoredPerUnit: nutrition harmless to a human, whose diet has
    // no plant_fibre entry, but what lets a deer's diet (above) restore hunger from it.
    private const float GrassHungerRestoredPerUnit = 0.5f;

    // Mirrors meat.json: meat is roughly five times as calorie-dense as fruit (apple/pear/potato/
    // mushroom all restore FoodHungerRestoredPerUnit=1), the same order of magnitude real meat
    // and fruit differ by. A typical meal (eating from the pack starts at hunger 25 and eats down
    // to zero) needs 25 apple units but only 5 meat units, so a deer's carcass (30 meat) covers
    // about six meals - enough for a shipped band, not just one person.
    private const float MeatHungerRestoredPerUnit = 5f;

    // Mirrors bone.json: hard and tough like stone (StoneHardness=1, StoneToughness=0.15 above)
    // but lighter, and too tough to fracture into an edge (CanKnap needs Toughness < 0.3) -
    // not-knappable and not-sharpenable purely from these numbers, no special case needed.
    private const float BoneDensity = 1.3f;
    private const float BoneHardness = 0.75f;
    private const float BoneToughness = 0.6f;
    private const float BoneFlexibility = 0.1f;
    private const float BoneElasticity = 0.05f;
    private const float BoneFibrousness = 0.1f;

    // Mirrors sinew.json: fibrous and flexible enough to twist (CanTwist needs Fibrousness > 0.5
    // and Flexibility > 0.4) and elastic enough to hold tension (HoldsTension needs Elasticity >
    // 0.6) - the bow/snare material the crafting doc names.
    private const float SinewDensity = 0.9f;
    private const float SinewToughness = 0.5f;
    private const float SinewFlexibility = 0.6f;
    private const float SinewElasticity = 0.7f;
    private const float SinewFibrousness = 0.85f;

    private const float FoodVolume = 1f;
    private const float WoodVolume = 2f;
    private const float StoneVolume = 1f;
    private const float GrassVolume = 5f;
    private const float AxeVolume = 2.5f;
    private const float WarmClothingVolume = 4f;
    private const float HideVolume = 3f;
    private const float BoneVolume = 1f;
    private const float SinewVolume = 1f;

    // Basket (wood) and bag (grass, lighter but holds less); CarryCapacityBonus adds to how much
    // a person can carry.
    private const int BasketInputAmount = 8;
    private const float BasketVolume = 4f;
    private const float BasketCarryCapacityBonus = 20f;
    private const int BagInputAmount = 10;
    private const float BagVolume = 5f;
    private const float BagCarryCapacityBonus = 10f;
    private const float GrassRegenPerTick = 1f;
    // Deliberately far beyond any realistic carry capacity: what routes it into the world
    // instead of the maker's pack.
    private const float StorageHutVolume = 200f;
    private const float WoodRegenPerTick = 0.5f;

    // Mirrors Content/resources/{kind}/{kind}.json. Rocks, stumps and logs get no regen: finite,
    // never regrow.
    private const float DecorationWoodRegenPerTick = 0.5f;
    private const float DecorationGroundCoverRegenPerTick = 1f;

    // Felling a forest tree leaves a stump (some wood, never regrows) and a fallen log (the bulk
    // of the trunk, more than one load); felling a bush leaves an ordinary wood pile.
    private const float FellTreeStumpYield = 20f;
    private const float FellLogYield = 40f;
    private const float FellBushWoodYield = 30f;

    // Mirrors collisionRadius in Content/resources/{kind}/{kind}.json; deliberately independent of
    // the sprite's height.
    private const float FruitTreeCollisionRadius = 0.35f;
    private const float BushCollisionRadius = 0.3f;
    private const float ForestTreeCollisionRadius = 0.4f;
    private const float RockPileCollisionRadius = 0.3f;
    private const float RockClusterCollisionRadius = 0.45f;
    private const float RockBoulderCollisionRadius = 0.6f;
    private const long HumanMaxLifespanYears = 10;

    // Mirrors Content/species/deer/deer.json: only defined when a test opts into having deer, so
    // every test that doesn't care about animals keeps the human-only catalog.
    private const long DeerWeaningAgeYears = 1;
    private const long DeerAdultAgeYears = 2;
    private const long DeerElderAgeYears = 6;
    private const long DeerMaxLifespanYears = 8;
    private const float DeerHerdHomeRadius = 15f;
    private const float DeerHerdDriftMetresPerSeason = 20f;

    // Mirrors deer.json's breeding block: mates in Mild (Spring and Autumn in the shipped
    // calendar), carries for two seasons, and must be under 40 hunger to count as eligible.
    private const long DeerGestationTicks = 150;
    private const float DeerConceptionChancePerTick = 0.02f;
    private const float DeerSatietyHungerBelow = 40f;
    private const float DeerSafeDistance = 16f;
    private const float DeerFleeSpeedPerTick = 0.6f;

    // Mirrors Content/forms/{id}/{id}.json: only a wedge presents an edge, which is what keeps a
    // raw lump of the same stone from scoring as a tool.
    private const float WedgeEdgeSharpness = 1f;
    private const float CordLashingStrength = 1f;

    // A shaft doubles the blow of what is lashed to its end.
    private const float StickHaftLeverage = 1f;
    private static readonly SkillTypeId RootDigging = new("root_digging");
    private static readonly SkillTypeId Twisting = new("twisting");
    private static readonly SkillTypeId Binding = new("binding");
    private static readonly SkillTypeId Knapping = new("knapping");
    private static readonly SkillTypeId Tanning = new("tanning");
    private static readonly SkillTypeId Sharpening = new("sharpening");
    private static readonly SkillTypeId Mining = new("mining");
    private static readonly SkillTypeId Butchering = new("butchering");
    private static readonly SkillTypeId Hunting = new("hunting");

    // Nobody is born knowing how to eat or teach either.
    private static readonly SkillTypeId Eating = new("eating");
    private static readonly SkillTypeId Teaching = new("teaching");
    private static readonly TechniqueId BasicRootDigging = new("basic_root_digging");
    private static readonly TechniqueId BasicBurial = new("basic_burial");
    private static readonly TechniqueId EfficientRootDigging = new("efficient_root_digging");
    private static readonly TechniqueId EfficientTwisting = new("efficient_twisting");
    private static readonly TechniqueId EfficientBinding = new("efficient_binding");
    private static readonly TechniqueId EfficientKnapping = new("efficient_knapping");
    private static readonly TechniqueId EfficientTanning = new("efficient_tanning");
    private static readonly TechniqueId EfficientSharpening = new("efficient_sharpening");
    private static readonly TechniqueId EfficientMining = new("efficient_mining");
    private static readonly ItemKindId PearItem = new("pear");
    private static readonly ItemKindId MushroomItem = new("mushroom");
    private static readonly ItemKindId PotatoItem = new("potato");
    private static readonly ItemKindId Basket = new("basket");
    private static readonly ItemKindId Bag = new("bag");

    private static readonly MaterialId WoodMaterial = new("wood");
    private static readonly MaterialId StoneMaterial = new("stone");
    private static readonly MaterialId PlantFibreMaterial = new("plant_fibre");
    private static readonly MaterialId HideMaterial = new("hide");
    private static readonly MaterialId RawhideMaterial = new("rawhide");
    private static readonly MaterialId AppleMaterial = new("apple");
    private static readonly MaterialId PearMaterial = new("pear");
    private static readonly MaterialId PotatoMaterial = new("potato");
    private static readonly MaterialId MushroomMaterial = new("mushroom");
    private static readonly MaterialId MeatMaterial = new("meat");
    private static readonly MaterialId BoneMaterial = new("bone");
    private static readonly MaterialId SinewMaterial = new("sinew");

    private static readonly FormId Whole = new("whole");
    private static readonly FormId Stick = new("stick");
    private static readonly FormId Lump = new("lump");
    private static readonly FormId Fibre = new("fibre");
    private static readonly FormId Vessel = new("vessel");
    private static readonly FormId Garment = new("garment");
    private static readonly FormId Shelter = new("shelter");
    private static readonly TechniqueId TwistVerb = new("twist");
    private static readonly TechniqueId KnapVerb = new("knap");
    private static readonly TechniqueId TanVerb = new("tan");

    // Mirrors Content/species/human/human.json's diet: every material whose item has a
    // HungerRestoredPerUnit above zero, at digestibility 1 - exactly what is edible, so no test's
    // behaviour changes.
    private static readonly IReadOnlyList<SpeciesDefinition.DietEntry> HumanDiet =
    [
        new(AppleMaterial, 1f),
        new(PearMaterial, 1f),
        new(MushroomMaterial, 1f),
        new(PotatoMaterial, 1f),
        new(MeatMaterial, 1f),
    ];

    private static readonly SpeciesDefinition HumanSpecies = new(Person.HumanSpecies, "Human", HumanLifeCycle, HumanDiet)
    {
        CollisionRadius = HumanCollisionRadius,
        HungerPerTickMultiplier = HumanHungerPerTickMultiplier,
    };

    private static readonly IReadOnlyList<SpeciesDefinition.DietEntry> DeerDiet =
    [
        new(PlantFibreMaterial, 1f),
        new(AppleMaterial, 1f),
    ];

    private static readonly IReadOnlyList<SpeciesDefinition.CarcassYield> DeerCarcass =
    [
        new(MeatItem, DeerCarcassMeat),
        new(RawhideItem, DeerCarcassHide),
        new(BoneItem, DeerCarcassBone),
        new(SinewItem, DeerCarcassSinew),
    ];

    private static readonly SpeciesDefinition DeerSpecies = new(
        DeerSpeciesId,
        "Deer",
        DeerLifeCycle,
        DeerDiet,
        InnateTechniques: [BasicEating, BasicForaging],
        CanCarry: false,
        Herd: new SpeciesDefinition.HerdDefinition(DeerHerdMinSize, DeerHerdMaxSize, DeerHerdHomeRadius, DeerHerdDriftMetresPerSeason),
        Breeding: new SpeciesDefinition.BreedingDefinition(Climate.Mild, DeerGestationTicks, DeerConceptionChancePerTick, DeerSatietyHungerBelow),
        Flee: new SpeciesDefinition.FleeDefinition(DeerFleeDistance, DeerSafeDistance, DeerFleeSpeedPerTick),
        Carcass: DeerCarcass)
    {
        CollisionRadius = DeerCollisionRadius,
        HungerPerTickMultiplier = DeerHungerPerTickMultiplier,
    };

    private static IReadOnlyList<ClimateYield> ColdFoodYield => [new ClimateYield(Climate.Cold, ColdFoodYieldMultiplier)];

    public static WorldConfiguration CreateConfiguration()
    {
        var materials = CreateMaterialCatalog();
        var forms = CreateFormCatalog();

        return new(
            CreateSpeciesCatalog(HumanSpecies),
            CreateResourceCatalog(),
            CreateSkillCatalog(),
            CreateRecipeCatalog(),
            materials,
            forms,
            CreateItemCatalog(materials, forms),
            SeasonParameters.Default,
            SimulationRules.Default);
    }

    public static WorldState CreateWorld() => new(CreateConfiguration());

    // Every person's max hunger comes out exactly the same: the shipped game draws one per
    // person, so a test pinning an exact tick of death would otherwise assert against a draw.
    // Tests about the spread use CreateWorld.
    public static WorldConfiguration CreateConfigurationWithoutHungerVariation() =>
        CreateConfiguration() with { Rules = SimulationRules.Default with { MaxHungerVariation = 0f } };

    public static WorldState CreateWorldWithoutHungerVariation() => new(CreateConfigurationWithoutHungerVariation());

    // For tests that shrink a lifespan to make old age arrive after a handful of ticks instead of
    // ten winters. MaxLifespanYears lives on the species' LifeCycle, not SimulationRules. Keeps
    // the standard diet - only the life cycle differs.
    public static WorldConfiguration CreateConfigurationWithLifeCycle(LifeCycle humanLifeCycle) =>
        CreateConfigurationWithSpecies(HumanSpecies with { LifeCycle = humanLifeCycle });

    // For tests that need a human species with a diet (or anything else about the species) other
    // than the standard one - e.g. a species that cannot digest the apple's material, to prove
    // EatCommand consults it.
    public static WorldConfiguration CreateConfigurationWithSpecies(SpeciesDefinition humanSpecies) =>
        CreateConfiguration() with { SpeciesCatalog = CreateSpeciesCatalog(humanSpecies) };

    // For tests about Animal/HomeRange/SpawnAnimalCommand and MapLoader's starting herds: the
    // human catalog plus the one deer species above.
    public static WorldConfiguration CreateConfigurationWithDeer() =>
        CreateConfiguration() with { SpeciesCatalog = CreateSpeciesCatalog(HumanSpecies, DeerSpecies) };

    public static WorldState CreateWorldWithDeer() => new(CreateConfigurationWithDeer());

    public static WorldState CreateWorldWithShortCorpseDecay(long corpseDecayTicks, long bonesLingerTicks) =>
        new(CreateConfigurationWithShortCorpseDecay(corpseDecayTicks, bonesLingerTicks));

    // For tests about animal breeding that need a chance, gestation or satiety threshold other
    // than the shipped deer.json's, so a condition can be proven with a handful of ticks instead
    // of replaying the real numbers.
    public static WorldConfiguration CreateConfigurationWithDeerBreeding(SpeciesDefinition.BreedingDefinition breeding) =>
        CreateConfiguration() with { SpeciesCatalog = CreateSpeciesCatalog(HumanSpecies, DeerSpecies with { Breeding = breeding }) };

    // The shipped axe-grade sharp hafted tool's arithmetic is pinned against the hunting
    // hit-chance-per-tool-score constant: a knapped wedge lashed to a stick, both practised to
    // mastery (skill level 50 makes Practised 1). Built directly from the parts rather than by
    // executing Knap/Twist/Bind, so a test can pin its exact chopping score without depending on
    // those commands' own dice.
    //
    // ChoppingScoreOf works out to EdgeSharpness(Wedge=1) * Hardness(Stone=1) * sqrt(weight
    // density(Stone=2)*volume(1)=2) * (1 + HaftLeverage(Stick=1) * JointStrength(0.5)) ~= 2.121 -
    // the haft side scores nothing on its own (Stick has no EdgeSharpness), so the max in the
    // chopping-score formula always picks the head's own reading.
    public static Assembly CreateTestAxe(WorldState world)
    {
        var itemCatalog = world.Configuration.ItemCatalog;
        var axe = itemCatalog.Get(Axe);
        var wood = itemCatalog.Get(WoodItem);

        var head = new Assembly.Part(axe.Material, axe.Form, Quality: 1f, Volume: StonePerWedge * StoneVolume);
        var haft = new Assembly.Part(wood.Material, wood.Form, Quality: 1f, Volume: WoodVolume);

        return new Assembly.Joined(JointStrength: 0.5f, JointWeight: 0f, head, haft);
    }

    private static SpeciesCatalog CreateSpeciesCatalog(SpeciesDefinition humanSpecies, SpeciesDefinition? deerSpecies = null) =>
        deerSpecies is null ? new([humanSpecies]) : new([humanSpecies, deerSpecies]);

    private static ResourceCatalog CreateResourceCatalog() => new(new[]
    {
        new ResourceDefinition(Apple, "Apple", Foraging, AppleItem, ColdFoodYield, FoodRegenPerTick, CanFell: true, FellLeaves: [new(Wood, FellWoodYield)], CollisionRadius: FruitTreeCollisionRadius),
        new ResourceDefinition(Pear, "Pear", Foraging, PearItem, ColdFoodYield, FoodRegenPerTick, CanFell: true, FellLeaves: [new(Wood, FellWoodYield)], CollisionRadius: FruitTreeCollisionRadius),
        new ResourceDefinition(Mushroom, "Mushroom", MushroomForaging, MushroomItem, ColdFoodYield, FoodRegenPerTick),
        new ResourceDefinition(Potato, "Potato", RootDigging, PotatoItem, ColdFoodYield, FoodRegenPerTick),
        new ResourceDefinition(Wood, "Wood", Woodcutting, WoodItem, RegenPerTick: WoodRegenPerTick),
        new ResourceDefinition(Grass, "Wild Grass", Foraging, GrassItem, RegenPerTick: GrassRegenPerTick),
        new ResourceDefinition(ConiferTree, "Conifer Tree", Woodcutting, WoodItem, RegenPerTick: DecorationWoodRegenPerTick, CanFell: true, FellLeaves: [new(TreeStump, FellTreeStumpYield), new(FallenLog, FellLogYield)], RequiresToolToFell: true, CollisionRadius: ForestTreeCollisionRadius),
        new ResourceDefinition(DeciduousTree, "Deciduous Tree", Woodcutting, WoodItem, RegenPerTick: DecorationWoodRegenPerTick, CanFell: true, FellLeaves: [new(TreeStump, FellTreeStumpYield), new(FallenLog, FellLogYield)], RequiresToolToFell: true, CollisionRadius: ForestTreeCollisionRadius),
        new ResourceDefinition(Bush, "Bush", Woodcutting, WoodItem, RegenPerTick: DecorationWoodRegenPerTick, CanFell: true, FellLeaves: [new(Wood, FellBushWoodYield)], CollisionRadius: BushCollisionRadius),
        new ResourceDefinition(Flower, "Flower", Foraging, GrassItem, RegenPerTick: DecorationGroundCoverRegenPerTick),
        new ResourceDefinition(Fern, "Fern", Foraging, GrassItem, RegenPerTick: DecorationGroundCoverRegenPerTick),
        new ResourceDefinition(RockPile, "Rock Pile", Mining, StoneItem, CollisionRadius: RockPileCollisionRadius),
        new ResourceDefinition(RockBoulder, "Rock Boulder", Mining, StoneItem, CollisionRadius: RockBoulderCollisionRadius),
        new ResourceDefinition(RockCluster, "Rock Cluster", Mining, StoneItem, CollisionRadius: RockClusterCollisionRadius),
        new ResourceDefinition(TreeStump, "Tree Stump", Woodcutting, WoodItem),
        new ResourceDefinition(FallenLog, "Fallen Log", Woodcutting, WoodItem),
    });

    private static SkillCatalog CreateSkillCatalog() => new(new[]
    {
        new SkillDefinition(Foraging, "Foraging", BasicForaging, EfficientForaging),
        new SkillDefinition(MushroomForaging, "Mushroom Foraging", BasicMushroomForaging, EfficientMushroomForaging),
        new SkillDefinition(RootDigging, "Root Digging", BasicRootDigging, EfficientRootDigging),
        new SkillDefinition(Woodcutting, "Woodcutting", BasicWoodcutting, EfficientWoodcutting, UsesChoppingScore: true),
        new SkillDefinition(Twisting, "Twisting", BasicTwisting, EfficientTwisting),
        new SkillDefinition(Binding, "Binding", BasicBinding, EfficientBinding),
        new SkillDefinition(Knapping, "Knapping", BasicKnapping, EfficientKnapping),
        new SkillDefinition(Tanning, "Tanning", BasicTanning, EfficientTanning),
        new SkillDefinition(Sharpening, "Sharpening", BasicSharpening, EfficientSharpening),
        new SkillDefinition(Mining, "Mining", BasicMining, EfficientMining),
        new SkillDefinition(Burial, "Burial", BasicBurial, EfficientBurial),
        new SkillDefinition(Eating, "Eating", BasicEating, EfficientEating),
        new SkillDefinition(Teaching, "Teaching", BasicTeaching, EfficientTeaching),
        new SkillDefinition(Butchering, "Butchering", BasicButchering, EfficientButchering),
        new SkillDefinition(Hunting, "Hunting", BasicHunting, EfficientHunting),
    });

    private static RecipeCatalog CreateRecipeCatalog() => new(new[]
    {
        new RecipeDefinition(Axe, WoodItem, AxeInputAmount),
        new RecipeDefinition(WarmClothing, HideItem, WarmClothingInputAmount),
        // Rawhide clothing needs no further knowledge and shrivels each season - the same
        // two-hide amount as warm_clothing.
        new RecipeDefinition(RawhideClothing, RawhideItem, WarmClothingInputAmount),
        new RecipeDefinition(Basket, WoodItem, BasketInputAmount),
        new RecipeDefinition(Bag, GrassItem, BagInputAmount),
        new RecipeDefinition(StorageHutItem, WoodItem, StorageHutInputAmount),
    });

    private static FormCatalog CreateFormCatalog() => new(new[]
    {
        new FormDefinition(Whole, "Whole"),
        new FormDefinition(Stick, "Stick", HaftLeverage: StickHaftLeverage),
        new FormDefinition(Lump, "Lump"),
        new FormDefinition(Fibre, "Fibre"),
        new FormDefinition(Wedge, "Wedge", WedgeEdgeSharpness),
        new FormDefinition(Vessel, "Vessel"),
        new FormDefinition(Garment, "Garment"),
        new FormDefinition(Shelter, "Shelter"),
        new FormDefinition(Cord, "Cord", LashingStrength: CordLashingStrength),
    });

    private static MaterialCatalog CreateMaterialCatalog() => new(new[]
    {
        new MaterialDefinition(WoodMaterial, "Wood", WoodDensity, Hardness: WoodHardness, Toughness: WoodToughness, Flexibility: WoodFlexibility, Fibrousness: WoodFibrousness),
        new MaterialDefinition(StoneMaterial, "Stone", StoneDensity, Hardness: StoneHardness, Toughness: StoneToughness),
        new MaterialDefinition(PlantFibreMaterial, "Plant Fibre", PlantFibreDensity, Toughness: PlantFibreToughness, Flexibility: PlantFibreFlexibility, Fibrousness: PlantFibreFibrousness),
        // Tanned, so it never spoils - rawhide below is what a fresh carcass gives instead.
        new MaterialDefinition(HideMaterial, "Hide", HideDensity, HideInsulation, Toughness: HideToughness, Flexibility: HideFlexibility, Elasticity: HideElasticity, Fibrousness: HideFibrousness),
        new MaterialDefinition(RawhideMaterial, "Rawhide", HideDensity, HideInsulation, Toughness: HideToughness, Flexibility: HideFlexibility, Elasticity: HideElasticity, Fibrousness: HideFibrousness, ShelfLifeTicks: RawhideShelfLifeTicks),
        new MaterialDefinition(AppleMaterial, "Apple Flesh", FoodDensity, ShelfLifeTicks: AppleShelfLifeTicks),
        new MaterialDefinition(PearMaterial, "Pear Flesh", FoodDensity, ShelfLifeTicks: PearShelfLifeTicks),
        new MaterialDefinition(PotatoMaterial, "Potato Flesh", FoodDensity, ShelfLifeTicks: PotatoShelfLifeTicks),
        new MaterialDefinition(MushroomMaterial, "Mushroom Flesh", FoodDensity, ShelfLifeTicks: MushroomShelfLifeTicks),
        new MaterialDefinition(MeatMaterial, "Meat", FoodDensity, ShelfLifeTicks: MeatShelfLifeTicks),
        new MaterialDefinition(BoneMaterial, "Bone", BoneDensity, Hardness: BoneHardness, Toughness: BoneToughness, Flexibility: BoneFlexibility, Elasticity: BoneElasticity, Fibrousness: BoneFibrousness),
        // Dried sinew does not spoil.
        new MaterialDefinition(SinewMaterial, "Sinew", SinewDensity, Toughness: SinewToughness, Flexibility: SinewFlexibility, Elasticity: SinewElasticity, Fibrousness: SinewFibrousness),
    });

    // The axe is stone; the warm clothing recipe asks for hide (Content/recipes/warm_clothing/
    // warm_clothing.json).
    private static ItemCatalog CreateItemCatalog(MaterialCatalog materials, FormCatalog forms) => new(new[]
    {
        new ItemDefinition(WarmClothing, "Warm Clothing", HideMaterial, Garment, WarmClothingVolume),
        new ItemDefinition(RawhideClothing, "Rawhide Clothing", RawhideMaterial, Garment, WarmClothingVolume),
        new ItemDefinition(WoodItem, "Wood", WoodMaterial, Stick, WoodVolume),
        new ItemDefinition(Axe, "Axe", StoneMaterial, Wedge, AxeVolume),
        new ItemDefinition(AppleItem, "Apple", AppleMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(PearItem, "Pear", PearMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(MushroomItem, "Mushroom", MushroomMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(PotatoItem, "Potato", PotatoMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(GrassItem, "Grass", PlantFibreMaterial, Fibre, GrassVolume, GrassHungerRestoredPerUnit, Transitions: [new FormTransition(TwistVerb, Cord, GrassPerCord)]),
        new ItemDefinition(StoneItem, "Stone", StoneMaterial, Lump, StoneVolume, Transitions: [new FormTransition(KnapVerb, Wedge, StonePerWedge)]),
        new ItemDefinition(Basket, "Basket", WoodMaterial, Vessel, BasketVolume, CarryCapacityBonus: BasketCarryCapacityBonus),
        new ItemDefinition(Bag, "Bag", PlantFibreMaterial, Vessel, BagVolume, CarryCapacityBonus: BagCarryCapacityBonus),
        new ItemDefinition(StorageHutItem, "Storage Hut", WoodMaterial, Shelter, StorageHutVolume),
        new ItemDefinition(MeatItem, "Meat", MeatMaterial, Lump, FoodVolume, MeatHungerRestoredPerUnit),
        new ItemDefinition(HideItem, "Hide", HideMaterial, Whole, HideVolume),
        new ItemDefinition(RawhideItem, "Rawhide", RawhideMaterial, Whole, HideVolume, Transitions: [new FormTransition(TanVerb, Whole, RawhidePerHide, HideMaterial)]),
        new ItemDefinition(BoneItem, "Bone", BoneMaterial, Stick, BoneVolume),
        new ItemDefinition(SinewItem, "Sinew", SinewMaterial, Fibre, SinewVolume, Transitions: [new FormTransition(TwistVerb, Cord, SinewPerCord)]),
    }, materials, forms);

    // For tests about corpse decay and bones that shrink CorpseDecayTicks/BonesLingerTicks
    // rather than simulating hundreds of ticks to reach them.
    private static WorldConfiguration CreateConfigurationWithShortCorpseDecay(long corpseDecayTicks, long bonesLingerTicks) =>
        CreateConfigurationWithDeer() with { Rules = SimulationRules.Default with { CorpseDecayTicks = corpseDecayTicks, BonesLingerTicks = bonesLingerTicks } };
}
