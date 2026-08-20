#!/usr/bin/env python3
"""
Fetches real waterway geometry (rivers/streams/canals) from OpenStreetMap for the
same patch fetch_terrain.py covers, and writes it as local-meter polylines the game
loads statically - no network access at runtime.

Source: OpenStreetMap contributors, via the public Overpass API (ODbL license).
Tunnel/culverted segments are dropped - they aren't a visible surface feature.

Run:  python3 fetch_stream.py <center_lat> <center_lon> <half_size_m> <output_dir>
"""

import sys
import os
import json
import math
import urllib.request
import urllib.parse

METERS_PER_DEGREE_LAT = 111_320.0
LINEAR_WATERWAYS = {"river", "stream", "canal", "drain"}


def meters_per_degree_lon(lat_degrees):
    return METERS_PER_DEGREE_LAT * math.cos(math.radians(lat_degrees))


def to_local(lat, lon, center_lat, center_lon, lon_scale):
    east = (lon - center_lon) * lon_scale
    north = (lat - center_lat) * METERS_PER_DEGREE_LAT
    return [east, north]


def main():
    if len(sys.argv) < 5:
        print(__doc__)
        sys.exit(1)

    center_lat = float(sys.argv[1])
    center_lon = float(sys.argv[2])
    half_size_m = float(sys.argv[3])
    out_dir = sys.argv[4]

    lon_scale = meters_per_degree_lon(center_lat)
    half_lat = half_size_m / METERS_PER_DEGREE_LAT
    half_lon = half_size_m / lon_scale
    south, north = center_lat - half_lat, center_lat + half_lat
    west, east = center_lon - half_lon, center_lon + half_lon

    query = f'[out:json][timeout:25];way["waterway"]({south},{west},{north},{east});out geom;'
    url = "https://overpass-api.de/api/interpreter?" + urllib.parse.urlencode({"data": query})
    print(f"Querying Overpass for waterways in the {half_size_m * 2:.0f} m patch...")
    request = urllib.request.Request(url, headers={"User-Agent": "many-winters-terrain-research"})
    with urllib.request.urlopen(request, timeout=30) as response:
        data = json.loads(response.read().decode("utf-8"))

    polylines = []
    for element in data.get("elements", []):
        tags = element.get("tags", {})
        waterway = tags.get("waterway")
        if waterway not in LINEAR_WATERWAYS or tags.get("tunnel") == "yes":
            continue

        points = [to_local(p["lat"], p["lon"], center_lat, center_lon, lon_scale)
                  for p in element.get("geometry", [])]
        if len(points) < 2:
            continue

        width_raw = tags.get("width", "3").rstrip("m ").strip()
        try:
            width_m = float(width_raw)
        except ValueError:
            width_m = 3.0

        polylines.append({
            "name": tags.get("name", waterway),
            "waterway": waterway,
            "widthMeters": width_m,
            "points": points,
        })

    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, "waterways.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump({
            "source": "OpenStreetMap contributors (ODbL), via Overpass API",
            "centerLatitude": center_lat,
            "centerLongitude": center_lon,
            "polylines": polylines,
        }, f, indent=2)

    print(f"wrote {out_path} with {len(polylines)} waterway segment(s)")
    for p in polylines:
        print(f"  {p['name']} ({p['waterway']}, {p['widthMeters']} m wide, {len(p['points'])} points)")


if __name__ == "__main__":
    main()
