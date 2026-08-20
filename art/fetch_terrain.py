#!/usr/bin/env python3
"""
Fetches a real elevation grid for one small patch of Europe and writes it as a
static JSON heightmap the game loads locally - no network access at runtime.

Source: EU-DEM v1.1 (25 m, EPSG:3035 native) via the public OpenTopoData API,
per docs/terrain-and-world-scale-architecture.md. This script is a one-time
data-prep step, the same role art/generate_sprites.py plays for pixel art.

Run:  python3 fetch_terrain.py <center_lat> <center_lon> <output_dir> [size_m] [cell_m]
"""

import sys
import os
import json
import time
import math
import urllib.request

API_URL = "https://api.opentopodata.org/v1/eudem25m"
BATCH_SIZE = 100
REQUEST_DELAY_SECONDS = 1.0
METERS_PER_DEGREE_LAT = 111_320.0


def meters_per_degree_lon(lat_degrees):
    return METERS_PER_DEGREE_LAT * math.cos(math.radians(lat_degrees))


def build_grid(center_lat, center_lon, size_m, cell_m):
    half = size_m / 2
    offsets = []
    d = -half
    while d <= half + 1e-6:
        offsets.append(d)
        d += cell_m

    lon_scale = meters_per_degree_lon(center_lat)
    points = []
    for north_m in offsets:
        row = []
        for east_m in offsets:
            lat = center_lat + (north_m / METERS_PER_DEGREE_LAT)
            lon = center_lon + (east_m / lon_scale)
            row.append((lat, lon))
        points.append(row)
    return points, offsets


def fetch_elevations(flat_points):
    elevations = []
    for start in range(0, len(flat_points), BATCH_SIZE):
        batch = flat_points[start:start + BATCH_SIZE]
        locations = "|".join(f"{lat:.6f},{lon:.6f}" for lat, lon in batch)
        url = f"{API_URL}?locations={locations}"
        with urllib.request.urlopen(url, timeout=30) as response:
            data = json.loads(response.read().decode("utf-8"))
        if data.get("status") != "OK":
            raise RuntimeError(f"OpenTopoData error: {data}")
        elevations.extend(r["elevation"] for r in data["results"])
        print(f"fetched {len(elevations)}/{len(flat_points)}")
        if start + BATCH_SIZE < len(flat_points):
            time.sleep(REQUEST_DELAY_SECONDS)
    return elevations


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        sys.exit(1)

    center_lat = float(sys.argv[1])
    center_lon = float(sys.argv[2])
    out_dir = sys.argv[3]
    size_m = float(sys.argv[4]) if len(sys.argv) > 4 else 1000.0
    cell_m = float(sys.argv[5]) if len(sys.argv) > 5 else 25.0

    grid, offsets = build_grid(center_lat, center_lon, size_m, cell_m)
    flat_points = [p for row in grid for p in row]

    print(f"Fetching {len(flat_points)} elevation samples ({len(offsets)}x{len(offsets)} grid, "
          f"{cell_m} m cells, {size_m} m across) centered at {center_lat}, {center_lon}...")
    elevations = fetch_elevations(flat_points)

    grid_size = len(offsets)
    heights = [elevations[row * grid_size:(row + 1) * grid_size] for row in range(grid_size)]

    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, "heightmap.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump({
            "source": "EU-DEM v1.1 (25 m) via OpenTopoData",
            "centerLatitude": center_lat,
            "centerLongitude": center_lon,
            "cellSizeMeters": cell_m,
            "gridSize": grid_size,
            "heights": heights,
        }, f, indent=2)

    flat = [h for row in heights for h in row]
    print(f"wrote {out_path}")
    print(f"elevation range: {min(flat):.1f} m - {max(flat):.1f} m")


if __name__ == "__main__":
    main()
