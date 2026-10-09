using System.Text.Json;
using Godot;
using ManyWinters.Core.Maps;
using ManyWinters.Core.World;
using ManyWinters.Presentation.Logic;
using ManyWinters.Presentation.Sprites;

namespace ManyWinters.Presentation.Terrain;

// Real-terrain rendering (docs/terrain-and-world-scale-architecture.md): loads one
// elevation/terrain-feature patch and builds it into its own subtree. Shared by the terrain sandbox
// prototype and the game's own entry point so both render identical terrain.
public sealed partial class TerrainRenderer : Node3D
{
    // The one terrain patch the game ships, owned here rather than by whichever entry point
    // constructs it - more than one caller renders this same patch, and neither is where the
    // shipped asset paths belong.
    private const string DefaultHeightmapPath = "res://Content/terrain/praha-liben/heightmap.json";
    private const string DefaultFeaturesPath = "res://Content/terrain/praha-liben/features.json";
    private const string DefaultGroundTexturePath = "res://Content/terrain/ground.png";

    private const float TextureTileMeters = 16f;
    // How far water and rock layers float above the ground mesh, so they never z-fight it;
    // water a little higher, so where a cliff meets a river the water lies on top.
    private const float WaterLift = 0.15f;
    private const float RockLift = 0.1f;
    private const float RockRuggedness = 1f;

    // How far the shader pushes a rock edge in or out, so a cliff's even strip has bays and
    // spurs. Must stay within the exact-distance band, LayerBandCells cells of FineCellSize.
    private const float RockEdgeWobble = 2f;
    private const int LayerBandCells = 2;
    private const string SurfaceLayerShaderPath = "res://Content/effects/surface_layer.gdshader";
    private const string WaterShaderPath = "res://Content/effects/water_surface.gdshader";
    private const string GroundShaderPath = "res://Content/effects/ground.gdshader";

    // Minimum gap so two decorations never land (near-)exactly on top of each other, which reads
    // as z-fighting rather than the deliberate clumped-forest overlap. Rejects only coincidence,
    // not crowding - at ~1500 points over a ~38000 sq m disk a 10cm collision is near even odds.
    private const float MinDecorationSpacing = 0.1f;
    private const int MaxPlacementAttempts = 10;

    // Bump whenever the vertex/colour/UV formula in BuildMeshAndCollision changes shape: the hash
    // covers every value that goes into the mesh, not the code that combines them.
    private const int TerrainMeshCacheVersion = 3;
    private const string TerrainMeshCacheDirectory = "user://terrain_mesh_cache";

    // Dark and cool, so an outcrop does not read as a sandbank; the shader roughens it.
    private static readonly Color RockColor = new(0.30f, 0.31f, 0.33f);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _groundTexturePath;
    private Heightmap _heightmap = null!;
    private string _heightmapJson = null!;
    private TerrainFeatures _features = TerrainFeatures.None;
    private string _featuresJson = string.Empty;

    // Water, worked out before the ground mesh because the ground is carved to lie under it:
    // the water areas' signed distance and level per fine vertex, and the rivers in short
    // stretches, each with its level at both ends.
    private float[,] _waterDistances = null!;
    private float[,] _waterLevels = null!;
    private List<RiverStretch> _rivers = null!;

    // Metres from each fine vertex to the nearest water, area or river - how much the grass
    // there drinks from it.
    private float[,] _waterProximity = null!;

    // Spatial hash (cell size = MinDecorationSpacing) of every decoration placed so far, across
    // every ScatterDecoration call - O(1)-ish neighbour lookup once the total reaches thousands.
    private readonly Dictionary<(int, int), List<Vector2>> _occupiedPositions = new();

    private TerrainRenderer(string heightmapPath, string featuresPath, string groundTexturePath)
    {
        _groundTexturePath = groundTexturePath;
        LoadHeightmap(heightmapPath);
        LoadFeatures(featuresPath);
        PrepareWater();
    }

    // Published rather than taken as a constructor callback: a click on the ground body is
    // this type's own business to report, not a consumer's to wire in before this exists.
    public event CollisionObject3D.InputEventEventHandler? GroundInputEvent;

    public float Half { get; private set; }

    private int FineGridSize => _heightmap.FineGridSize;

    private float FineCellSize => _heightmap.FineCellSize;

