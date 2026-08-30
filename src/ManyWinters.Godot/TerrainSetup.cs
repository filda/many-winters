using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

// Builds the terrain mesh, waterways, and every scattered decoration prop - purely
// presentation/art, no gameplay rule lives here (contrast MapLoader, which defines the
// actual starting WorldState). Pulled out of Main so that class isn't also the place you'd
// have to touch to retune tree counts or add a new decoration kind.
public static class TerrainSetup
{
    private const string HeightmapPath = "res://Content/terrain/praha-liben/heightmap.json";
    private const string WaterwaysPath = "res://Content/terrain/praha-liben/waterways.json";
    private const string GroundTexturePath = "res://Content/terrain/ground.png";
    private const string ConiferTreePath = "res://Content/terrain/conifer_tree.png";
    private const string DeciduousTreePath = "res://Content/terrain/deciduous_tree.png";
    private const string BushPath = "res://Content/terrain/bush.png";
    private const string GrassPath = "res://Content/terrain/grass.png";
    private const string FlowerPath = "res://Content/terrain/flower.png";
    private const string FernPath = "res://Content/terrain/fern.png";
    private const string RockPilePath = "res://Content/terrain/rock_pile.png";
    private const string RockBoulderPath = "res://Content/terrain/rock_boulder.png";
    private const string RockClusterPath = "res://Content/terrain/rock_cluster.png";
    private const string TreeStumpPath = "res://Content/terrain/tree_stump.png";
    private const string FallenLogPath = "res://Content/terrain/fallen_log.png";

    // DecorationRadius grew from the original 70m (see below) to cover much more of the map,
    // not just a small disk hugging camp - every dense-zone count below was scaled up by
    // roughly the same area ratio ((110/70)^2 ~ 2.47x) so the forest reads at least as thick
    // as before over that much bigger footprint, not thinned out by spreading the old counts
    // over a wider area.
    private const int TreeCount = 345;
    private const int DeciduousTreeCount = 247;
    private const int BushCount = 222;
    // Ground cover needs to read as more than scattered specks, but every decoration here is
    // still its own Sprite3D + shadow node pair (these are meant to become real, individually
    // clickable ResourceNode entities later - todo #11/#13 - not get merged into an anonymous
    // GPU-instanced batch that would need unpicking again once that happens), so the total
    // count is a real per-frame cost. A first pass at "1/12 m^2" density (a proper carpet)
    // visibly stuttered - this instead only moderately improves on the original 1/38 m^2.
    private const int GrassCount = 1500;
    private const int FlowerCount = 250;
    private const int FernCount = 550;
    private const int RockCount = 150;
    private const int StumpCount = 25;
    private const int FallenLogCount = 18;
    // A second, sparser pass across the whole real terrain patch (radius = terrain.Half) on
    // top of the camp-centered one above - without it, everywhere outside DecorationRadius of
    // camp is bare, since the dense pass never reaches that far. This pass covers the *entire*
    // ~1km terrain patch, so it's by far the most sensitive to count - a first attempt at
    // bumping ground cover here (6500/1300/3100) was the single biggest contributor to a
    // visible stutter. These are a modest (~4x) increase over the original 120/30/(no fern
    // yet) instead, not an attempt at real meadow density this far out.
    private const int WideTreeCount = 70;
    private const int WideDeciduousTreeCount = 50;
    private const int WideBushCount = 40;
    private const int WideGrassCount = 500;
    private const int WideFlowerCount = 120;
    private const int WideFernCount = 300;
    private const int WideRockCount = 30;
    private const int WideStumpCount = 12;
    private const int WideFallenLogCount = 8;
    // A handful of standalone groves scattered elsewhere on the map, on top of the wide
    // background pass and the camp's own dense zone - a single "dense disk around camp,
    // sparse everywhere else" gradient reads as one unnaturally uniform blob rather than the
    // patchy, clumped-together way real forest cover actually sits on a landscape. Count and
    // radius both grew alongside DecorationRadius, same area-ratio reasoning.
    private const int GroveCount = 6;
    private const float GroveRadius = 65f;
    private const int GroveTreeCount = 85;
    private const int GroveDeciduousTreeCount = 55;
    private const int GroveBushCount = 50;
    // Same moderate density as the dense zone's own GrassCount/etc.
    private const int GroveGrassCount = 530;
    private const int GroveFlowerCount = 90;
    private const int GroveFernCount = 190;
    private const int GroveRockCount = 30;
    private const int GroveStumpCount = 6;
    private const int GroveFallenLogCount = 4;
    private const float TreeHeightMeters = 8f;
    private const float DeciduousTreeHeightMeters = 7f;
    private const float BushHeightMeters = 1.2f;
    private const float GrassHeightMeters = 0.4f;
    private const float FlowerHeightMeters = 0.35f;
    private const float FernHeightMeters = 0.5f;
    private const float RockHeightMeters = 1.5f;
    private const float StumpHeightMeters = 1.1f;
    private const float FallenLogHeightMeters = 1.3f;
    private const float DecorationMinScale = 0.8f;
    private const float DecorationMaxScale = 1.3f;
    private const int DecorationScatterSeed = 1;
    // Centered on the camp, not spread across the whole real terrain patch (Half=500) - see
    // ScatterDecoration's doc comment. Grown from the original 70m so the forest reads as
    // covering a real swath of the map, not just a small disk immediately around the starting
    // band, while still well short of the full 500m extent.
    private const float DecorationRadius = 110f;

