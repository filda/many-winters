using System.Text.Json;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Maps;

// Where water and bare rock lie on the terrain patch, for placement only: nothing is spawned
// in a river and rock outcrops are always rocky. Height stays a purely visual matter.
public sealed class TerrainFeatures
{
    // A cliff is mapped as its top edge line; the rock face and the scree at its foot are a
    // strip, not a line.
    private const double CliffMarginMeters = 3;

    private const string FileName = "features.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly List<Area> _waterAreas;
    private readonly List<Area> _rockAreas;
    private readonly List<Strip> _waterStrips;
    private readonly List<Strip> _cliffStrips;

    private TerrainFeatures(List<Waterway> waterways, List<Area> waterAreas, List<Area> rockAreas, List<Strip> cliffStrips)
    {
        Waterways = waterways;
        _waterAreas = waterAreas;
        _rockAreas = rockAreas;
        _cliffStrips = cliffStrips;
        _waterStrips = waterways.Select(waterway => new Strip(waterway.Points.ToArray(), waterway.WidthMeters / 2)).ToList();
    }

    public static TerrainFeatures None { get; } = new([], [], [], []);

    public IReadOnlyList<Waterway> Waterways { get; }

    // The terrain catalog folder also yields the heightmap, so the one document that matters is
    // picked by name. A second patch needs a real decision about which one the world uses.
    public static TerrainFeatures LoadFromJson(IEnumerable<(string Source, string Json)> documents)
    {
        var features = documents
            .Where(document => Path.GetFileName(document.Source) == FileName)
            .ToList();

        if (features.Count == 0)
        {
            return None;
        }

        if (features.Count > 1)
        {
            throw new InvalidDataException(
                $"Expected one '{FileName}' but found {features.Count}: {string.Join(", ", features.Select(f => $"'{f.Source}'"))}.");
        }

        var (source, json) = features[0];
        FeaturesData data;
        try
        {
            data = JsonSerializer.Deserialize<FeaturesData>(json, JsonOptions)
                ?? throw new InvalidDataException($"Terrain features '{source}' could not be parsed.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Terrain features '{source}' could not be parsed.", exception);
        }

        return new TerrainFeatures(
            (data.Waterways ?? []).Select(w => new Waterway(w.Name ?? string.Empty, w.WidthMeters, ToPositions(w.Points))).ToList(),
            (data.WaterAreas ?? []).Select(a => new Area(a.Rings.Select(ToPositions).ToList())).ToList(),
            (data.RockAreas ?? []).Select(a => new Area(a.Rings.Select(ToPositions).ToList())).ToList(),
            (data.Cliffs ?? []).Select(c => new Strip(ToPositions(c.Points), CliffMarginMeters)).ToList());
    }

    // Inside a water area, or within half a waterway's width of its centerline.
    public bool IsWater(Position position) =>
        IsWaterArea(position) || _waterStrips.Any(strip => strip.Reaches(position));

    // Water areas alone, without the waterways: a river a few metres wide is drawn along its
    // centerline, which a grid of cells can only render as a jagged staircase.
    public bool IsWaterArea(Position position) => _waterAreas.Any(area => area.Contains(position));

    // How far inside the nearest water area's edge a point lies, negative outside it: what a
    // renderer needs to draw that edge smooth instead of along the cells of whatever grid it
    // samples on. Walks every edge of every ring, so it is meant for points near an edge.
    public double WaterAreaSignedDistance(Position position) =>
        _waterAreas.Select(area => area.SignedDistance(position)).DefaultIfEmpty(double.NegativeInfinity).Max();

    // As WaterAreaSignedDistance, over rock areas and the cliff strips together.
    public double RockSignedDistance(Position position) =>
        _rockAreas.Select(area => area.SignedDistance(position))
            .Concat(_cliffStrips.Select(strip => strip.SignedDistance(position)))
            .DefaultIfEmpty(double.NegativeInfinity)
            .Max();

    // Inside a rock area, or within the margin of a cliff line.
    public bool IsRock(Position position) =>
        _rockAreas.Any(area => area.Contains(position)) || _cliffStrips.Any(strip => strip.Reaches(position));

