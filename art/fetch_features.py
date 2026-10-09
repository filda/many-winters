#!/usr/bin/env python3
"""
Fetches the surface features of the patch fetch_terrain.py covers from OpenStreetMap and
writes them as local-metre geometry the game loads statically - no network access at runtime.

- waterways: river and stream centerlines with a width (tunnelled segments dropped; they are
  not a visible surface feature)
- waterAreas: lakes, ponds and river surfaces (natural=water), as rings
- rockAreas: bare rock, scree and shingle (natural=bare_rock|scree|shingle), as rings
- cliffs: natural=cliff lines

Areas are lists of rings read with the even-odd rule, so a multipolygon's inner rings (an
island in a pond) are holes without being marked as such.

Only what is stable over millennia is taken. Modern land cover (forest, grass, scrub) is
today's city, not a prehistoric landscape, and is left out on purpose, as are plainly
engineered waters: canals, drains, retention basins and sewage ponds.

A ring lying wholly outside the patch is dropped: a river's multipolygon reaches kilometres
past it, and a ring that far away cannot change whether a point inside the patch is wet.

Source: OpenStreetMap contributors via the public Overpass API (ODbL).

Run:  python3 fetch_features.py <center_lat> <center_lon> <half_size_m> <output_dir>
"""

import sys
import os
import json
import math
import urllib.request
import urllib.parse

METERS_PER_DEGREE_LAT = 111_320.0
LINEAR_WATERWAYS = {"river", "stream"}
ENGINEERED_WATERS = {"basin", "wastewater"}
ROCK_SURFACES = {"bare_rock", "scree", "shingle"}
DEFAULT_WATERWAY_WIDTH_M = 3.0


def meters_per_degree_lon(lat_degrees):
    return METERS_PER_DEGREE_LAT * math.cos(math.radians(lat_degrees))


def to_local(point, center_lat, center_lon, lon_scale):
    east = (point["lon"] - center_lon) * lon_scale
    north = (point["lat"] - center_lat) * METERS_PER_DEGREE_LAT
    return [round(east, 2), round(north, 2)]


def join_rings(ways):
    """Stitches a multipolygon's member ways (each a list of points) into closed rings.

    OSM splits one ring over several ways in any order and direction; a way that cannot be
    closed (a member cut off by the editor) is dropped rather than closed with a false edge.
    """
    pending = [list(w) for w in ways if len(w) >= 2]
    rings = []
    while pending:
        ring = pending.pop(0)
        while ring[0] != ring[-1]:
            for i, way in enumerate(pending):
                if way[0] == ring[-1]:
                    ring.extend(way[1:])
                elif way[-1] == ring[-1]:
                    ring.extend(reversed(way[:-1]))
                else:
                    continue
                pending.pop(i)
                break
            else:
                break
        if ring[0] == ring[-1] and len(ring) >= 4:
            rings.append(ring)
    return rings


def touches_patch(ring, half_size_m):
    xs = [p[0] for p in ring]
    ys = [p[1] for p in ring]
    return max(xs) >= -half_size_m and min(xs) <= half_size_m and max(ys) >= -half_size_m and min(ys) <= half_size_m


def area_rings(element):
    if element["type"] == "way":
        return join_rings([element.get("geometry", [])])
    return join_rings([m.get("geometry", []) for m in element.get("members", [])
                       if m.get("type") == "way" and m.get("role") in ("outer", "inner")])


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
    bbox = f"{center_lat - half_lat},{center_lon - half_lon},{center_lat + half_lat},{center_lon + half_lon}"

    query = (
        "[out:json][timeout:60];("
        f'way["waterway"]({bbox});'
        f'nwr["natural"~"^(water|bare_rock|scree|shingle|cliff)$"]({bbox});'
        ");out geom;"
    )
    url = "https://overpass-api.de/api/interpreter?" + urllib.parse.urlencode({"data": query})
    print(f"Querying Overpass for surface features in the {half_size_m * 2:.0f} m patch...")
    request = urllib.request.Request(url, headers={"User-Agent": "many-winters-terrain-research"})
    with urllib.request.urlopen(request, timeout=90) as response:
        data = json.loads(response.read().decode("utf-8"))

    def local(points):
        return [to_local(p, center_lat, center_lon, lon_scale) for p in points]

    waterways, water_areas, rock_areas, cliffs = [], [], [], []
    for element in data.get("elements", []):
        tags = element.get("tags", {})
        natural = tags.get("natural")
        waterway = tags.get("waterway")

        if waterway in LINEAR_WATERWAYS and element["type"] == "way":
            if tags.get("tunnel") in ("yes", "culvert"):
                continue
            points = local(element.get("geometry", []))
            if len(points) < 2:
                continue
            try:
                width_m = float(tags.get("width", "").rstrip("m ").strip())
            except ValueError:
                width_m = DEFAULT_WATERWAY_WIDTH_M
            waterways.append({
                "name": tags.get("name", waterway),
                "waterway": waterway,
                "widthMeters": width_m,
                "points": points,
            })
        elif natural == "water" or natural in ROCK_SURFACES:
            hidden = tags.get("covered") == "yes" or tags.get("location") == "underground"
            if hidden or tags.get("water") in ENGINEERED_WATERS:
                continue
            rings = [ring for ring in (local(r) for r in area_rings(element)) if touches_patch(ring, half_size_m)]
            if not rings:
                continue
            feature = {"name": tags.get("name", ""), "kind": tags.get("water", natural), "rings": rings}
            (water_areas if natural == "water" else rock_areas).append(feature)
        elif natural == "cliff" and element["type"] == "way":
            points = local(element.get("geometry", []))
            if len(points) >= 2:
                cliffs.append({"name": tags.get("name", ""), "points": points})

    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, "features.json")
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump({
            "source": "OpenStreetMap contributors (ODbL), via Overpass API",
            "centerLatitude": center_lat,
            "centerLongitude": center_lon,
            "waterways": waterways,
            "waterAreas": water_areas,
            "rockAreas": rock_areas,
            "cliffs": cliffs,
        }, f, ensure_ascii=False, separators=(",", ":"))
        f.write("\n")

    print(f"wrote {out_path}: {len(waterways)} waterway(s), {len(water_areas)} water area(s), "
          f"{len(rock_areas)} rock area(s), {len(cliffs)} cliff(s)")
    for group in (waterways, water_areas, rock_areas, cliffs):
        for item in group:
            print(f"  {item.get('kind') or item.get('waterway') or 'cliff'}: {item['name'] or '-'}")


if __name__ == "__main__":
    main()
