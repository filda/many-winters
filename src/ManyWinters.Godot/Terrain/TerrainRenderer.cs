using System.Text.Json;
using Godot;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Terrain;

// Real-terrain rendering (docs/terrain-and-world-scale-architecture.md): loads one
// elevation/waterway patch and builds it into a Node3D. Shared by Prototypes/TerrainSandbox.cs
// and Main.cs (via TerrainSetup) so both render identical terrain.
public sealed class TerrainRenderer
{
    private const float TextureTileMeters = 16f;
    private const float WaterSurfaceOffset = 0.15f;

    private static readonly Color LowColor = new(0.22f, 0.24f, 0.16f);
    private static readonly Color HighColor = new(0.55f, 0.52f, 0.46f);
    private static readonly Color WaterColor = new(0.24f, 0.34f, 0.40f, 0.8f);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record HeightmapData(
        string Source,
        double CenterLatitude,
        double CenterLongitude,
        float CellSizeMeters,
        int GridSize,
        float[][] Heights);

    private sealed record WaterwaysData(
        string Source,
        double CenterLatitude,
        double CenterLongitude,
        WaterwayPolyline[] Polylines);

    // The JSON carries name/waterway-type per polyline too; only the geometry is read here.
    // Instantiated by JsonSerializer via WaterwaysData.Polylines, which InspectCode doesn't see.
    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed record WaterwayPolyline(float WidthMeters, float[][] Points);

    // Minimum gap so two decorations never land (near-)exactly on top of each other, which reads
    // as z-fighting rather than the deliberate clumped-forest overlap. Rejects only coincidence,
    // not crowding - at ~1500 points over a ~38000 sq m disk a 10cm collision is near even odds.
    private const float MinDecorationSpacing = 0.1f;
    private const int MaxPlacementAttempts = 10;

    private readonly string _groundTexturePath;
    private readonly string _waterwaysPath;
    private Heightmap _heightmap = null!;
    private string _heightmapJson = null!;

    // Spatial hash (cell size = MinDecorationSpacing) of every decoration placed so far, across
    // every ScatterDecoration call - O(1)-ish neighbour lookup once the total reaches thousands.
    private readonly Dictionary<(int, int), List<Vector2>> _occupiedPositions = new();

    public float Half { get; private set; }

    public TerrainRenderer(string heightmapPath, string waterwaysPath, string groundTexturePath)
    {
        _waterwaysPath = waterwaysPath;
        _groundTexturePath = groundTexturePath;
        LoadHeightmap(heightmapPath);
    }

    private void LoadHeightmap(string heightmapPath)
    {
        var json = ContentFiles.ReadText(heightmapPath);
        _heightmapJson = json;
        var data = JsonSerializer.Deserialize<HeightmapData>(json, JsonOptions)
            ?? throw new InvalidDataException($"Heightmap '{heightmapPath}' could not be parsed.");

        _heightmap = new Heightmap(data.Heights, data.GridSize, data.CellSizeMeters);
        Half = _heightmap.HalfExtentMeters;
    }

    private int FineGridSize => _heightmap.FineGridSize;

    private float FineCellSize => _heightmap.FineCellSize;

    // The ground height anything standing on the terrain should use - see Heightmap.HeightAt
    // for why it interpolates the mesh's own vertices rather than the formula behind them.
    public float SampleHeight(float x, float z) => _heightmap.HeightAt(x, z);

    // Bump whenever the vertex/colour/UV formula in BuildMeshAndCollision changes shape: the hash
    // covers every value that goes into the mesh, not the code that combines them.
    private const int TerrainMeshCacheVersion = 1;
    private const string TerrainMeshCacheDirectory = "user://terrain_mesh_cache";

    // The mesh build is a pure function of the heightmap file and the constants above, and took
    // ~1.2s per start - the biggest chunk of startup. A hash-keyed cache loads it in a few ms and
    // invalidates itself on any heightmap or tuning change.
    private string ComputeMeshCacheKey()
    {
        var input = string.Join(
            '|',
            TerrainMeshCacheVersion,
            Heightmap.ShapeFingerprint,
            LowColor,
            HighColor,
            _heightmapJson);
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }

    // Returns the collision body so callers can hook click handling onto it ("click ground to
    // walk there").
    public StaticBody3D BuildTerrainMesh(Node3D parent)
    {
        var cacheKey = ComputeMeshCacheKey();
        var meshCachePath = $"{TerrainMeshCacheDirectory}/{cacheKey}.mesh.res";
        var shapeCachePath = $"{TerrainMeshCacheDirectory}/{cacheKey}.shape.res";

        Mesh mesh;
        Shape3D collisionShape;
        if (ResourceLoader.Exists(meshCachePath) && ResourceLoader.Exists(shapeCachePath))
        {
            mesh = ResourceLoader.Load<Mesh>(meshCachePath, cacheMode: ResourceLoader.CacheMode.Replace);
            collisionShape = ResourceLoader.Load<Shape3D>(shapeCachePath, cacheMode: ResourceLoader.CacheMode.Replace);
        }
        else
        {
            (mesh, collisionShape) = BuildMeshAndCollision();

            DirAccess.MakeDirRecursiveAbsolute(TerrainMeshCacheDirectory);
            ResourceSaver.Save(mesh, meshCachePath);
            ResourceSaver.Save(collisionShape, shapeCachePath);
        }

        var groundTexture = ResourceLoader.Load<Texture2D>(_groundTexturePath);
        var meshInstance = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = groundTexture,
                // LinearWithMipmaps to match BillboardSprite's filter; Nearest made the ground's
                // tiling read as blocky next to everything standing on it.
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
                VertexColorUseAsAlbedo = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        };
        parent.AddChild(meshInstance);