    private static Position[] ToPositions(double[][] points) =>
        points.Select(point => new Position(point[0], point[1])).ToArray();

    private static double DistanceToSegment(Position p, Position a, Position b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        var t = lengthSquared == 0
            ? 0
            : Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared, 0, 1);
        var ex = p.X - (a.X + (t * dx));
        var ey = p.Y - (a.Y + (t * dy));
        return Math.Sqrt((ex * ex) + (ey * ey));
    }

    private readonly record struct Bounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        internal static Bounds Of(IEnumerable<Position> points, double margin)
        {
            var minX = double.PositiveInfinity;
            var minY = double.PositiveInfinity;
            var maxX = double.NegativeInfinity;
            var maxY = double.NegativeInfinity;
            foreach (var point in points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new Bounds(minX - margin, minY - margin, maxX + margin, maxY + margin);
        }

        internal bool Contains(Position position) =>
            position.X >= MinX && position.X <= MaxX && position.Y >= MinY && position.Y <= MaxY;
    }

    public sealed record Waterway(string Name, double WidthMeters, IReadOnlyList<Position> Points);

    // Rings are read with the even-odd rule: a point is inside when it is inside an odd number
    // of them, so an inner ring is a hole without being marked as one.
    private sealed class Area
    {
        private readonly List<Position[]> _rings;
        private readonly Bounds _bounds;

        internal Area(List<Position[]> rings)
        {
            _rings = rings;
            _bounds = Bounds.Of(rings.SelectMany(ring => ring), 0);
        }

        internal bool Contains(Position position)
        {
            if (!_bounds.Contains(position))
            {
                return false;
            }

            var crossings = 0;
            foreach (var ring in _rings)
            {
                for (var i = 0; i < ring.Length; i++)
                {
                    var a = ring[i];
                    var b = ring[(i + ring.Length - 1) % ring.Length];
                    if ((a.Y > position.Y) == (b.Y > position.Y))
                    {
                        continue;
                    }

                    var crossingX = a.X + ((position.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X));
                    if (position.X < crossingX)
                    {
                        crossings++;
                    }
                }
            }

            return crossings % 2 == 1;
        }

        internal double SignedDistance(Position position)
        {
            var nearest = double.PositiveInfinity;
            foreach (var ring in _rings)
            {
                for (var i = 0; i < ring.Length - 1; i++)
                {
                    nearest = Math.Min(nearest, DistanceToSegment(position, ring[i], ring[i + 1]));
                }
            }

            return Contains(position) ? nearest : -nearest;
        }
    }

    // A polyline thickened by a reach on both sides.
    private sealed class Strip
    {
        private readonly Position[] _points;
        private readonly double _reach;
        private readonly Bounds _bounds;

        internal Strip(Position[] points, double reach)
        {
            _points = points;
            _reach = reach;
            _bounds = Bounds.Of(points, reach);
        }

        internal bool Reaches(Position position) => _bounds.Contains(position) && DistanceTo(position) <= _reach;

        // Positive within the reach, negative beyond it.
        internal double SignedDistance(Position position) => _reach - DistanceTo(position);

        private double DistanceTo(Position position)
        {
            var nearest = double.PositiveInfinity;
            for (var i = 0; i < _points.Length - 1; i++)
            {
                nearest = Math.Min(nearest, DistanceToSegment(position, _points[i], _points[i + 1]));
            }

            return nearest;
        }
    }

    // Instantiated by JsonSerializer, which InspectCode doesn't see as usage.
    // ReSharper disable ClassNeverInstantiated.Local
    private sealed record FeaturesData(WaterwayData[]? Waterways, AreaData[]? WaterAreas, AreaData[]? RockAreas, LineData[]? Cliffs);

    private sealed record WaterwayData(string? Name, double WidthMeters, double[][] Points);

    private sealed record AreaData(double[][][] Rings);

    private sealed record LineData(double[][] Points);

    // ReSharper restore ClassNeverInstantiated.Local
}