    private static readonly Color TreeFallbackColor = new(0.20f, 0.32f, 0.18f);
    private static readonly Color DeciduousTreeFallbackColor = new(0.30f, 0.38f, 0.22f);
    private static readonly Color BushFallbackColor = new(0.26f, 0.36f, 0.18f);
    private static readonly Color GrassFallbackColor = new(0.32f, 0.42f, 0.18f);
    private static readonly Color FlowerFallbackColor = new(0.82f, 0.52f, 0.62f);
    private static readonly Color FernFallbackColor = new(0.28f, 0.40f, 0.20f);
    private static readonly Color RockFallbackColor = new(0.5f, 0.5f, 0.52f);
    private static readonly Color StumpFallbackColor = new(0.36f, 0.24f, 0.14f);
    private static readonly Color FallenLogFallbackColor = new(0.38f, 0.26f, 0.15f);

    public static TerrainRenderer Create(Node3D parent, Position campCenter, CollisionObject3D.InputEventEventHandler onGroundClicked)
    {
        var terrain = new TerrainRenderer(HeightmapPath, WaterwaysPath, GroundTexturePath);
        var groundBody = terrain.BuildTerrainMesh(parent);
        groundBody.InputEvent += onGroundClicked;
        terrain.BuildWaterways(parent);

        var rng = new Random(DecorationScatterSeed);
        var campX = (float)campCenter.X;
        var campZ = (float)campCenter.Y;

        // texturePaths as params (last, not first, unlike the old single-texture signature)
        // so a call can list one texture or several equally-likely variants - see
        // TerrainRenderer.ScatterDecoration's own doc comment.
        void Scatter(int count, float height, Color fallbackColor, float centerX, float centerZ, float radius, params string[] texturePaths)
            => terrain.ScatterDecoration(parent, rng, count, texturePaths, height, fallbackColor, DecorationMinScale, DecorationMaxScale, centerX, centerZ, radius);

        // A single disk of the full radius draws as one perfect, hard-edged circle once
        // you're zoomed out enough to see its whole outline - unmistakably a "stamp", not a
        // patch of forest. Splitting it into several smaller, jittered, differently-sized
        // disks and letting their footprints overlap is the same fix generate_sprites.py's
        // lobe_cluster_mask already uses for canopy silhouettes (many small unioned blobs,
        // not one primitive) - the union's outline is irregular/scalloped instead.
        //
        // Ground cover (grass/flower/fern) deliberately isn't part of this - a real meadow
        // is a continuous carpet, not clumps with bare gaps between them the way trees
        // actually do grow, so those are scattered once over the *whole* disk instead (see
        // the plain Scatter calls right after each ScatterClump call below).
        void ScatterClump(
            float centerX, float centerZ, float radius, int subClusters,
            int treeCount, int deciduousCount, int bushCount, int rockCount, int stumpCount, int fallenLogCount)
        {
            for (var sub = 0; sub < subClusters; sub++)
            {
                var subAngle = (float)rng.NextDouble() * Mathf.Tau;
                var subCenterOffset = radius * 0.35f * (float)rng.NextDouble();
                var subX = centerX + (MathF.Cos(subAngle) * subCenterOffset);
                var subZ = centerZ + (MathF.Sin(subAngle) * subCenterOffset);
                var subRadius = radius * (0.55f + ((float)rng.NextDouble() * 0.35f));

                Scatter(treeCount / subClusters, TreeHeightMeters, TreeFallbackColor, subX, subZ, subRadius, ConiferTreePath);
                Scatter(deciduousCount / subClusters, DeciduousTreeHeightMeters, DeciduousTreeFallbackColor, subX, subZ, subRadius, DeciduousTreePath);
                Scatter(bushCount / subClusters, BushHeightMeters, BushFallbackColor, subX, subZ, subRadius, BushPath);
                Scatter(rockCount / subClusters, RockHeightMeters, RockFallbackColor, subX, subZ, subRadius, RockPilePath, RockBoulderPath, RockClusterPath);
                Scatter(stumpCount / subClusters, StumpHeightMeters, StumpFallbackColor, subX, subZ, subRadius, TreeStumpPath);
                Scatter(fallenLogCount / subClusters, FallenLogHeightMeters, FallenLogFallbackColor, subX, subZ, subRadius, FallenLogPath);
            }
        }

        const int denseZoneSubClusters = 5;
        ScatterClump(campX, campZ, DecorationRadius, denseZoneSubClusters, TreeCount, DeciduousTreeCount, BushCount, RockCount, StumpCount, FallenLogCount);
        Scatter(GrassCount, GrassHeightMeters, GrassFallbackColor, campX, campZ, DecorationRadius, GrassPath);
        Scatter(FlowerCount, FlowerHeightMeters, FlowerFallbackColor, campX, campZ, DecorationRadius, FlowerPath);
        Scatter(FernCount, FernHeightMeters, FernFallbackColor, campX, campZ, DecorationRadius, FernPath);

        const int groveSubClusters = 3;
        for (var i = 0; i < GroveCount; i++)
        {
            var groveX = ((float)rng.NextDouble() - 0.5f) * 2f * terrain.Half;
            var groveZ = ((float)rng.NextDouble() - 0.5f) * 2f * terrain.Half;
            ScatterClump(
                groveX, groveZ, GroveRadius, groveSubClusters,
                GroveTreeCount, GroveDeciduousTreeCount, GroveBushCount, GroveRockCount, GroveStumpCount, GroveFallenLogCount);
            Scatter(GroveGrassCount, GrassHeightMeters, GrassFallbackColor, groveX, groveZ, GroveRadius, GrassPath);
            Scatter(GroveFlowerCount, FlowerHeightMeters, FlowerFallbackColor, groveX, groveZ, GroveRadius, FlowerPath);
            Scatter(GroveFernCount, FernHeightMeters, FernFallbackColor, groveX, groveZ, GroveRadius, FernPath);
        }

        Scatter(WideTreeCount, TreeHeightMeters, TreeFallbackColor, 0f, 0f, terrain.Half, ConiferTreePath);
        Scatter(WideDeciduousTreeCount, DeciduousTreeHeightMeters, DeciduousTreeFallbackColor, 0f, 0f, terrain.Half, DeciduousTreePath);
        Scatter(WideBushCount, BushHeightMeters, BushFallbackColor, 0f, 0f, terrain.Half, BushPath);
        Scatter(WideGrassCount, GrassHeightMeters, GrassFallbackColor, 0f, 0f, terrain.Half, GrassPath);
        Scatter(WideFlowerCount, FlowerHeightMeters, FlowerFallbackColor, 0f, 0f, terrain.Half, FlowerPath);
        Scatter(WideFernCount, FernHeightMeters, FernFallbackColor, 0f, 0f, terrain.Half, FernPath);
        Scatter(WideRockCount, RockHeightMeters, RockFallbackColor, 0f, 0f, terrain.Half, RockPilePath, RockBoulderPath, RockClusterPath);
        Scatter(WideStumpCount, StumpHeightMeters, StumpFallbackColor, 0f, 0f, terrain.Half, TreeStumpPath);
        Scatter(WideFallenLogCount, FallenLogHeightMeters, FallenLogFallbackColor, 0f, 0f, terrain.Half, FallenLogPath);

        return terrain;
    }
}
