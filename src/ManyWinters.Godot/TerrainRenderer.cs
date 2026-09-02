using System.Text.Json;
using Godot;
using ManyWinters.Core.Maps;

namespace ManyWinters.Godot;

// Shared real-terrain rendering (docs/terrain-and-world-scale-architecture.md): loads one
// elevation/waterway patch and builds it into a given Node3D. Used by both TerrainSandbox.cs
// (pure visual sandbox) and Main.cs (the live game) so both render identically real terrain.
public sealed class TerrainRenderer
{
    private const float TextureTileMeters = 16f;
    private const float WaterSurfaceOffset = 0.15f;

    // The real elevation data is only a 41x41 grid at 25m spacing (docs/terrain-and-world-
    // scale-architecture.md) - bilinear interpolation between those samples alone reads as
    // smooth, blank "placka" ground. A light procedural bump on top (todo: "použít Perlinův
    // šum na lehkou modifikaci terénu") brings back some fine, organic surface variation -
    // amplitude deliberately small (a texture, not a new hill range). The wavelength has to
    // stay meaningfully larger than however far apart the mesh's own vertices actually are,
    // or it aliases into per-vertex jitter instead of smooth rolling - see
    // TerrainSubdivisionsPerCell below for why that's a mesh-resolution limit, not a
    // performance one: evaluating the noise itself costs the same regardless of wavelength.
    private static readonly Noise2D TerrainBumpNoise = new(TerrainBumpNoiseSeed);
    private const int TerrainBumpNoiseSeed = 42;
    private const int TerrainBumpOctaves = 3;
    private const double TerrainBumpFrequency = 1.0 / 10.0;
    private const float TerrainBumpAmplitudeMeters = 2.0f;

    // Each 25m source heightmap cell is rendered as this many smaller ones instead of one -
    // 10 -> 2.5m spacing between actual mesh vertices, 4 samples per TerrainBumpFrequency's
    // 10m wave (comfortable margin above the 2-sample Nyquist minimum, not right at the
    // edge of aliasing) while the DEM's own 41x41 samples stay exactly as they were
    // (SampleHeight still just bilinearly interpolates them - see BuildTerrainMesh's
    // VertexAt). Real cost, unlike the noise itself: (41-1)*10+1 = 401 vertices per side
    // instead of 41 - a few hundred thousand triangles, not millions - and a correspondingly
    // heavier collision trimesh, all built once at load, not per frame - a non-issue at this
    // map's size, but the actual reason not to push subdivision arbitrarily high just to
    // allow an even shorter wavelength.
    private const int TerrainSubdivisionsPerCell = 10;

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

    private sealed record WaterwayPolyline(string Name, string Waterway, float WidthMeters, float[][] Points);

    // Even a sprite has "mass" as far as placement goes - a minimum gap so two decorations
    // never land exactly (or near-exactly) on top of each other, which reads as a rendering
    // glitch (flickering z-fighting) rather than the deliberately overlapping clumped-forest
    // look ScatterClump's own sub-disks already lean into (see its doc comment). This only
    // rejects near-exact coincidence, not ordinary crowding - at the scatter counts/areas
    // involved, exact coincidences are common enough by chance alone to matter (~1500 points
    // over a ~38000 sq m disk already gives close to even odds of at least one 10cm-range
    // collision), not just a hypothetical edge case.
    private const float MinDecorationSpacing = 0.1f;
    private const int MaxPlacementAttempts = 10;

    private readonly string _groundTexturePath;
    private readonly string _waterwaysPath;
    private HeightmapData _heightmap = null!;
    private float _minHeight;

    // Spatial hash (cell size = MinDecorationSpacing) of every decoration position placed so
    // far, across every ScatterDecoration call on this instance - an O(1)-ish neighbor lookup
    // instead of checking a candidate against every decoration placed before it, which would
    // get slow once the running total climbs into the thousands.
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
        _heightmap = JsonSerializer.Deserialize<HeightmapData>(json, JsonOptions)
            ?? throw new InvalidDataException($"Heightmap '{heightmapPath}' could not be parsed.");

        var minHeight = float.MaxValue;
        foreach (var row in _heightmap.Heights)
        {
            foreach (var h in row)
            {
                minHeight = Math.Min(minHeight, h);
            }
        }

