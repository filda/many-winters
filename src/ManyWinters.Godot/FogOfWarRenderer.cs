using Godot;
using ManyWinters.Core.Maps;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

// Renders the two non-"currently visible" fog-of-war tiers (todo #13) as overlay geometry
// above the real terrain, independent of TerrainRenderer's own (cached - see its own doc
// comment) mesh: recoloring the actual terrain per tick would throw that cache away every
// single tick, since ExplorationState.Visible changes constantly as the group moves. Two
// separate meshes, rebuilt from ExplorationState:
//   - "Unknown" cells (never explored) get a low, billowing mist ceiling a few meters above
//     the ground, fully opaque - the real terrain shape underneath should not be visible
//     there at all, and (being opaque, not alpha-blended) it correctly occludes other
//     transparent geometry like the river ribbon behind it too, unlike an alpha material,
//     which Godot's Forward+ renderer sorts per-object rather than depth-testing per pixel
//     against other transparent surfaces - a real river hundreds of meters past unexplored
//     territory poking through an alpha "wall" in front of it is exactly that sorting
//     artifact, not a hole in the geometry. Matches docs/ZemanConceptArt.png's "Unknown"
//     panel in spirit (actual swirl-cloud art, and gaps that let a hill/tree peek through,
//     are a follow-up once there's art for it - this is the shape the mechanism needs first).
//   - "Remembered" cells (explored, but nobody currently has them in sight) get a translucent
//     sepia wash hugging the ground - the terrain itself stays visible, just dimmed, same as
//     ResourceNodeView.SetRemembered already does for resource sprites.
public sealed class FogOfWarRenderer
{
    private const float CeilingHeightMeters = 3.5f;
    private const float GroundHuggingOffsetMeters = 0.1f;
    private static readonly Color UnknownColor = new(0.58f, 0.6f, 0.64f);
    private static readonly Color RememberedColor = new(0.55f, 0.48f, 0.36f, 0.5f);

    // The mesh's own edge is still a stair-stepped grid boundary no matter how much the
    // wobble above softens its *surface* - see FogOfWarRenderer's own history and the user's
    // "furt je to okolo družiny hranatý" ("it's still blocky around the group"). Cells this
    // close to any explored one are left out of the mesh entirely (see RebuildUnknown's
    // fringe exclusion) and get individual scattered cloud billboards instead - the same
    // "distinct hand-authored variants, randomly placed and scaled" treatment
    // MapLoader.ScatterDecorations already gives trees, which is exactly the "plácnout to tam
    // podobně variabilně jako stromy" the user asked for. The flat mesh only ever has to cover
    // the deep interior, where its own edge is never actually seen up close.
    private const int CloudFringeRingCells = 3;
    private const int CloudsPerFringeCellMin = 1;
    private const int CloudsPerFringeCellMax = 3;
    private const float CloudMinScale = 0.8f;
    private const float CloudMaxScale = 1.8f;
    private const float CloudBaseWorldHeight = 5f;

    private static readonly string[] CloudTexturePaths =
    [
        "res://Content/effects/cloud_1.png",
        "res://Content/effects/cloud_2.png",
        "res://Content/effects/cloud_3.png",
    ];

    // Bends the unknown mesh's otherwise perfectly flat, perfectly grid-aligned quads into
    // something that reads as a billowing mist bank instead of a rigid tabletop with a
    // stair-stepped edge: both the per-vertex height and its own horizontal position get
    // nudged by a coherent noise field, and a second, independent sample jitters each
    // vertex's brightness for a bit of visual texture in place of a flat, uniform color. All
    // three are keyed by the vertex's own base world position (not by which cell "owns" it),
    // so two quads sharing a corner always agree on where that corner actually ended up -
    // no seams between them, no matter how the terrain elevation or exploration state itself
    // changes around them.
    private static readonly Noise2D MistNoise = new(4242);
    private const double MistNoiseFrequency = 1.0 / 14.0;
    private const float MistHeightJitterMeters = 1.4f;
    private const float MistHorizontalJitterMeters = 1.6f;
    private const float MistBrightnessJitterMin = 0.85f;
    private const float MistBrightnessJitterMax = 1.15f;