    // The shipped terrain patch, mesh and waterways built - unattached, ready for composition
    // code to AddChild once and never construct a second one from these same paths.
    public static TerrainRenderer CreateDefault()
    {
        var terrain = new TerrainRenderer(DefaultHeightmapPath, DefaultFeaturesPath, DefaultGroundTexturePath);
        terrain.BuildTerrainMesh();
        terrain.BuildWaterways();
        terrain.BuildSurfaceLayers();
        return terrain;
    }

    // The ground height anything standing on the terrain should use: interpolates the mesh's
    // own vertices rather than recomputing from the height formula.
    public float SampleHeight(float x, float z) => _heightmap.HeightAt(x, z);

    // Cutout/billboard scatter for the TerrainSandbox prototype; the game's decorations are
    // ResourceNodes instead. Per-node sprites, never a MultiMesh batch: decorations keep
    // individual identity so they can become clickable (AGENTS.md).
    //
    // Scattered within radius of (centerX, centerZ), not over the whole ~1 km patch - a forest
    // dense enough there would still leave the small playable area bare. Each instance picks
    // one of texturePaths at random, so one call can mix differently shaped rocks.
    public void ScatterDecoration(
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
            // more area, so points do not bunch toward the centre. Retried a bounded number of
            // times when too close to an existing decoration, then falls back to the last
            // attempt rather than skipping.
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
            AddChild(groundShadow);

            var sprite = BillboardSprite.Create(texturePath, worldHeight, fallbackColor);
            sprite.Position = new Vector3(x, SampleHeight(x, z) + (worldHeight / 2f), z);
            AddChild(sprite);
        }
    }

    private static void AddTriangle(
        SurfaceTool tool,
        (Vector3 Position, Color Color, Vector2 Uv2) a,
        (Vector3 Position, Color Color, Vector2 Uv2) b,
        (Vector3 Position, Color Color, Vector2 Uv2) c)
    {
        foreach (var vertex in new[] { a, b, c })
        {
            tool.SetColor(vertex.Color);
            tool.SetUV2(vertex.Uv2);
            tool.AddVertex(vertex.Position);
        }
    }

    private static (int, int) CellFor(Vector2 position) =>
        ((int)MathF.Floor(position.X / MinDecorationSpacing), (int)MathF.Floor(position.Y / MinDecorationSpacing));

    // Its colours, shallow and deep, live in the shader: nothing else draws water.
    private static ShaderMaterial WaterMaterial() => new() { Shader = ResourceLoader.Load<Shader>(WaterShaderPath) };

    private static ShaderMaterial RockMaterial()
    {
        var material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(SurfaceLayerShaderPath) };
        material.SetShaderParameter("albedo", RockColor);
        material.SetShaderParameter("ruggedness", RockRuggedness);
        material.SetShaderParameter("edge_wobble", RockEdgeWobble);
        return material;
    }

    private void BuildTerrainMesh()
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

        // The shader samples the texture with mipmapped linear filtering, as sprites are filtered;
        // Nearest made the ground's tiling read as blocky next to them.
        var material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(GroundShaderPath) };
        material.SetShaderParameter("ground_texture", ResourceLoader.Load<Texture2D>(_groundTexturePath));
        material.SetShaderParameter("tile_meters", TextureTileMeters);
        AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material });

        var collisionBody = new StaticBody3D { InputRayPickable = true };
        collisionBody.AddChild(new CollisionShape3D { Shape = collisionShape });
        collisionBody.InputEvent += (camera, @event, position, normal, shapeIdx) =>
            GroundInputEvent?.Invoke(camera, @event, position, normal, shapeIdx);
        AddChild(collisionBody);
    }

    // Real OSM waterway centerlines (art/fetch_features.py) as ribbons, level across their width
    // and falling along their length; the ground under them is already carved below. Three
    // vertices across, edges and centerline, so the water shader gets its distance in from the
    // edge as for a water area: zero at both banks, half the width midstream.
    private void BuildWaterways()
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        void AddVertex(Vector2 at, float y, float inside)
        {
            surfaceTool.SetUV(new Vector2(inside, 0f));
            surfaceTool.AddVertex(new Vector3(at.X, y, at.Y));
        }

        foreach (var stretch in _rivers)
        {
            var side = new Vector2(-(stretch.To.Y - stretch.From.Y), stretch.To.X - stretch.From.X).Normalized() * stretch.HalfWidth;
            var fromY = stretch.FromLevel + WaterLift;
            var toY = stretch.ToLevel + WaterLift;

            foreach (var bank in new[] { -side, side })
            {
                AddVertex(stretch.From + bank, fromY, 0f);
                AddVertex(stretch.To + bank, toY, 0f);
                AddVertex(stretch.From, fromY, stretch.HalfWidth);

                AddVertex(stretch.From, fromY, stretch.HalfWidth);
                AddVertex(stretch.To + bank, toY, 0f);
                AddVertex(stretch.To, toY, stretch.HalfWidth);
            }
        }

        if (_rivers.Count > 0)
        {
            AddLayerMesh(surfaceTool, WaterMaterial());
        }
    }

    // Water areas and rock as their own meshes laid over the ground's own triangles, lifted
    // clear of it. Tinting the ground's vertex colours instead cannot work: the ground texture
    // multiplies them, and a green texture turns any blue into green. Waterways are left to
    // their ribbons, which follow the centerline a grid could only render as a staircase.
    private void BuildSurfaceLayers()
    {
        var fineGridSize = FineGridSize;
        var positions = new Position[fineGridSize, fineGridSize];
        var ground = new float[fineGridSize, fineGridSize];
        var waterSurface = new float[fineGridSize, fineGridSize];
        for (var row = 0; row < fineGridSize; row++)
        {
            for (var col = 0; col < fineGridSize; col++)
            {
                positions[row, col] = FinePosition(row, col);
                ground[row, col] = _heightmap.FineVertexAt(row, col);
                var level = _waterLevels[row, col];
                waterSurface[row, col] = float.IsNaN(level) ? ground[row, col] : level;
            }
        }

        var rock = SignedDistanceBand.Build(
            fineGridSize,
            (row, col) => _features.IsRock(positions[row, col]),
            (row, col) => _features.RockSignedDistance(positions[row, col]),
            LayerBandCells,
            LayerBandCells * FineCellSize);

        AddSurfaceLayer(positions, ground, rock, RockLift, RockMaterial(), RockEdgeWobble);
        AddSurfaceLayer(positions, waterSurface, _waterDistances, WaterLift, WaterMaterial(), 0f);
    }

    private void AddSurfaceLayer(Position[,] positions, float[,] heights, float[,] distances, float lift, Material material, float reach)
    {
        var fineGridSize = FineGridSize;
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        var builtAny = false;

        void AddVertex((int Row, int Col) at)
        {
            surfaceTool.SetUV(new Vector2(distances[at.Row, at.Col], 0f));
            var position = positions[at.Row, at.Col];
            surfaceTool.AddVertex(new Vector3((float)position.X, heights[at.Row, at.Col] + lift, (float)position.Y));
        }

        void AddCoveredTriangle((int Row, int Col) a, (int Row, int Col) b, (int Row, int Col) c)
        {
            if (!SignedDistanceBand.Touches(distances[a.Row, a.Col], distances[b.Row, b.Col], distances[c.Row, c.Col], reach))
            {
                return;
            }

            AddVertex(a);
            AddVertex(b);
            AddVertex(c);
            builtAny = true;
        }

        // The same two triangles per cell, in the same winding, as the ground mesh.
        for (var row = 0; row < fineGridSize - 1; row++)
        {
            for (var col = 0; col < fineGridSize - 1; col++)
            {
                AddCoveredTriangle((row, col), (row, col + 1), (row + 1, col));
                AddCoveredTriangle((row, col + 1), (row + 1, col + 1), (row + 1, col));
            }
        }

        if (!builtAny)
        {
            return;
        }

        AddLayerMesh(surfaceTool, material);
    }

    private void AddLayerMesh(SurfaceTool surfaceTool, Material material)
    {
        surfaceTool.GenerateNormals();
        AddChild(new MeshInstance3D { Mesh = surfaceTool.Commit(), MaterialOverride = material });
    }

    // Levels the water and carves the ground under it. Levels come from the DEM without the
    // bump, which samples a lake or river at its surface; the ground, bump and all, is then
    // held below them.
    private void PrepareWater()
    {
        var fineGridSize = FineGridSize;
        var reach = LayerBandCells * FineCellSize;

        _waterDistances = SignedDistanceBand.Build(
            fineGridSize,
            (row, col) => _features.IsWaterArea(FinePosition(row, col)),
            (row, col) => _features.WaterAreaSignedDistance(FinePosition(row, col)),
            LayerBandCells,
            reach);
        _waterLevels = WaterSurface.AreaLevels(_waterDistances, RawAtFineVertex, LayerBandCells);
        _rivers = RiverStretches();

        var ceiling = new float[fineGridSize, fineGridSize];
        var wet = new bool[fineGridSize, fineGridSize];
        for (var row = 0; row < fineGridSize; row++)
        {
            for (var col = 0; col < fineGridSize; col++)
            {
                wet[row, col] = _waterDistances[row, col] > 0f;
                var level = _waterLevels[row, col];
                ceiling[row, col] = float.IsNaN(level)
                    ? float.PositiveInfinity
                    : WaterSurface.Ceiling(level, _waterDistances[row, col], reach);
            }
        }

        foreach (var stretch in _rivers)
        {
            var margin = stretch.HalfWidth + reach;
            var (rowFrom, rowTo) = FineRange(Math.Min(stretch.From.Y, stretch.To.Y) - margin, Math.Max(stretch.From.Y, stretch.To.Y) + margin);
            var (colFrom, colTo) = FineRange(Math.Min(stretch.From.X, stretch.To.X) - margin, Math.Max(stretch.From.X, stretch.To.X) + margin);
            for (var row = rowFrom; row <= rowTo; row++)
            {
                for (var col = colFrom; col <= colTo; col++)
                {
                    var at = new Vector2((col * FineCellSize) - Half, (row * FineCellSize) - Half);
                    var (distance, along) = WaterSurface.ToSegment(at, stretch.From, stretch.To);
                    var level = Mathf.Lerp(stretch.FromLevel, stretch.ToLevel, along);
                    ceiling[row, col] = Math.Min(ceiling[row, col], WaterSurface.Ceiling(level, stretch.HalfWidth - distance, reach));
                    wet[row, col] |= distance <= stretch.HalfWidth;
                }
            }
        }

        _heightmap = _heightmap.WithCeiling(ceiling);

        var cells = GridDistanceField.DistanceToNearestTrue(wet);
        _waterProximity = new float[fineGridSize, fineGridSize];
        for (var row = 0; row < fineGridSize; row++)
        {
            for (var col = 0; col < fineGridSize; col++)
            {
                _waterProximity[row, col] = cells[row, col] * FineCellSize;
            }
        }
    }

    // Each waterway cut to the patch and split at the fine grid's spacing, so its level follows
    // the valley closely; the level never rises toward the mouth.
    private List<RiverStretch> RiverStretches()
    {
        var stretches = new List<RiverStretch>();
        foreach (var waterway in _features.Waterways)
        {
            var halfWidth = (float)waterway.WidthMeters / 2f;
            var pieces = new List<(Vector2 From, Vector2 To)>();
            for (var i = 0; i < waterway.Points.Count - 1; i++)
            {
                var from = new Vector2((float)waterway.Points[i].X, (float)waterway.Points[i].Y);
                var to = new Vector2((float)waterway.Points[i + 1].X, (float)waterway.Points[i + 1].Y);
                if (from.DistanceSquaredTo(to) < 0.0001f || SquareClip.Clip(from, to, Half) is not var (start, end))
                {
                    continue;
                }

                var steps = Math.Max(1, Mathf.CeilToInt(start.DistanceTo(end) / FineCellSize));
                for (var step = 0; step < steps; step++)
                {
                    pieces.Add((start.Lerp(end, (float)step / steps), start.Lerp(end, (float)(step + 1) / steps)));
                }
            }

            if (pieces.Count == 0)
            {
                continue;
            }

            // One sample per piece start plus the last piece's end, so neighbouring pieces share
            // the level where they meet.
            var samples = pieces.Select(piece => piece.From).Append(pieces[^1].To).ToList();
            var levels = WaterSurface.Descending(samples.Select(p => _heightmap.RawAt(p.X, p.Y)).ToList());
            for (var i = 0; i < pieces.Count; i++)
            {
                // Where a stream runs into a lake or a wider river, that water is already drawn;
                // a ribbon over it would double the colour and fight it for the same depth.
                var middle = (pieces[i].From + pieces[i].To) / 2f;
                if (_features.IsWaterArea(new Position(middle.X, middle.Y)))
                {
                    continue;
                }

                stretches.Add(new RiverStretch(pieces[i].From, pieces[i].To, levels[i], levels[i + 1], halfWidth));
            }
        }

        return stretches;
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

    // No features file simply means a patch without water or rock marked.
    private void LoadFeatures(string featuresPath)
    {
        if (!ContentFiles.Exists(featuresPath))
        {
            return;
        }

        _featuresJson = ContentFiles.ReadText(featuresPath);
        _features = TerrainFeatures.LoadFromJson([(featuresPath, _featuresJson)]);
    }

    // The mesh build is a pure function of the heightmap and features files and the constants
    // above, and took ~1.2s per start - the biggest chunk of startup. A hash-keyed cache loads it in a few ms and
    // invalidates itself on any heightmap or tuning change.
    private string ComputeMeshCacheKey()
    {
        var input = string.Join(
            '|',
            TerrainMeshCacheVersion,
            Heightmap.ShapeFingerprint,
            WaterSurface.Fingerprint,
            LayerBandCells,
            _heightmapJson,
            _featuresJson);
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }

    private (Mesh Mesh, Shape3D CollisionShape) BuildMeshAndCollision()
    {
        var heightRange = Math.Max(0.001f, _heightmap.MaxHeight - _heightmap.MinHeight);

        // The heightmap is a 41x41 grid at 25m - far too coarse for the bump's wavelength, so
        // each source cell is subdivided without changing the source data. FineVertexAt per
        // vertex, not SampleHeight: HeightAt blends between these very vertices, so calling it
        // here would add a pointless interpolation.
        var fineGridSize = FineGridSize;
        var fineCellSize = FineCellSize;

        // Each fine-grid vertex is shared by up to 4 quads; computing it per quad would evaluate
        // the bump noise up to 4x over (~640,000 instead of 160,801 on a 401x401 grid).
        var vertices = new Vector3[fineGridSize, fineGridSize];
        var colors = new Color[fineGridSize, fineGridSize];
        var situations = new Vector2[fineGridSize, fineGridSize];
        for (var row = 0; row < fineGridSize; row++)
        {
            for (var col = 0; col < fineGridSize; col++)
            {
                var x = (col * fineCellSize) - Half;
                var z = (row * fineCellSize) - Half;
                var rawHeight = _heightmap.RawAt(x, z);
                vertices[row, col] = new Vector3(x, _heightmap.FineVertexAt(row, col), z);
                // What the ground shader colours by (see ground.gdshader): elevation as a
                // fraction of the patch's range, distance to water, and the real slope.
                colors[row, col] = new Color(rawHeight / heightRange, 0f, 0f);
                situations[row, col] = new Vector2(_waterProximity[row, col], _heightmap.SlopeAt(x, z));
            }
        }

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
                    (a, colors[row, col], situations[row, col]),
                    (b, colors[row, col + 1], situations[row, col + 1]),
                    (c, colors[row + 1, col], situations[row + 1, col]));
                AddTriangle(
                    surfaceTool,
                    (b, colors[row, col + 1], situations[row, col + 1]),
                    (d, colors[row + 1, col + 1], situations[row + 1, col + 1]),
                    (c, colors[row + 1, col], situations[row + 1, col]));
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var collisionShape = mesh.CreateTrimeshShape();
        return (mesh, collisionShape);
    }

    private Position FinePosition(int row, int col) => new((col * FineCellSize) - Half, (row * FineCellSize) - Half);

    private float RawAtFineVertex(int row, int col) => _heightmap.RawAt((col * FineCellSize) - Half, (row * FineCellSize) - Half);

    // The fine-grid indices covering [from, to] metres along one axis, clamped to the grid.
    private (int From, int To) FineRange(float from, float to) =>
        (Math.Max(0, Mathf.FloorToInt((from + Half) / FineCellSize)), Math.Min(FineGridSize - 1, Mathf.CeilToInt((to + Half) / FineCellSize)));

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

    private readonly record struct RiverStretch(Vector2 From, Vector2 To, float FromLevel, float ToLevel, float HalfWidth);

    private sealed record HeightmapData(
        string Source,
        double CenterLatitude,
        double CenterLongitude,
        float CellSizeMeters,
        int GridSize,
        float[][] Heights);
}