        var collisionBody = new StaticBody3D { InputRayPickable = true };
        collisionBody.AddChild(new CollisionShape3D { Shape = collisionShape });
        parent.AddChild(collisionBody);
        return collisionBody;
    }

    private (Mesh Mesh, Shape3D CollisionShape) BuildMeshAndCollision()
    {
        var heightRange = Math.Max(0.001f, _heightmap.MaxHeight - _heightmap.MinHeight);

        // The heightmap is a 41x41 grid at 25m - far too coarse for the bump's wavelength (see
        // Heightmap), so each source cell is subdivided (Heightmap.SubdivisionsPerCell) without
        // changing the source data. RawAt + BumpAt per vertex, not SampleHeight: HeightAt blends
        // between these very vertices, so calling it here would add a pointless interpolation.
        var fineGridSize = FineGridSize;
        var fineCellSize = FineCellSize;

        // Each fine-grid vertex is shared by up to 4 quads; computing it per quad would evaluate
        // the bump noise up to 4x over (~640,000 instead of 160,801 on a 401x401 grid).
        var vertices = new Vector3[fineGridSize, fineGridSize];
        var colors = new Color[fineGridSize, fineGridSize];
        for (var row = 0; row < fineGridSize; row++)
        {
            for (var col = 0; col < fineGridSize; col++)
            {
                var x = (col * fineCellSize) - Half;
                var z = (row * fineCellSize) - Half;
                var rawHeight = _heightmap.RawAt(x, z);
                vertices[row, col] = new Vector3(x, rawHeight + Heightmap.BumpAt(x, z), z);
                colors[row, col] = LowColor.Lerp(HighColor, rawHeight / heightRange);
            }
        }

        Vector2 UvFor(Vector3 vertex) => new Vector2(vertex.X, vertex.Z) / TextureTileMeters;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        for (var row = 0; row < fineGridSize - 1; row++)
        {
            for (var col = 0; col < fineGridSize - 1; col++)
            {
                var a = vertices[row, col];
                var b = vertices[row, col + 1];
                var c = vertices[row + 1, col];
                var d = vertices[row + 1, col + 1];

                AddTriangle(
                    surfaceTool,
                    (a, colors[row, col], UvFor(a)),
                    (b, colors[row, col + 1], UvFor(b)),
                    (c, colors[row + 1, col], UvFor(c)));
                AddTriangle(
                    surfaceTool,
                    (b, colors[row, col + 1], UvFor(b)),
                    (d, colors[row + 1, col + 1], UvFor(d)),
                    (c, colors[row + 1, col], UvFor(c)));
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var collisionShape = mesh.CreateTrimeshShape();
        return (mesh, collisionShape);
    }

    private static void AddTriangle(
        SurfaceTool tool,
        (Vector3 Position, Color Color, Vector2 Uv) a,
        (Vector3 Position, Color Color, Vector2 Uv) b,
        (Vector3 Position, Color Color, Vector2 Uv) c)
    {
        foreach (var vertex in new[] { a, b, c })
        {
            tool.SetColor(vertex.Color);
            tool.SetUV(vertex.Uv);
            tool.AddVertex(vertex.Position);
        }
    }

    // Real OSM waterway centerlines (art/fetch_stream.py) as flat ribbons following the terrain's
    // raw height at each point - the DEM already holds the valley the river cut.
    public void BuildWaterways(Node3D parent)
    {
        if (!ContentFiles.Exists(_waterwaysPath))
        {
            return;
        }

        var json = ContentFiles.ReadText(_waterwaysPath);
        var data = JsonSerializer.Deserialize<WaterwaysData>(json, JsonOptions)
            ?? throw new InvalidDataException($"Waterways '{_waterwaysPath}' could not be parsed.");

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        var builtAny = false;

        foreach (var polyline in data.Polylines)
        {
            var points = polyline.Points;
            var halfWidth = polyline.WidthMeters / 2f;

            for (var i = 0; i < points.Length - 1; i++)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                var direction = new Vector2(p1[0] - p0[0], p1[1] - p0[1]);
                if (direction.LengthSquared() < 0.0001f)
                {
                    continue;
                }

                var side = new Vector2(-direction.Y, direction.X).Normalized() * halfWidth;

                var a = WaterVertex(p0[0] - side.X, p0[1] - side.Y);
                var b = WaterVertex(p0[0] + side.X, p0[1] + side.Y);
                var c = WaterVertex(p1[0] - side.X, p1[1] - side.Y);
                var d = WaterVertex(p1[0] + side.X, p1[1] + side.Y);

                AddPlainTriangle(surfaceTool, a, c, b);
                AddPlainTriangle(surfaceTool, b, c, d);
                builtAny = true;
            }
        }

        if (!builtAny)
        {
            return;
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        parent.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = WaterColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        });
    }

    private Vector3 WaterVertex(float x, float z) => new Vector3(x, _heightmap.RawAt(x, z) + WaterSurfaceOffset, z);

    private static void AddPlainTriangle(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c)
    {
        tool.AddVertex(a);
        tool.AddVertex(b);
        tool.AddVertex(c);
    }

    // Cutout/billboard scatter for the TerrainSandbox prototype; the game's decorations are
    // ResourceNodes (MapLoader.ScatterDecorations). Per-node sprites, never a MultiMesh batch:
    // decorations keep individual identity so they can become clickable (AGENTS.md).
    //
    // Scattered within radius of (centerX, centerZ), not over the whole ~1 km patch - a forest
    // dense enough there would still leave the small playable area bare. Each instance picks
    // one of texturePaths at random, so one call can mix differently shaped rocks.
    public void ScatterDecoration(
        Node3D parent,
        Random rng,
        int count,
        IReadOnlyList<string> texturePaths,
        float baseHeight,
        Color fallbackColor,
        float minScale,
        float maxScale,
        float centerX,
        float centerZ,
        float radius)
    {
        for (var i = 0; i < count; i++)
        {
            // Uniform over the disk, not a square: sqrt(u) compensates for outer rings covering
            // more area, so points do not bunch toward the centre. Retried up to
            // MaxPlacementAttempts when within MinDecorationSpacing of a placed decoration; falls
            // back to the last attempt rather than skipping (the same "don't loop forever"
            // trade-off as MapLoader's crowd placement).
            var position = new Vector2(centerX, centerZ);
            for (var attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                var angle = (float)rng.NextDouble() * Mathf.Tau;
                var distance = radius * MathF.Sqrt((float)rng.NextDouble());
                position = new Vector2(centerX + (MathF.Cos(angle) * distance), centerZ + (MathF.Sin(angle) * distance));
                if (!IsTooCloseToAnExistingDecoration(position))
                {
                    break;
                }
            }

            MarkOccupied(position);
            var x = position.X;
            var z = position.Y;
            var scale = minScale + ((float)rng.NextDouble() * (maxScale - minScale));
            var worldHeight = baseHeight * scale;
            var texturePath = texturePaths[rng.Next(texturePaths.Count)];

            var groundShadow = GroundShadow.Create(worldHeight * 0.5f);
            groundShadow.Position += new Vector3(x, SampleHeight(x, z) + GroundShadow.GroundOffset, z);
            parent.AddChild(groundShadow);

            var sprite = BillboardSprite.Create(texturePath, worldHeight, fallbackColor);
            sprite.Position = new Vector3(x, SampleHeight(x, z) + (worldHeight / 2f), z);
            parent.AddChild(sprite);
        }
    }

    private static (int, int) CellFor(Vector2 position) =>
        ((int)MathF.Floor(position.X / MinDecorationSpacing), (int)MathF.Floor(position.Y / MinDecorationSpacing));

    // The candidate's cell plus its 8 neighbours - two points within MinDecorationSpacing can
    // sit in adjacent cells.
    private bool IsTooCloseToAnExistingDecoration(Vector2 candidate)
    {
        var (cellX, cellY) = CellFor(candidate);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                if (!_occupiedPositions.TryGetValue((cellX + dx, cellY + dz), out var positions))
                {
                    continue;
                }

                foreach (var existing in positions)
                {
                    if (existing.DistanceTo(candidate) < MinDecorationSpacing)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void MarkOccupied(Vector2 position)
    {
        var cell = CellFor(position);
        if (!_occupiedPositions.TryGetValue(cell, out var positions))
        {
            positions = new List<Vector2>();
            _occupiedPositions[cell] = positions;
        }

        positions.Add(position);
    }
}