    private readonly ExplorationState _exploration;
    private readonly Func<float, float, float> _sampleHeight;
    private readonly int _minCellIndex;
    private readonly int _maxCellIndex;
    private readonly MeshInstance3D _unknownMeshInstance;
    private readonly MeshInstance3D _rememberedMeshInstance;
    private readonly Node3D _cloudContainer;

    // Only the "unknown" mesh is skipped when this hasn't changed since the last rebuild -
    // it covers however much of the map's ~40,000 cells (Half=500, CellSizeMeters=5) is still
    // unexplored, which starts as almost all of them, so re-emitting it every tick regardless
    // of whether anything actually changed would be the single most expensive thing this class
    // does for no visual difference most ticks. "Remembered" is cheap by comparison - bounded
    // by however much has actually been explored so far - and Visible (which it also depends
    // on) genuinely does change most ticks the group is doing anything, so it just rebuilds
    // unconditionally.
    private int _lastExploredCount = -1;

    public FogOfWarRenderer(Node3D parent, ExplorationState exploration, Func<float, float, float> sampleHeight, float halfExtentMeters)
    {
        _exploration = exploration;
        _sampleHeight = sampleHeight;
        _minCellIndex = (int)Math.Floor(-halfExtentMeters / ExplorationState.CellSizeMeters);
        _maxCellIndex = (int)Math.Ceiling(halfExtentMeters / ExplorationState.CellSizeMeters);

        // Opaque, not alpha-blended (see the class doc comment on why alpha here caused the
        // river to visibly poke through unexplored territory) - GenerateNormals lets it catch
        // the scene's own light like the terrain and every sprite already do, instead of
        // reading as a flat, uniformly-lit cutout pasted over the world.
        _unknownMeshInstance = new MeshInstance3D
        {
            MaterialOverride = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        };
        parent.AddChild(_unknownMeshInstance);

        // Alpha, unshaded: this one is *meant* to let the dimmed terrain underneath show
        // through, so it isn't trying to catch scene light on its own surface.
        _rememberedMeshInstance = new MeshInstance3D
        {
            MaterialOverride = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        parent.AddChild(_rememberedMeshInstance);

        _cloudContainer = new Node3D();
        parent.AddChild(_cloudContainer);

        RebuildAll();
    }

    public void Refresh()
    {
        RebuildRemembered();

        var exploredCount = _exploration.Explored.Count;
        if (exploredCount != _lastExploredCount)
        {
            _lastExploredCount = exploredCount;
            var fringe = ComputeFringe();
            RebuildUnknown(fringe);
            RebuildClouds(fringe);
        }
    }

    private void RebuildAll()
    {
        RebuildRemembered();
        _lastExploredCount = _exploration.Explored.Count;
        var fringe = ComputeFringe();
        RebuildUnknown(fringe);
        RebuildClouds(fringe);
    }

    // Unexplored cells within CloudFringeRingCells of any explored one - see its own doc
    // comment for why these are handled as scattered cloud billboards instead of flat mesh.
    private HashSet<ExplorationCell> ComputeFringe()
    {
        var fringe = new HashSet<ExplorationCell>();
        foreach (var explored in _exploration.Explored)
        {
            for (var dx = -CloudFringeRingCells; dx <= CloudFringeRingCells; dx++)
            {
                for (var dy = -CloudFringeRingCells; dy <= CloudFringeRingCells; dy++)
                {
                    var candidate = new ExplorationCell(explored.X + dx, explored.Y + dy);
                    if (!_exploration.IsExplored(candidate))
                    {
                        fringe.Add(candidate);
                    }
                }
            }
        }

        return fringe;
    }

    // Shared-vertex grid (like TerrainRenderer.BuildTerrainMesh's own precomputed array) -
    // every corner is computed exactly once from its own base position, then quads for
    // unexplored cells reference those same corner values. Building each quad independently
    // (as RebuildRemembered still does - see its own doc comment on why that's fine there)
    // would instead let two neighboring quads compute the same shared corner from two
    // slightly different noise inputs, which is exactly what would tear the mist surface at
    // every cell boundary.
    private void RebuildUnknown(HashSet<ExplorationCell> fringe)
    {
        var verticesPerSide = (_maxCellIndex - _minCellIndex) + 2;
        var vertices = new Vector3[verticesPerSide, verticesPerSide];
        var colors = new Color[verticesPerSide, verticesPerSide];
        var cellSize = ExplorationState.CellSizeMeters;

        for (var i = 0; i < verticesPerSide; i++)
        {
            for (var j = 0; j < verticesPerSide; j++)
            {
                var baseX = (_minCellIndex + i) * cellSize;
                var baseZ = (_minCellIndex + j) * cellSize;

                // Fbm's [0, 1] remapped to [-1, 1] first - see TerrainRenderer.TerrainBump's own
                // doc comment for why (otherwise every vertex only ever gets nudged one way).
                // Offsetting the coordinate fed to each successive sample keeps them decorrelated
                // from each other despite sharing one noise field.
                var heightNoise = (float)((MistNoise.Fbm(baseX, baseZ, 3, MistNoiseFrequency) - 0.5) * 2.0);
                var xJitterNoise = (float)((MistNoise.Fbm(baseX + 5000, baseZ, 2, MistNoiseFrequency) - 0.5) * 2.0);
                var zJitterNoise = (float)((MistNoise.Fbm(baseX, baseZ + 5000, 2, MistNoiseFrequency) - 0.5) * 2.0);
                var brightnessNoise = MistNoise.Fbm(baseX + 9000, baseZ + 9000, 2, MistNoiseFrequency * 1.7);

                var x = baseX + (xJitterNoise * MistHorizontalJitterMeters);
                var z = baseZ + (zJitterNoise * MistHorizontalJitterMeters);
                var y = _sampleHeight(baseX, baseZ) + CeilingHeightMeters + (heightNoise * MistHeightJitterMeters);
                vertices[i, j] = new Vector3(x, y, z);

                var brightness = MistBrightnessJitterMin + ((float)brightnessNoise * (MistBrightnessJitterMax - MistBrightnessJitterMin));
                colors[i, j] = UnknownColor * brightness;
            }
        }

        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        var hasAny = false;

        for (var cx = _minCellIndex; cx <= _maxCellIndex; cx++)
        {
            for (var cy = _minCellIndex; cy <= _maxCellIndex; cy++)
            {
                var cell = new ExplorationCell(cx, cy);
                if (_exploration.IsExplored(cell) || fringe.Contains(cell))
                {
                    continue;
                }

                var i = cx - _minCellIndex;
                var j = cy - _minCellIndex;
                AddTriangle(tool, vertices[i, j], colors[i, j], vertices[i + 1, j], colors[i + 1, j], vertices[i, j + 1], colors[i, j + 1]);
                AddTriangle(tool, vertices[i + 1, j], colors[i + 1, j], vertices[i + 1, j + 1], colors[i + 1, j + 1], vertices[i, j + 1], colors[i, j + 1]);
                hasAny = true;
            }
        }

        if (hasAny)
        {
            tool.GenerateNormals();
        }

        _unknownMeshInstance.Mesh = hasAny ? tool.Commit() : null;
    }

    // Scattered cloud billboards over exactly the fringe band RebuildUnknown just excluded
    // from its own mesh - same "a few hand-authored variants, randomly placed/scaled/picked"
    // recipe as MapLoader.ScatterDecorations uses for trees, just floated at ceiling height
    // instead of standing on the ground. Rebuilt from scratch each time (this only runs when
    // newly-explored cells actually changed the fringe's position, same cadence as
    // RebuildUnknown) rather than diffed against the previous fringe - simpler, and the
    // fringe is small enough (a ring around the explored perimeter, not the whole map) that
    // re-scattering it entirely is still cheap.
    private void RebuildClouds(HashSet<ExplorationCell> fringe)
    {
        foreach (var child in _cloudContainer.GetChildren())
        {
            child.QueueFree();
        }

        var cellSize = ExplorationState.CellSizeMeters;
        foreach (var cell in fringe)
        {
            // Seeded from the cell's own coordinates, not a shared Random - the fringe is
            // recomputed (and fully re-scattered) every time it changes shape, so a cell that
            // was on the fringe before and still is should keep looking the same rather than
            // re-rolling into a different arrangement of clouds each time a neighbor cell
            // gets explored.
            var rng = new Random(unchecked((cell.X * 73856093) ^ (cell.Y * 19349663)));
            var count = rng.Next(CloudsPerFringeCellMin, CloudsPerFringeCellMax + 1);
            for (var i = 0; i < count; i++)
            {
                var x = ((cell.X + (float)rng.NextDouble()) * cellSize) - (cellSize / 2f);
                var z = ((cell.Y + (float)rng.NextDouble()) * cellSize) - (cellSize / 2f);
                var scale = CloudMinScale + ((float)rng.NextDouble() * (CloudMaxScale - CloudMinScale));
                var texturePath = CloudTexturePaths[rng.Next(CloudTexturePaths.Length)];

                var sprite = BillboardSprite.Create(texturePath, CloudBaseWorldHeight * scale, UnknownColor);
                sprite.Position = new Vector3(x, _sampleHeight(x, z) + CeilingHeightMeters, z);
                sprite.FlipH = rng.NextDouble() < 0.5;
                _cloudContainer.AddChild(sprite);
            }
        }
    }

    // Flat per-cell quads, independent of each other - unlike the unknown mesh above, there's
    // no continuous mist surface to keep seamless here (it's a thin, unshaded, ground-hugging
    // wash), and this tier is bounded by however much the group has actually explored so far
    // rather than most of the map, so it stays cheap enough to just rebuild plainly every tick.
    private void RebuildRemembered()
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        var hasAny = false;

        foreach (var cell in _exploration.Explored)
        {
            if (!_exploration.IsVisible(cell))
            {
                AddCellQuad(tool, cell, GroundHuggingOffsetMeters, RememberedColor);
                hasAny = true;
            }
        }

        _rememberedMeshInstance.Mesh = hasAny ? tool.Commit() : null;
    }

    private void AddCellQuad(SurfaceTool tool, ExplorationCell cell, float heightOffset, Color color)
    {
        var cellSize = ExplorationState.CellSizeMeters;
        var x0 = cell.X * cellSize;
        var x1 = x0 + cellSize;
        var z0 = cell.Y * cellSize;
        var z1 = z0 + cellSize;

        var a = VertexAt(x0, z0, heightOffset);
        var b = VertexAt(x1, z0, heightOffset);
        var c = VertexAt(x0, z1, heightOffset);
        var d = VertexAt(x1, z1, heightOffset);

        AddTriangle(tool, a, color, b, color, c, color);
        AddTriangle(tool, b, color, d, color, c, color);
    }

    private static void AddTriangle(SurfaceTool tool, Vector3 a, Color colorA, Vector3 b, Color colorB, Vector3 c, Color colorC)
    {
        tool.SetColor(colorA);
        tool.AddVertex(a);
        tool.SetColor(colorB);
        tool.AddVertex(b);
        tool.SetColor(colorC);
        tool.AddVertex(c);
    }

    private Vector3 VertexAt(float x, float z, float heightOffset) =>
        new(x, _sampleHeight(x, z) + heightOffset, z);
}
