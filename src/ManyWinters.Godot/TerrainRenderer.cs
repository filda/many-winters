using System.Text.Json;
using Godot;

namespace ManyWinters.Godot;

// Shared real-terrain rendering (docs/terrain-and-world-scale-architecture.md): loads one
// elevation/waterway patch and builds it into a given Node3D. Used by both TerrainSandbox.cs
// (pure visual sandbox) and Main.cs (the live game) so both render identically real terrain.
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

    private sealed record WaterwayPolyline(string Name, string Waterway, float WidthMeters, float[][] Points);

    private readonly string _groundTexturePath;
    private readonly string _waterwaysPath;
    private HeightmapData _heightmap = null!;
    private float _minHeight;

    public float Half { get; private set; }

    public TerrainRenderer(string heightmapPath, string waterwaysPath, string groundTexturePath)
    {
        _waterwaysPath = waterwaysPath;
        _groundTexturePath = groundTexturePath;
        LoadHeightmap(heightmapPath);
    }

    private void LoadHeightmap(string heightmapPath)
    {
        var path = ProjectSettings.GlobalizePath(heightmapPath);
        var json = File.ReadAllText(path);
        _heightmap = JsonSerializer.Deserialize<HeightmapData>(json, JsonOptions)
            ?? throw new InvalidDataException($"Heightmap '{path}' could not be parsed.");

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

    // Bilinear height sample at any local (x, z), clamped to the grid's edge beyond its bounds.
    public float SampleHeight(float x, float z)
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

    // Builds the terrain mesh + matching collision into the given parent, and returns that
    // collision body so callers can hook their own click handling onto it (e.g. "click ground
    // to walk there").
    public StaticBody3D BuildTerrainMesh(Node3D parent)
    {
        var gridSize = _heightmap.GridSize;
        var cellSize = _heightmap.CellSizeMeters;
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

        Vector3 VertexAt(int row, int col)
        {
            var x = (col * cellSize) - Half;
            var z = (row * cellSize) - Half;
            return new Vector3(x, heights[row][col] - _minHeight, z);
        }

        Color ColorAt(int row, int col)
        {
            var t = (heights[row][col] - _minHeight) / heightRange;
            return LowColor.Lerp(HighColor, t);
        }

        Vector2 UvFor(Vector3 vertex) => new Vector2(vertex.X, vertex.Z) / TextureTileMeters;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        for (var row = 0; row < gridSize - 1; row++)
        {
            for (var col = 0; col < gridSize - 1; col++)
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
        var path = ProjectSettings.GlobalizePath(_waterwaysPath);
        if (!File.Exists(path))
        {
            return;
        }

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<WaterwaysData>(json, JsonOptions)
            ?? throw new InvalidDataException($"Waterways '{path}' could not be parsed.");

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

    private Vector3 WaterVertex(float x, float z) => new Vector3(x, SampleHeight(x, z) + WaterSurfaceOffset, z);

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
            // area than the inner ones, so points don't bunch up toward the center.
            var angle = (float)rng.NextDouble() * Mathf.Tau;
            var distance = radius * MathF.Sqrt((float)rng.NextDouble());
            var x = centerX + (MathF.Cos(angle) * distance);
            var z = centerZ + (MathF.Sin(angle) * distance);
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
}