        _minHeight = minHeight;
        Half = (_heightmap.GridSize - 1) * _heightmap.CellSizeMeters / 2f;
    }

    // Every 25m source heightmap cell is rendered as this many smaller ones instead of one -
    // see TerrainSubdivisionsPerCell's own doc comment for why (that field is the actual
    // tunable; these two just expose the grid it implies to both SampleHeight and
    // BuildTerrainMesh so they can never compute it two slightly different ways).
    private int FineGridSize => ((_heightmap.GridSize - 1) * TerrainSubdivisionsPerCell) + 1;

    private float FineCellSize => _heightmap.CellSizeMeters / TerrainSubdivisionsPerCell;

    // The real elevation data plus the light procedural bump, at one exact fine-grid vertex -
    // what BuildTerrainMesh's own vertices are (see VertexAt) and what SampleHeight below
    // blends between for any other (x, z).
    private float FineVertexHeight(int row, int col)
    {
        var x = (col * FineCellSize) - Half;
        var z = (row * FineCellSize) - Half;
        return SampleRawHeight(x, z) + TerrainBump(x, z);
    }

    // Ground height at any local (x, z) - what everything that actually needs to visually
    // sit on the terrain uses (person/decoration placement, the camera rig, click-to-move).
    // Bilinearly blends the FOUR SURROUNDING FINE-GRID VERTEX heights - the same ones
    // BuildTerrainMesh's own vertices use - rather than evaluating the raw elevation+bump
    // formula directly at (x, z). Those two used to agree closely enough not to matter, but
    // the rendered surface between vertices is a flat triangle, not the smooth noise curve
    // itself - near a peak the true curve bulges above that straight line, near a valley it
    // dips below it - and once TerrainBump's wavelength got short enough relative to
    // TerrainSubdivisionsPerCell (several rounds of "make the waves more visible" tuning),
    // that gap became visible as a person floating above, or sinking into, ground that was
    // rendered flat right under them even though their own height came from what looked like
    // the same source. Matching the mesh's own vertices exactly, not just the formula they
    // were built from, is what actually guarantees the two agree.
    public float SampleHeight(float x, float z)
    {
        var fineGridSize = FineGridSize;
        var fineCellSize = FineCellSize;
        var colF = Mathf.Clamp((x + Half) / fineCellSize, 0, fineGridSize - 1);
        var rowF = Mathf.Clamp((z + Half) / fineCellSize, 0, fineGridSize - 1);
        var col0 = (int)Mathf.Floor(colF);
        var row0 = (int)Mathf.Floor(rowF);
        var col1 = Math.Min(col0 + 1, fineGridSize - 1);
        var row1 = Math.Min(row0 + 1, fineGridSize - 1);
        var tx = colF - col0;
        var tz = rowF - row0;

        var h0 = Mathf.Lerp(FineVertexHeight(row0, col0), FineVertexHeight(row0, col1), tx);
        var h1 = Mathf.Lerp(FineVertexHeight(row1, col0), FineVertexHeight(row1, col1), tx);
        return Mathf.Lerp(h0, h1, tz);
    }

    // Bilinear height sample at any local (x, z) from the real elevation data alone, clamped
    // to the grid's edge beyond its bounds - no bump noise. Water (WaterVertex) is the only
    // caller: a river surface is naturally smoother than the ground around it in reality, not
    // textured with the same small-scale variation, so it tracks the DEM's own valley
    // without the bump making it look choppy.
    private float SampleRawHeight(float x, float z)
    {
        var gridSize = _heightmap.GridSize;
        var heights = _heightmap.Heights;
        var colF = Mathf.Clamp((x + Half) / _heightmap.CellSizeMeters, 0, gridSize - 1);
        var rowF = Mathf.Clamp((z + Half) / _heightmap.CellSizeMeters, 0, gridSize - 1);
        var col0 = (int)Mathf.Floor(colF);
        var row0 = (int)Mathf.Floor(rowF);
        var col1 = Math.Min(col0 + 1, gridSize - 1);
        var row1 = Math.Min(row0 + 1, gridSize - 1);
        var tx = colF - col0;
        var tz = rowF - row0;

        var h0 = Mathf.Lerp(heights[row0][col0], heights[row0][col1], tx);
        var h1 = Mathf.Lerp(heights[row1][col0], heights[row1][col1], tx);
        return Mathf.Lerp(h0, h1, tz) - _minHeight;
    }

    // Fbm's own [0, 1] range remapped to [-1, 1] first - otherwise every point would only
    // ever be nudged upward, raising the whole terrain by roughly half the amplitude
    // instead of rolling both up and down around the real elevation.
    private static float TerrainBump(float x, float z) =>
        (float)((TerrainBumpNoise.Fbm(x, z, TerrainBumpOctaves, TerrainBumpFrequency) - 0.5) * 2.0) * TerrainBumpAmplitudeMeters;

    // Builds the terrain mesh + matching collision into the given parent, and returns that
    // collision body so callers can hook their own click handling onto it (e.g. "click ground
    // to walk there").
    public StaticBody3D BuildTerrainMesh(Node3D parent)
    {
        var heights = _heightmap.Heights;

        var maxHeight = float.MinValue;
        foreach (var row in heights)
        {
            foreach (var h in row)
            {
                maxHeight = Math.Max(maxHeight, h);
            }
        }

        var heightRange = Math.Max(0.001f, maxHeight - _minHeight);

        // The real heightmap is only a 41x41 grid at 25m spacing - fine enough for the DEM's
        // own broad shape, but far too coarse to resolve TerrainBump's wavelength (see its
        // own doc comment on why that has to stay meaningfully larger than sample spacing to
        // avoid aliasing into per-vertex jitter). Subdividing each 25m source cell into
        // TerrainSubdivisionsPerCell smaller ones lets the bump use a shorter, still-smooth
        // wavelength without changing the source data at all. FineVertexHeight, not
        // SampleHeight, for each vertex - SampleHeight itself now blends between these exact
        // vertices for everything ELSE (see its own doc comment), so calling it here would
        // just re-derive the same value through an extra, pointless layer of interpolation.
        var fineGridSize = FineGridSize;
        var fineCellSize = FineCellSize;

        Vector3 VertexAt(int row, int col)
        {
            var x = (col * fineCellSize) - Half;
            var z = (row * fineCellSize) - Half;
            return new Vector3(x, FineVertexHeight(row, col), z);
        }

        Color ColorAt(int row, int col)
        {
            var x = (col * fineCellSize) - Half;
            var z = (row * fineCellSize) - Half;
            var t = SampleRawHeight(x, z) / heightRange;
            return LowColor.Lerp(HighColor, t);
        }

        Vector2 UvFor(Vector3 vertex) => new Vector2(vertex.X, vertex.Z) / TextureTileMeters;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        for (var row = 0; row < fineGridSize - 1; row++)
        {
            for (var col = 0; col < fineGridSize - 1; col++)
            {
                var a = VertexAt(row, col);
                var b = VertexAt(row, col + 1);
                var c = VertexAt(row + 1, col);
                var d = VertexAt(row + 1, col + 1);

                AddTriangle(
                    surfaceTool,
                    (a, ColorAt(row, col), UvFor(a)),
                    (b, ColorAt(row, col + 1), UvFor(b)),
                    (c, ColorAt(row + 1, col), UvFor(c)));
                AddTriangle(
                    surfaceTool,
                    (b, ColorAt(row, col + 1), UvFor(b)),
                    (d, ColorAt(row + 1, col + 1), UvFor(d)),
                    (c, ColorAt(row + 1, col), UvFor(c)));
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        var groundTexture = ResourceLoader.Load<Texture2D>(_groundTexturePath);
        var meshInstance = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = groundTexture,
                // LinearWithMipmaps, not Nearest: BillboardSprite's engraving-detail sprites
                // moved to this filter in 058cf05, but the ground kept the old hard-pixel
                // filter, so its tiling read as blocky next to everything sitting on it.
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
                VertexColorUseAsAlbedo = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        };
        parent.AddChild(meshInstance);

        var collisionBody = new StaticBody3D { InputRayPickable = true };
        collisionBody.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
        parent.AddChild(collisionBody);
        return collisionBody;
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

    // Real OSM waterway centerlines (see art/fetch_stream.py), rendered as flat ribbons that
    // follow the terrain's own height at each point - the DEM already captures the valley the
    // real river cut, so the ribbon should track the visible low ground without extra fudging.
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

    private Vector3 WaterVertex(float x, float z) => new Vector3(x, SampleRawHeight(x, z) + WaterSurfaceOffset, z);

    private static void AddPlainTriangle(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c)
    {
        tool.AddVertex(a);
        tool.AddVertex(b);
        tool.AddVertex(c);
    }

    // Cutout/billboard scatter (visual plan Phase B/C) - purely decorative, unrelated to
    // gameplay resource nodes. Reuses the same BillboardSprite every other entity uses.
    //
    // Scattered within radius of (centerX, centerZ), not across the whole terrain patch -
    // a count that reads as a reasonably dense forest over a real ~1 km terrain (Half=500)
    // is instead spread so thin that the tiny playable area around it looks bare, since
    // that area is a negligible fraction of the total scatter footprint.
    // texturePaths: one or more textures for this decoration kind - each instance picks one
    // independently at random, so a single call can scatter (say) a mix of three differently
    // shaped/sized rocks instead of the same one just rescaled.
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
            // Uniform over the *disk* of radius, not independent x/z within [-radius, radius]
            // (a square) - sqrt(u) compensates for the outer rings of a circle covering more
            // area than the inner ones, so points don't bunch up toward the center. Retried
            // (up to MaxPlacementAttempts) if the candidate lands within MinDecorationSpacing
            // of an already-placed decoration - falls back to the last attempt tried rather
            // than skipping the decoration entirely if every retry still collides (same
            // "don't loop forever" tradeoff as MapLoader's own crowd placement).
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

    // Checks the candidate's own cell plus its 8 neighbors, not just the one it falls in -
    // two points can be within MinDecorationSpacing of each other while sitting in different
    // (adjacent) cells near a shared cell boundary.
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
