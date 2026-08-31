#!/usr/bin/env python3
"""
Generates woodcut-style sprites for the ManyWinters Godot prototype: hand-inked
crosshatch shading (line density carries tone, not a blended gradient) plus base
silhouettes built from more than one primitive, in the vein of Karel Zeman's engraved
paper-cutout dioramas (see docs/ZemanConceptArt.png, docs/ZemanSprites.png and
art/zeman-sprite-prompts.md for the target look).

Drawn at 4x the old 64x64 grid (see SCALE) so the hatch lines are actually visible
rather than single hard pixels - BillboardSprite.cs already renders with mipmapped
linear filtering in anticipation of exactly this kind of fine engraved detail, so no
engine change is needed to drop in a higher-resolution texture.

Run:  python3 generate_sprites.py <output_dir>
"""

import sys
import os
import math
import random
import zlib
import numpy as np
from PIL import Image, ImageDraw

SCALE = 4
S = 64 * SCALE  # canvas size; shape coordinates below are still authored on the old 64-unit grid

# Where every ground-standing shape's lowest point (trunk, stem, base ellipse...) should sit -
# close to the canvas's own bottom edge (64), not comfortably above it. The engine positions
# each sprite's shadow at the sprite's nominal canvas-bottom = true ground level (see
# TerrainRenderer.ScatterDecoration's own comment on why it doesn't try to compensate for a
# margin here instead - that needs the sprite's own *billboard-local* Y, which doesn't equal a
# real world-space Y from an oblique camera). A base authored well above the canvas edge (the
# original 58 many of these used) reads as the object floating above its own shadow instead.
GROUND_CONTACT_Y = 63


# ---------------------------------------------------------------- colour utils

def rgb(*c):
    return tuple(int(round(v * 255)) if isinstance(v, float) else int(v) for v in c)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def lighten(c, t):
    return mix(c, (255, 255, 255), t)


def darken(c, t):
    return mix(c, (0, 0, 0), t)


def seed_for(name):
    """Stable seed derived from the sprite's own name, so re-running the generator
    always reproduces the same output instead of depending on hash randomisation."""
    return zlib.crc32(name.encode()) & 0xFFFF


# ---------------------------------------------------------------- mask helpers

def _blank():
    return Image.new("L", (S, S), 0)


def _to_mask(img):
    return np.array(img) > 127


def ellipse(cx, cy, rx, ry):
    cx, cy, rx, ry = (v * SCALE for v in (cx, cy, rx, ry))
    img = _blank()
    ImageDraw.Draw(img).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
    return _to_mask(img)


def rect(x0, y0, x1, y1):
    x0, y0, x1, y1 = (v * SCALE for v in (x0, y0, x1, y1))
    img = _blank()
    ImageDraw.Draw(img).rectangle([x0, y0, x1, y1], fill=255)
    return _to_mask(img)


def poly(points):
    points = [(x * SCALE, y * SCALE) for x, y in points]
    img = _blank()
    ImageDraw.Draw(img).polygon(points, fill=255)
    return _to_mask(img)


def shift(mask, dx, dy):
    out = np.zeros_like(mask)
    xs = slice(max(0, dx), S + min(0, dx))
    ys = slice(max(0, dy), S + min(0, dy))
    sxs = slice(max(0, -dx), S + min(0, -dx))
    sys_ = slice(max(0, -dy), S + min(0, -dy))
    out[ys, xs] = mask[sys_, sxs]
    return out


def dilate(mask, r=1):
    out = mask.copy()
    for _ in range(r):
        nxt = out.copy()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nxt |= shift(out, dx, dy)
        out = nxt
    return out


def erode(mask, r=1):
    return ~dilate(~mask, r)


# ---------------------------------------------------------------- jagged shapes
# Turns a clean vector polygon into a hand-cut/torn-paper edge. Deliberately NOT used
# on shapes that are already built from several unioned primitives (apple, potato,
# grave mounds, rock piles, fruit-tree canopies) - stacking edge noise on top of a
# union-of-blobs is what made an early tree prototype look "gnawed by mice" rather than
# hand-drawn. For those, the lopsidedness comes from off-centre primitives instead, and
# rough_outline() (which works on the final raster silhouette regardless of how it was
# built) supplies the hand-inked edge on top.

def jagged_poly(points, rng, amp=1.6, segments_per_edge=5, smooth_passes=2):
    n = len(points)
    pts = []
    for i in range(n):
        p0 = np.array(points[i], dtype=float)
        p1 = np.array(points[(i + 1) % n], dtype=float)
        edge = p1 - p0
        length = np.hypot(*edge) or 1.0
        normal = np.array([-edge[1], edge[0]]) / length
        for t in np.linspace(0.0, 1.0, segments_per_edge, endpoint=False):
            base = p0 + edge * t
            # corners wobble less than mid-edge points, so proportions stay recognisable
            edge_bias = min(t, 1.0 - t) * 2.0
            offset = (rng.random() - 0.5) * 2 * amp * edge_bias
            pts.append(base + normal * offset)
    pts = np.array(pts)
    for _ in range(smooth_passes):
        pts = (np.roll(pts, 1, axis=0) + pts + np.roll(pts, -1, axis=0)) / 3.0
    return [tuple(p) for p in pts]


def lobe_cluster_mask(apex, base_left, base_right, rng, rows=3):
    """A cluster of small pointed sprigs scattered across a triangle's footprint and
    unioned/closed together - reads as a clump of branches, not a geometric cone.
    Tuned conservatively (few, big, heavily-overlapping lobes; strong closing) after an
    earlier pass with many small sharp lobes looked chewed-on rather than clumped."""
    apex_x, apex_y = apex
    base_y = base_left[1]
    base_cx = (base_left[0] + base_right[0]) / 2.0
    half_w_base = (base_right[0] - base_left[0]) / 2.0

    mask = np.zeros((S, S), dtype=bool)
    for row in range(rows):
        t = (row + 0.6) / rows
        y = apex_y + (base_y - apex_y) * t
        half_w = max(half_w_base * t, 1.5)
        cx = apex_x + (base_cx - apex_x) * t
        n_lobes = 1 if row == 0 else max(2, round(2 + t * rng.uniform(1.2, 2.0)))
        lobe_r = max(1.8, (half_w * 2 / n_lobes) * 1.15)
        for i in range(n_lobes):
            frac = ((i + 0.5) / n_lobes) * 2 - 1
            lx = cx + frac * half_w + rng.uniform(-lobe_r * 0.2, lobe_r * 0.2)
            ly = y + rng.uniform(-lobe_r * 0.25, lobe_r * 0.25)
            sprig = [
                (lx - lobe_r, ly + lobe_r * 0.7),
                (lx, ly - lobe_r * 1.4),
                (lx + lobe_r, ly + lobe_r * 0.7),
            ]
            sprig = jagged_poly(sprig, rng, amp=lobe_r * 0.10, segments_per_edge=3, smooth_passes=2)
            mask |= poly(sprig)
    close_r = max(3, SCALE * 2)
    return erode(dilate(mask, close_r), close_r)


# ---------------------------------------------------------------- hand-drawn hatching
# Each hatch line gets its own stable seeded "personality" (lateral offset, curvature,
# thickness, and mid-stroke breaks) instead of every line reacting to one shared noise
# field - a shared field made every line bend in lockstep, which read as corrugated
# sheet metal rather than a hand-ruled crosshatch.

_YY, _XX = np.mgrid[0:S, 0:S]
INK = rgb(0.14, 0.10, 0.08)
PERIOD = 6 * SCALE // 4


def _line_hash(idx, salt):
    h = (idx.astype(np.int64) * np.int64(2654435761) + np.int64(salt) * np.int64(40503)) & 0xFFFFFFFF
    h = (h ^ (h >> np.int64(13))) & 0xFFFFFFFF
    return (h % 10007) / 10007.0


def _hatch_direction(diag_coord, along_coord, tone, density_scale, tone_offset, salt_base):
    raw_idx = np.floor(diag_coord / PERIOD).astype(np.int64)
    lateral = (_line_hash(raw_idx, salt_base + 0) - 0.5) * 2.2
    freq = 0.010 + _line_hash(raw_idx, salt_base + 1) * 0.018
    phase = _line_hash(raw_idx, salt_base + 2) * 2 * np.pi
    curve_amp = 1.5 + _line_hash(raw_idx, salt_base + 3) * 2.0
    thickness_jitter = 0.55 + _line_hash(raw_idx, salt_base + 4) * 0.9

    curve = np.sin(along_coord * freq + phase) * curve_amp
    wobbled = (diag_coord - lateral - curve) % PERIOD

    # a hand-ruled hatch doesn't run edge-to-edge unbroken - chop into segments and
    # randomly skip some, like a pen lifting mid-stroke
    seg_len = 10.0
    seg_idx = np.floor(along_coord / seg_len).astype(np.int64)
    combined = raw_idx * np.int64(100003) + seg_idx
    drawn = _line_hash(combined, salt_base + 5) > 0.16

    width = np.clip(tone - tone_offset, 0.0, 1.0) * PERIOD * density_scale * thickness_jitter
    return drawn & (wobbled < width)


def hatch_fill(mask, base_color, seed):
    """Fills mask with base_color plus ink crosshatching whose density follows a
    diagonal light gradient (upper-left lit, lower-right shadowed) - tone comes from
    line density, not a blended/painterly gradient (the woodcut style explicitly rules
    painterly gradients out)."""
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return np.zeros((S, S, 3), dtype=np.uint8)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    diag = max((x1 - x0) + (y1 - y0), 1)
    # capped short of 1.0 so the darkest corner still shows a hint of base colour
    # instead of crushing to solid ink
    tone = np.clip(((_XX - x0) + (_YY - y0)) / diag, 0.0, 1.0) * 0.86

    salt = (seed % 97) * 11
    line_a = _hatch_direction(_XX - _YY, _XX + _YY, tone, 1.3, 0.12, salt + 1)
    line_b = _hatch_direction(_XX + _YY, _XX - _YY, tone, 2.0, 0.55, salt + 11)
    ink_mask = mask & (line_a | line_b)

    out_rgb = np.zeros((S, S, 3), dtype=np.uint8)
    out_rgb[mask] = base_color
    out_rgb[ink_mask] = INK
    return out_rgb


class Canvas:
    def __init__(self, seed):
        self.seed = seed
        self.rgb = np.zeros((S, S, 3), dtype=np.uint8)
        self.alpha = np.zeros((S, S), dtype=bool)

    def fill(self, mask, base_color):
        """Base colour plus hand-drawn crosshatch shading - use for anything with
        enough area to show tone."""
        out_rgb = hatch_fill(mask, base_color, self.seed)
        self.rgb[mask] = out_rgb[mask]
        self.alpha |= mask

    def flat(self, mask, color):
        """Plain flat fill, no hatching - for accents too small for line density to
        read (a glint, a seam, a rune mark)."""
        self.rgb[mask] = color
        self.alpha |= mask

    def rough_outline(self, width=2):
        """A hand-inked contour: an uneven ring that thins and thickens in patches,
        not a uniform machine-drawn outline."""
        ring = dilate(self.alpha, width) & ~self.alpha
        idx = np.floor((_XX + _YY) / 3).astype(np.int64)
        keep = _line_hash(idx, self.seed * 13 + 7) > 0.22
        ring &= keep
        self.rgb[ring] = INK
        self.alpha |= ring

    def image(self):
        out = np.zeros((S, S, 4), dtype=np.uint8)
        out[..., :3] = self.rgb
        out[..., 3] = np.where(self.alpha, 255, 0)
        return Image.fromarray(out, "RGBA")


# ---------------------------------------------------------------- trunk/canopy split
# A tree's trunk needs to stay fully solid when the camera's occlusion fade ghosts
# whatever's standing between the camera and the selected person, while its canopy is
# exactly what should fade (see docs/Camera.png and Main.UpdateOcclusionFade). Building
# one combined, hand-inked Canvas exactly as before and then partitioning its *final*
# pixels between two layers - rather than inking each layer separately - keeps the
# split a pure rendering-time concern: stacking both layers back together reproduces
# today's single flattened tree image pixel-for-pixel.
_tree_split_cache = {}


def split_trunk_canopy(name, trunk_mask, canopy_masks, trunk_color, canopy_color):
    """canopy_masks: one or more pieces filled in order (each its own Canvas.fill call),
    not pre-unioned - a conifer's tiers each need their own hatch tone gradient computed
    across their own footprint rather than one gradient across the whole canopy's
    bounding box (see hatch_fill), exactly like the original single-Canvas conifer_tree
    did. A tree with just one canopy piece (the fruit trees) passes a single-item list."""
    if name in _tree_split_cache:
        return _tree_split_cache[name]

    seed = seed_for(name)
    outline_width = max(1, SCALE // 2)
    combined = Canvas(seed)
    combined.fill(trunk_mask, trunk_color)
    canopy_mask = np.zeros((S, S), dtype=bool)
    for mask in canopy_masks:
        combined.fill(mask, canopy_color)
        canopy_mask |= mask
    combined.rough_outline(width=outline_width)

    # Canopy is filled second above, so it already wins any genuine geometric overlap
    # (e.g. a low canopy lobe overhanging the trunk's top) - trunk keeps only what
    # canopy never touches. rough_outline's added ink ring is split the same way: a
    # ring pixel counts as trunk's own edge only if it's near the trunk silhouette and
    # NOT also near canopy - an ambiguous seam pixel goes to canopy, since that's what
    # visually sits on top there.
    trunk_only = trunk_mask & ~canopy_mask
    ring = combined.alpha & ~(trunk_mask | canopy_mask)
    trunk_ring = ring & dilate(trunk_mask, outline_width) & ~dilate(canopy_mask, outline_width)
    trunk_alpha = trunk_only | trunk_ring
    canopy_alpha = combined.alpha & ~trunk_alpha

    trunk_canvas = Canvas(seed)
    trunk_canvas.rgb = combined.rgb
    trunk_canvas.alpha = trunk_alpha

    canopy_canvas = Canvas(seed)
    canopy_canvas.rgb = combined.rgb
    canopy_canvas.alpha = canopy_alpha

    result = (combined, trunk_canvas, canopy_canvas)
    _tree_split_cache[name] = result
    return result


# ---------------------------------------------------------------- richer base shapes
# (person) - a nipped-waist, scalloped-hem robe; a tapered bent arm; a hood with an
# actual cowl point; a boot with a heel and a toe - built as multi-point silhouettes
# instead of a trapezoid/quad/ellipse-ring/rectangle before any jagging is applied.

# Three hand-authored robe silhouettes, all 13 points walked in the same order
# (shoulder_L, shoulder_R, arm_notch_R, waist_R, hip_R, hem_R1, hem_R2, hem_center,
# hem_L2, hem_L1, hip_L, waist_L, arm_notch_L). Each is a deliberately different,
# asymmetric drape - not mirror-symmetric, not a formula - the way cloth actually
# falls unevenly, rather than noise jittered onto a symmetric trapezoid (which read as
# "primitive with texture" no matter how smooth/jagged its edge was - see
# project_sprite_woodcut_texture_library memory for the wireframe test that ruled out
# edge smoothness as the cause). Blending between these per instance keeps the
# per-seed variety while keeping each result built from a genuinely asymmetric shape.
_ROBE_VARIANT_A = [
    (25, 19), (39, 21), (43, 26), (41, 36), (46, 45),
    (45, 53), (38, 50), (31, 55), (24, 49), (19, 52),
    (19, 44), (23, 33), (21, 24),
]
_ROBE_VARIANT_B = [
    (24, 21), (38, 19), (42, 25), (39, 34), (42, 43),
    (40, 49), (34, 52), (29, 48), (22, 53), (17, 50),
    (18, 46), (25, 37), (20, 26),
]
_ROBE_VARIANT_C = [
    (26, 20), (40, 20), (44, 27), (42, 35), (45, 47),
    (44, 54), (36, 49), (30, 53), (23, 47), (18, 51),
    (17, 43), (22, 35), (19, 25),
]
ROBE_VARIANTS = [np.array(v, dtype=float) for v in (_ROBE_VARIANT_A, _ROBE_VARIANT_B, _ROBE_VARIANT_C)]


def robe_silhouette(rng, sx, sy):
    weights = np.array([rng.random() for _ in ROBE_VARIANTS])
    weights /= weights.sum()
    blended = sum(w * v for w, v in zip(weights, ROBE_VARIANTS))
    return [(sx(x), sy(y)) for x, y in blended]


def arm_points(side, elbow_bulge):
    """Points listed walking the perimeter in order, so the polygon stays simple
    (non-self-crossing) - a generic mirror-by-multiplier version of this tangled the
    inner/outer edges into a self-intersecting bowtie."""
    if side == "l":
        shoulder_outer, shoulder_inner = 22, 26
        wrist_inner, wrist_outer = 24, 19
        bulge_dir = -1
    else:
        shoulder_outer, shoulder_inner = 42, 38
        wrist_inner, wrist_outer = 40, 45
        bulge_dir = 1
    return [
        (shoulder_outer, 24),
        (shoulder_inner, 24),
        (wrist_inner + bulge_dir * elbow_bulge * 0.4, 34),
        (wrist_inner, 44),
        (wrist_outer, 43),
        (wrist_outer + bulge_dir * elbow_bulge, 34),
    ]


def cowled_hood_mask(rng):
    base = ellipse(32, 13, 10, 10)
    peak = poly(jagged_poly(
        [(24, 8), (32, -1), (40, 8)], rng, amp=0.8, segments_per_edge=3, smooth_passes=1
    ))
    face_hole = ellipse(32, 17, 7, 8)
    return (base | peak) & ~face_hole


# the left boot's silhouette: a cuff, a toe box, and a heel that sticks out past the
# ankle, instead of a flat-bottomed rectangle. The right boot mirrors around x=32
# (64-x), which lines up exactly because the old rect boots (24-31 and 33-40) were
# already symmetric around that centre.
LEFT_BOOT_PTS = [(25, 55), (30, 55), (31, 57), (29, 59), (22, 58), (23, 56)]
RIGHT_BOOT_PTS = [(64 - x, y) for x, y in LEFT_BOOT_PTS]

CLOAK_OPTIONS = [
    rgb(0.34, 0.24, 0.16),  # sepia
    rgb(0.33, 0.36, 0.42),  # slate blue-grey
    rgb(0.47, 0.27, 0.15),  # rust brown
    rgb(0.40, 0.36, 0.20),  # dark ochre
]
SKIN = rgb(0.76, 0.60, 0.47)
BOOT = rgb(0.27, 0.20, 0.16)
HAIR_OPTIONS = [rgb(0.22, 0.16, 0.11), rgb(0.32, 0.22, 0.14), rgb(0.45, 0.40, 0.34)]


def person(seed=None):
    seed = seed_for("person") if seed is None else seed
    rng = random.Random(seed)
    c = Canvas(seed)

    cloak = rng.choice(CLOAK_OPTIONS)
    hair_color = rng.choice(HAIR_OPTIONS)
    build_w = rng.uniform(0.92, 1.1)
    build_h = rng.uniform(0.95, 1.08)
    hood_up = rng.random() > 0.25

    def sx(x, cx=32):
        return cx + (x - cx) * build_w

    def sy(y, top=20):
        return top + (y - top) * build_h

    # legs stay simple rects (thin, identity-critical, jag would just muddy them) but
    # feet get an actual boot silhouette - a heel and a toe, not a flat-bottomed box
    c.fill(rect(26, 46, 30, 56) | rect(34, 46, 38, 56), BOOT)
    boot_l = poly(jagged_poly(LEFT_BOOT_PTS, rng, amp=0.5, segments_per_edge=3, smooth_passes=1))
    boot_r = poly(jagged_poly(RIGHT_BOOT_PTS, rng, amp=0.5, segments_per_edge=3, smooth_passes=1))
    c.fill(boot_l | boot_r, darken(BOOT, 0.25))

    # cloak: an authored asymmetric drape (see ROBE_VARIANTS), lightly jagged on top
    # for hand-cut texture - less noise needed now that the base shape itself is
    # genuinely asymmetric, not a symmetric formula
    body_pts = robe_silhouette(rng, sx, sy)
    body = poly(jagged_poly(body_pts, rng, amp=1.0, segments_per_edge=3, smooth_passes=2))
    c.fill(body, cloak)
    c.flat(rect(24, 38, 40, 39) & body, lighten(cloak, 0.25))

    # arms: tapered with an elbow bulge instead of a straight-sided quad
    elbow_bulge = rng.uniform(0.5, 2.5)
    arm_l = poly(jagged_poly(arm_points("l", elbow_bulge), rng, amp=0.8, segments_per_edge=3))
    arm_r = poly(jagged_poly(arm_points("r", elbow_bulge), rng, amp=0.8, segments_per_edge=3))
    c.fill(arm_l, darken(cloak, 0.12))
    c.fill(arm_r, darken(cloak, 0.12))
    c.fill(ellipse(21, 45, 3, 3), SKIN)
    c.fill(ellipse(43, 45, 3, 3), SKIN)

    # head stays clean (round, small, carries too much identity to distort)
    c.fill(ellipse(32, 15, 8, 9), SKIN)

    if hood_up:
        c.fill(cowled_hood_mask(rng), cloak)
        c.fill(rect(22, 18, 42, 22) & ellipse(32, 13, 10, 11), cloak)
    else:
        c.flat(rect(27, 8, 37, 11) & ellipse(32, 15, 8, 9), hair_color)

    c.flat(rect(28, 15, 29, 16), rgb(0.10, 0.09, 0.10))
    c.flat(rect(35, 15, 36, 16), rgb(0.10, 0.09, 0.10))

    c.rough_outline(width=max(1, SCALE // 2))
    return c


def person_dead():
    """Literally the living sprite: rotated onto its side and drained of colour.

    Reusing the same silhouette is deliberate - at a glance it has to read as
    "that person, but down", not as a separate entity.
    """
    seed = seed_for("person_dead")
    arr = np.array(person().image()).astype(np.float32)
    lum = arr[..., :3] @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    for i, tint in enumerate((0.94, 0.97, 1.08)):
        arr[..., i] = np.clip(lum * 0.85 * tint + 22, 0, 255)
    rot = np.rot90(arr.astype(np.uint8), k=1)

    c = Canvas(seed)
    mask = rot[..., 3] > 127
    c.rgb[mask] = rot[..., :3][mask]
    c.alpha |= mask
    rows = np.flatnonzero(c.alpha.any(axis=1))
    drop = (S - 6 * SCALE) - rows.max()
    c.rgb = np.roll(c.rgb, drop, axis=0)
    c.alpha = np.roll(c.alpha, drop, axis=0)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


# ---------------------------------------------------------------- layered person parts
# (different-looking party members) - person()/person_dead() above stay exactly as they
# were (person_dead() still renders off of person()'s single flat image, unchanged) and
# keep shipping person.png/person_dead.png; these are new, separate layers PersonView
# composites at runtime instead - a bare body plus a swappable hair layer and a
# swappable clothing layer, each drawn in a light neutral tone so Modulate can recolour
# it at runtime (multiplying a light grey by a colour approximates that colour while the
# dark ink hatching stays dark regardless - the same principle BillboardSprite's own
# fallback-colour tinting already relies on, just applied to real baked art instead of a
# flat quad).

BODY_UNDERCLOTHES = rgb(0.55, 0.50, 0.45)
NEUTRAL_RECOLOURABLE = rgb(0.82, 0.80, 0.78)


def _body_layer(gender):
    """Boots, hands, head and a plain covered torso/legs - meant to sit almost entirely
    hidden under a clothing layer, so kept simple rather than elaborately detailed.
    Only the hip width actually differs by gender; everything else about this figure
    already ends up covered by clothing/hair on top of it."""
    seed = seed_for(f"body_{gender}")
    rng = random.Random(seed)
    c = Canvas(seed)

    hip_scale = 1.12 if gender == "female" else 1.0
    hip_l, hip_r = 32 - (10 * hip_scale), 32 + (10 * hip_scale)

    c.fill(rect(26, 46, 30, 56) | rect(34, 46, 38, 56), BOOT)
    boot_l = poly(jagged_poly(LEFT_BOOT_PTS, rng, amp=0.5, segments_per_edge=3, smooth_passes=1))
    boot_r = poly(jagged_poly(RIGHT_BOOT_PTS, rng, amp=0.5, segments_per_edge=3, smooth_passes=1))
    c.fill(boot_l | boot_r, darken(BOOT, 0.25))

    torso_pts = [
        (27, 22), (37, 22), (39, 34), (hip_r, 44),
        (hip_r - 2, 55), (hip_l + 2, 55), (hip_l, 44), (25, 34),
    ]
    torso = poly(jagged_poly(torso_pts, rng, amp=0.8, segments_per_edge=3, smooth_passes=1))
    c.fill(torso, BODY_UNDERCLOTHES)

    elbow_bulge = rng.uniform(0.5, 2.5)
    arm_l = poly(jagged_poly(arm_points("l", elbow_bulge), rng, amp=0.8, segments_per_edge=3))
    arm_r = poly(jagged_poly(arm_points("r", elbow_bulge), rng, amp=0.8, segments_per_edge=3))
    c.fill(arm_l, SKIN)
    c.fill(arm_r, SKIN)
    c.fill(ellipse(21, 45, 3, 3), SKIN)
    c.fill(ellipse(43, 45, 3, 3), SKIN)

    c.fill(ellipse(32, 15, 8, 9), SKIN)
    c.flat(rect(28, 15, 29, 16), rgb(0.10, 0.09, 0.10))
    c.flat(rect(35, 15, 36, 16), rgb(0.10, 0.09, 0.10))

    c.rough_outline(width=max(1, SCALE // 2))
    return c


def person_body_male():
    return _body_layer("male")


def person_body_female():
    return _body_layer("female")


def hair_short():
    """A simple close-cropped cap - the same top-of-head patch person() used when its
    hood was down, just on its own transparent layer."""
    seed = seed_for("hair_short")
    c = Canvas(seed)
    mask = rect(27, 8, 37, 11) & ellipse(32, 15, 8, 9)
    c.flat(mask, NEUTRAL_RECOLOURABLE)
    c.rough_outline(width=1)
    return c


def hair_long():
    seed = seed_for("hair_long")
    rng = random.Random(seed)
    c = Canvas(seed)
    top = rect(27, 8, 37, 11) & ellipse(32, 15, 8, 9)
    left = poly(jagged_poly(
        [(24, 10), (28, 9), (26, 26), (22, 30), (20, 24)],
        rng, amp=0.6, segments_per_edge=2, smooth_passes=1,
    ))
    right = poly(jagged_poly(
        [(40, 10), (36, 9), (38, 26), (42, 30), (44, 24)],
        rng, amp=0.6, segments_per_edge=2, smooth_passes=1,
    ))
    c.fill(top | left | right, NEUTRAL_RECOLOURABLE)
    c.rough_outline(width=1)
    return c


def hair_tied():
    seed = seed_for("hair_tied")
    rng = random.Random(seed)
    c = Canvas(seed)
    top = rect(27, 8, 37, 11) & ellipse(32, 15, 8, 9)
    tail = poly(jagged_poly(
        [(30, 10), (34, 10), (35, 22), (32, 26), (29, 22)],
        rng, amp=0.5, segments_per_edge=2, smooth_passes=1,
    ))
    c.fill(top | tail, NEUTRAL_RECOLOURABLE)
    c.rough_outline(width=1)
    return c


def _clothing_layer(variant_index):
    """One of the three hand-authored robe drapes (see ROBE_VARIANTS) used directly, not
    blended - a discrete "type" of clothing to pick between at runtime rather than
    person()'s continuous per-instance blend."""
    seed = seed_for(f"clothing_{variant_index}")
    rng = random.Random(seed)
    c = Canvas(seed)
    pts = [(x, y) for x, y in ROBE_VARIANTS[variant_index]]
    body = poly(jagged_poly(pts, rng, amp=1.0, segments_per_edge=3, smooth_passes=2))
    c.fill(body, NEUTRAL_RECOLOURABLE)
    c.flat(rect(24, 38, 40, 39) & body, lighten(NEUTRAL_RECOLOURABLE, 0.25))
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def clothing_robe():
    return _clothing_layer(0)


def clothing_tunic():
    return _clothing_layer(1)


def clothing_cloak():
    return _clothing_layer(2)


def _dead_layer_drop():
    """Ground-contact reference shared by every "_dead" layer variant below - computed
    from the body (whose boots define where "ground" is), not recomputed separately per
    layer, so hair/clothing end up shifted by the exact same amount as whichever body
    they're paired with at runtime. Each layer's own lowest opaque pixel differs (boots
    reach lower than a hairline) - using that per layer would misalign them once
    composited on their side."""
    alpha = np.array(person_body_male().image())[..., 3] > 127
    rows = np.flatnonzero(np.rot90(alpha, k=1).any(axis=1))
    return (S - 6 * SCALE) - rows.max() if len(rows) else 0


def _lay_down(image, seed):
    """Rotates a standing cutout 90 degrees onto its side and re-seats it at the shared
    ground line (see _dead_layer_drop) - the same "collapsed sideways" transform
    person_dead() already used for the old single-sprite figure, generalised so every
    composited layer (body, hair, clothing) gets its own matching variant instead of
    everyone falling back to one shared corpse once they're down."""
    arr = np.array(image).astype(np.uint8)
    rot = np.rot90(arr, k=1)
    c = Canvas(seed)
    mask = rot[..., 3] > 127
    c.rgb[mask] = rot[..., :3][mask]
    c.alpha |= mask
    drop = _dead_layer_drop()
    c.rgb = np.roll(c.rgb, drop, axis=0)
    c.alpha = np.roll(c.alpha, drop, axis=0)
    return c


def person_body_male_dead():
    return _lay_down(person_body_male().image(), seed_for("person_body_male_dead"))


def person_body_female_dead():
    return _lay_down(person_body_female().image(), seed_for("person_body_female_dead"))


def hair_short_dead():
    return _lay_down(hair_short().image(), seed_for("hair_short_dead"))


def hair_long_dead():
    return _lay_down(hair_long().image(), seed_for("hair_long_dead"))


def hair_tied_dead():
    return _lay_down(hair_tied().image(), seed_for("hair_tied_dead"))


def clothing_robe_dead():
    return _lay_down(clothing_robe().image(), seed_for("clothing_robe_dead"))


def clothing_tunic_dead():
    return _lay_down(clothing_tunic().image(), seed_for("clothing_tunic_dead"))


def clothing_cloak_dead():
    return _lay_down(clothing_cloak().image(), seed_for("clothing_cloak_dead"))


def _wood_log(canvas, cx, cy, rx, ry, bark_color, core_color, seed):
    """A log end: crosshatch shading (kept, not replaced) plus concentric growth rings,
    a couple of radiating checking-cracks, and short bark dashes around the rim - the
    literal thing a cut log shows, layered on top of the shading rather than replacing
    it (a flat-colour version of this read as "MS Paint flat" in review)."""
    rng = random.Random(seed)
    bark_mask = ellipse(cx, cy, rx, ry) & ~ellipse(cx, cy, rx * 0.86, ry * 0.86)
    core_mask = ellipse(cx, cy, rx * 0.86, ry * 0.86)
    canvas.fill(bark_mask, bark_color)
    canvas.fill(core_mask, core_color)

    ring_img = Image.new("L", (S, S), 0)
    draw = ImageDraw.Draw(ring_img)
    n_rings = rng.randint(7, 10)  # fine/numerous - a handful of bold bands read as a
    # target, not an engraved cross-section
    for i in range(1, n_rings + 1):
        frac = (i / (n_rings + 1)) * rng.uniform(0.92, 1.0)
        wob = rng.uniform(-0.3, 0.3)
        bbox = [(cx - rx * 0.86 * frac + wob) * SCALE, (cy - ry * 0.86 * frac + wob) * SCALE,
                (cx + rx * 0.86 * frac + wob) * SCALE, (cy + ry * 0.86 * frac + wob) * SCALE]
        draw.ellipse(bbox, outline=255, width=1)
    for _ in range(rng.randint(3, 5)):
        angle = rng.uniform(0, 2 * math.pi)
        t = rng.uniform(0.6, 0.95)
        x2, y2 = cx + math.cos(angle) * rx * 0.86 * t, cy + math.sin(angle) * ry * 0.86 * t
        draw.line([(cx * SCALE, cy * SCALE), (x2 * SCALE, y2 * SCALE)], fill=255, width=max(1, SCALE // 4))
    ring_mask = (np.array(ring_img) > 127) & core_mask
    canvas.flat(ring_mask, darken(core_color, 0.35))

    dash_img = Image.new("L", (S, S), 0)
    dd = ImageDraw.Draw(dash_img)
    n_dash = max(10, int(2 * math.pi * max(rx, ry) / 1.6))
    for i in range(n_dash):
        a = (i / n_dash) * 2 * math.pi + rng.uniform(-0.05, 0.05)
        r0 = rng.uniform(0.88, 0.94)
        dd.line([((cx + math.cos(a) * rx * r0) * SCALE, (cy + math.sin(a) * ry * r0) * SCALE),
                 ((cx + math.cos(a) * rx) * SCALE, (cy + math.sin(a) * ry) * SCALE)],
                fill=255, width=max(1, SCALE // 3))
    dash_mask = (np.array(dash_img) > 127) & bark_mask
    canvas.flat(dash_mask, darken(bark_color, 0.4))


def _rope_tie(canvas, p0, p1, rope_color, seed, width=2.6):
    """A wrapped-cord band across the stack, drawn on top - called for in the original
    brief ("tied with a rope") but never actually implemented."""
    rng = random.Random(seed)
    x0, y0 = p0
    x1, y1 = p1
    length = math.hypot(x1 - x0, y1 - y0)
    nx, ny = -(y1 - y0) / length, (x1 - x0) / length

    band_img = Image.new("L", (S, S), 0)
    ImageDraw.Draw(band_img).line([(x0 * SCALE, y0 * SCALE), (x1 * SCALE, y1 * SCALE)],
                                   fill=255, width=int(width * SCALE))
    band_mask = np.array(band_img) > 127
    canvas.flat(band_mask, rope_color)

    tick_img = Image.new("L", (S, S), 0)
    td = ImageDraw.Draw(tick_img)
    n_ticks = max(6, int(length / 1.6))
    for i in range(n_ticks):
        t = i / n_ticks
        cxm, cym = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        w = width * 0.5
        td.line([((cxm - nx * w) * SCALE, (cym - ny * w) * SCALE),
                 ((cxm + nx * w) * SCALE, (cym + ny * w) * SCALE)],
                fill=255, width=max(1, SCALE // 4))
    tick_mask = (np.array(tick_img) > 127) & band_mask
    canvas.flat(tick_mask, darken(rope_color, 0.35))

    edge_img = Image.new("L", (S, S), 0)
    ed = ImageDraw.Draw(edge_img)
    for side in (-1, 1):
        ex0, ey0 = x0 + nx * width * 0.5 * side, y0 + ny * width * 0.5 * side
        ex1, ey1 = x1 + nx * width * 0.5 * side, y1 + ny * width * 0.5 * side
        ed.line([(ex0 * SCALE, ey0 * SCALE), (ex1 * SCALE, ey1 * SCALE)], fill=255, width=max(1, SCALE // 3))
    edge_mask = (np.array(edge_img) > 127) & band_mask
    canvas.flat(edge_mask, darken(rope_color, 0.45))


def _ground_shadow_dashes(canvas, cx, cy, half_w, seed, n=14):
    """Sparse hatch dashes grounding the object, instead of it floating with no contact
    shadow at all - a small thing the reference sprites never skip."""
    rng = random.Random(seed)
    img = Image.new("L", (S, S), 0)
    draw = ImageDraw.Draw(img)
    for _ in range(n):
        x = cx + rng.uniform(-half_w, half_w)
        y = cy + rng.uniform(-0.6, 0.6)
        w = rng.uniform(1.5, 3.5)
        draw.line([((x - w / 2) * SCALE, y * SCALE), ((x + w / 2) * SCALE, y * SCALE)],
                  fill=255, width=1)
    mask = np.array(img) > 127
    canvas.rgb[mask & ~canvas.alpha] = darken(rgb(0.5, 0.45, 0.35), 0.3)
    canvas.alpha |= mask


def wood():
    seed = seed_for("wood")
    c = Canvas(seed)
    bark = rgb(0.40, 0.25, 0.10)
    core = rgb(0.72, 0.55, 0.34)
    logs = ((32, 22, 14, 12), (20, 42, 15, 13), (45, 44, 14, 12))
    for i, (cx, cy, rx, ry) in enumerate(logs):
        _wood_log(c, cx, cy, rx, ry, bark, core, seed + i * 7)
    _ground_shadow_dashes(c, 32, 57, 20, seed + 99)
    _rope_tie(c, (44, 30), (18, 50), rgb(0.62, 0.52, 0.30), seed + 5)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


SKIN_OPTIONS_APPLE = [rgb(0.74, 0.16, 0.14), rgb(0.68, 0.30, 0.12), rgb(0.60, 0.20, 0.16)]
LEAF = rgb(0.34, 0.46, 0.24)
STEM = rgb(0.32, 0.24, 0.15)


def apple():
    """Deliberately not jagged on the body edge (that's what shredded an early tree
    prototype) - the lopsidedness comes from unioning off-centre ellipses instead, and
    rough_outline (raster, not shape-aware) supplies the hand-inked edge on top."""
    seed = seed_for("apple")
    rng = random.Random(seed)
    c = Canvas(seed)
    skin = rng.choice(SKIN_OPTIONS_APPLE)

    lean = rng.uniform(-2.5, 2.5)
    lobe_l = rng.uniform(9, 13)
    lobe_r = rng.uniform(9, 13)
    top_y = rng.uniform(19, 22)
    body = (
        ellipse(32 + lean * 0.3, 38, 20, 19)
        | ellipse(22 + lean, 34, lobe_l, 12)
        | ellipse(42 + lean, 34, lobe_r, 12)
    )
    body &= ~rect(0, 0, 64, top_y)
    c.fill(body, skin)
    c.flat(ellipse(23, 29, 4, 3) & body, lighten(skin, 0.68))
    c.flat(ellipse(32 + lean * 0.2, 54, 2.2, 1.1) & body, darken(skin, 0.4))
    c.flat(ellipse(32, 55, 0.8, 0.8) & body, darken(skin, 0.55))

    stem = poly(jagged_poly(
        [(31, top_y - 1), (33.4, top_y - 1), (34.5 + lean * 0.4, 12), (32.5 + lean * 0.4, 12)],
        rng, amp=0.35, segments_per_edge=2, smooth_passes=1,
    ))
    c.fill(stem, STEM)
    leaf_pts = jagged_poly(
        [(34, 17), (44, 12), (52, 15), (44, 20)], rng, amp=1.0, segments_per_edge=3, smooth_passes=1
    )
    c.fill(poly(leaf_pts), LEAF)

    c.rough_outline(width=max(1, SCALE // 2))
    return c


def pear():
    seed = seed_for("pear")
    c = Canvas(seed)
    skin = rgb(0.62, 0.68, 0.20)  # muted towards the earthy palette; old value was neon-bright
    body = ellipse(32, 44, 18, 16) | ellipse(32, 28, 11, 12)
    c.fill(body, skin)
    c.flat(ellipse(25, 38, 4, 5), lighten(skin, 0.55))
    for px, py in ((38, 44), (34, 52), (42, 36), (28, 50), (36, 30)):
        c.flat(rect(px, py, px, py), darken(skin, 0.35))
    c.fill(rect(31, 10, 34, 20), rgb(0.36, 0.24, 0.14))
    c.fill(ellipse(41, 15, 9, 4) & ~ellipse(46, 11, 9, 5), rgb(0.30, 0.50, 0.18))
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def potato():
    seed = seed_for("potato")
    c = Canvas(seed)
    skin = rgb(0.66, 0.52, 0.30)  # muted towards the earthy palette
    body = ellipse(30, 34, 22, 16) | ellipse(40, 40, 15, 12) | ellipse(20, 40, 12, 10)
    c.fill(body, skin)
    for px, py in ((22, 30), (36, 28), (44, 38), (28, 42), (16, 38), (38, 46)):
        c.flat(ellipse(px, py, 2, 1), darken(skin, 0.34))
        c.flat(rect(px - 1, py - 1, px, py - 1), darken(skin, 0.5))
    c.flat(ellipse(26, 48, 6, 2), darken(skin, 0.52))
    c.flat(ellipse(45, 47, 4, 2), darken(skin, 0.52))
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def mushroom():
    seed = seed_for("mushroom")
    c = Canvas(seed)
    cap = rgb(0.50, 0.32, 0.20)
    stem = rgb(0.80, 0.75, 0.62)
    gills = rgb(0.64, 0.58, 0.46)
    c.fill(poly([(27, 30), (37, 30), (39, 54), (25, 54)]), stem)
    c.flat(rect(20, 30, 44, 33) & ellipse(32, 30, 24, 8), gills)
    capmask = ellipse(32, 30, 26, 20) & ~rect(0, 31, 64, 64)
    c.fill(capmask, cap)
    for px, py, r in ((22, 24, 4), (38, 20, 5), (45, 27, 3), (30, 16, 3), (14, 29, 3)):
        c.flat(ellipse(px, py, r, r - 1) & capmask, lighten(cap, 0.62))
    c.fill(ellipse(32, 54, 10, 4), darken(stem, 0.12))
    c.rough_outline(width=max(1, SCALE // 2))
    return c


WALL_OPTIONS = [rgb(0.56, 0.44, 0.28), rgb(0.50, 0.40, 0.26), rgb(0.58, 0.47, 0.31)]
ROOF_OPTIONS = [rgb(0.33, 0.28, 0.15), rgb(0.36, 0.30, 0.17)]
DOOR = rgb(0.22, 0.16, 0.11)


def thatch_fringe_mask(x0, x1, y, rng, droop_range=(2.0, 4.5)):
    """A row of small pointed straw-bundle drips hanging off the eave - what makes a
    roof read as *thatched* rather than a clean triangular plane."""
    mask = np.zeros((S, S), dtype=bool)
    n = max(4, round((x1 - x0) / 3.2))
    xs = np.linspace(x0, x1, n)
    width = (x1 - x0) / n
    for x in xs:
        droop = rng.uniform(*droop_range)
        w = width * rng.uniform(0.55, 0.85)
        drip = [(x - w / 2, y), (x, y + droop), (x + w / 2, y)]
        drip = jagged_poly(drip, rng, amp=0.4, segments_per_edge=2, smooth_passes=1)
        mask |= poly(drip)
    return mask


def storage_hut():
    seed = seed_for("storage_hut")
    rng = random.Random(seed)
    c = Canvas(seed)
    wall = rng.choice(WALL_OPTIONS)
    roof = rng.choice(ROOF_OPTIONS)

    wall_h_jitter = rng.uniform(-1.0, 1.0)
    wall_pts = [
        (12, 30), (52, 30),
        (52 + rng.uniform(-1, 1), GROUND_CONTACT_Y + wall_h_jitter),
        (12 + rng.uniform(-1, 1), GROUND_CONTACT_Y - wall_h_jitter),
    ]
    walls = poly(jagged_poly(wall_pts, rng, amp=0.9, segments_per_edge=4, smooth_passes=1))
    c.fill(walls, wall)
    for x in (20, 28, 36, 44):
        seam_x = x + rng.uniform(-0.6, 0.6)
        c.flat(rect(seam_x, 31, seam_x + 0.6, GROUND_CONTACT_Y - 1) & walls, darken(wall, 0.28))

    roof_apex = (32 + rng.uniform(-1.5, 1.5), 6 + rng.uniform(-1, 1))
    roof_pts = [roof_apex, (58, 32), (6, 32)]
    roof_body = poly(jagged_poly(roof_pts, rng, amp=1.1, segments_per_edge=5, smooth_passes=2))
    # kept inset from the triangle's own corners, which taper to a sliver too thin to
    # reliably fuse with a fringe drip even after closing
    fringe = thatch_fringe_mask(11, 53, 32, rng)
    close_r = max(3, SCALE * 2)
    roof_mask = erode(dilate(roof_body | fringe, close_r), close_r)
    c.fill(roof_mask, roof)

    door = poly(jagged_poly([(26, 40), (38, 40), (37, GROUND_CONTACT_Y), (27, GROUND_CONTACT_Y)], rng, amp=0.5, segments_per_edge=3))
    c.fill(door, DOOR)
    c.flat(ellipse(35, 49, 0.6, 0.6), rgb(0.85, 0.78, 0.5))

    window = poly(jagged_poly([(15, 36), (22, 36), (22, 42), (15, 42)], rng, amp=0.4, segments_per_edge=2))
    c.fill(window, rgb(0.20, 0.24, 0.26))
    c.flat(rect(18.2, 36, 18.8, 42) & window, darken(wall, 0.15))
    c.flat(rect(15, 38.7, 22, 39.3) & window, darken(wall, 0.15))

    c.rough_outline(width=max(1, SCALE // 2))
    return c


def grave_unmarked():
    """A bare dirt mound - no stone, no name, nothing left to read."""
    seed = seed_for("grave_unmarked")
    c = Canvas(seed)
    dirt = rgb(0.36, 0.26, 0.16)
    dirt_dark = darken(dirt, 0.32)
    mound = (ellipse(32, 46, 24, 15) | ellipse(18, 50, 12, 9) | ellipse(47, 49, 12, 9)) & ~rect(0, 0, 64, 32)
    c.fill(mound, dirt)
    for px, py in ((22, 42), (36, 38), (46, 44), (26, 52), (42, 52)):
        c.flat(ellipse(px, py, 3, 2) & mound, dirt_dark)
    stone = rgb(0.5, 0.5, 0.52)
    for px, py, r in ((16, 46, 3), (49, 44, 2), (30, 34, 2)):
        c.fill(ellipse(px, py, r, r - 1), stone)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def grave_marked():
    """A carved headstone planted in a small mound - the record survives."""
    seed = seed_for("grave_marked")
    c = Canvas(seed)
    dirt = rgb(0.36, 0.26, 0.16)
    stone = rgb(0.58, 0.58, 0.60)
    rune = darken(stone, 0.45)
    mound = ellipse(32, 54, 20, 8) & ~rect(0, 0, 64, 48)
    c.fill(mound, dirt)
    slab = rect(22, 24, 42, 50) | (ellipse(32, 24, 10, 10) & ~rect(0, 24, 64, 64))
    c.fill(slab, stone)
    c.flat(rect(30, 28, 34, 44) & slab, rune)
    c.flat(rect(25, 33, 39, 37) & slab, rune)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def random_conifer_tiers(rng):
    """A pine as an irregular stack of 3-5 branch tiers instead of 3 identical
    triangles: tier count, width, vertical spacing and horizontal drift all vary."""
    tier_count = rng.randint(3, 5)
    tiers = []
    apex_y = rng.uniform(2, 8)
    apex_x = 32 + rng.uniform(-2, 2)
    cur_y = apex_y
    half_width = rng.uniform(9, 12)
    last_base_y = cur_y
    for _ in range(tier_count):
        tier_h = rng.uniform(11, 15)
        base_y = cur_y + tier_h
        last_base_y = base_y
        drift = rng.uniform(-2.5, 2.5)
        left = (apex_x + drift - half_width, base_y)
        right = (apex_x + drift + half_width, base_y)
        apex = (apex_x + rng.uniform(-1.5, 1.5), cur_y - rng.uniform(0, 3))
        tiers.append([apex, right, left])
        cur_y = base_y - tier_h * rng.uniform(0.28, 0.4)
        half_width += rng.uniform(3.0, 5.0)
    return tiers, last_base_y


def _conifer_split(name):
    seed = seed_for(name)
    rng = random.Random(seed)
    trunk_base = rgb(0.34, 0.24, 0.15)
    foliage = rgb(0.36, 0.42, 0.26)

    tiers, trunk_top_y = random_conifer_tiers(rng)

    trunk_w = rng.uniform(2.5, 4.0)
    trunk_lean = rng.uniform(-1.5, 1.5)
    trunk_top = 32 + rng.uniform(-1, 1)
    trunk_mask = poly(jagged_poly([
        (trunk_top - trunk_w, trunk_top_y),
        (trunk_top + trunk_w, trunk_top_y),
        (trunk_top + trunk_w + trunk_lean, GROUND_CONTACT_Y),
        (trunk_top - trunk_w + trunk_lean, GROUND_CONTACT_Y),
    ], rng, amp=0.8, segments_per_edge=3, smooth_passes=1))

    tier_masks = [lobe_cluster_mask(apex, left, right, rng, rows=rng.choice([2, 3, 3])) for apex, right, left in tiers]

    return split_trunk_canopy(name, trunk_mask, tier_masks, trunk_base, foliage)


def conifer_tree():
    return _conifer_split("conifer_tree")[0]


def conifer_tree_trunk():
    return _conifer_split("conifer_tree")[1]


def conifer_tree_canopy():
    return _conifer_split("conifer_tree")[2]


# Three hand-authored angular boulder outlines (8 points each, same walk order: top-
# left facet, top, top-right facet, right, bottom-right facet, bottom, bottom-left
# facet, left), unit-scaled around the origin. Real stones read as a handful of flat
# facets meeting at sharp-ish corners, not a smooth round blob - three overlapping
# ellipses was the single most "still just a circle" shape in the whole roster.
_ROCK_VARIANT_A = [
    (-0.55, -0.85), (0.05, -1.0), (0.75, -0.6), (1.0, 0.05),
    (0.6, 0.75), (-0.1, 0.95), (-0.85, 0.55), (-0.95, -0.25),
]
_ROCK_VARIANT_B = [
    (-0.3, -1.0), (0.45, -0.8), (0.95, -0.15), (0.8, 0.55),
    (0.2, 1.0), (-0.5, 0.85), (-1.0, 0.2), (-0.7, -0.6),
]
_ROCK_VARIANT_C = [
    (-0.7, -0.7), (0.15, -0.95), (0.85, -0.35), (0.95, 0.35),
    (0.35, 0.9), (-0.4, 0.7), (-0.9, 0.3), (-0.95, -0.35),
]
ROCK_VARIANTS = [np.array(v, dtype=float) for v in (_ROCK_VARIANT_A, _ROCK_VARIANT_B, _ROCK_VARIANT_C)]


def _blended_rock_points(rng, cx, cy, rx, ry):
    weights = np.array([rng.random() for _ in ROCK_VARIANTS])
    weights /= weights.sum()
    unit = sum(w * v for w, v in zip(weights, ROCK_VARIANTS))
    return [(cx + p[0] * rx, cy + p[1] * ry) for p in unit]


def _stone_facets(mask, seed, n=3):
    """A couple of irregular crack lines per stone - real rock surfaces show a handful
    of distinct fracture lines, not a repeated micro-pattern (see
    project_sprite_woodcut_texture_library memory: few large marks, not many small
    identical ones, is what kept this from reading as a stamped pattern)."""
    rng = random.Random(seed)
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return np.zeros((S, S), dtype=bool)
    x0, x1, y0, y1 = xs.min() / SCALE, xs.max() / SCALE, ys.min() / SCALE, ys.max() / SCALE
    img = Image.new("L", (S, S), 0)
    draw = ImageDraw.Draw(img)
    for _ in range(n):
        x, y = rng.uniform(x0 + 1, x1 - 1), rng.uniform(y0 + 1, y1 - 1)
        angle = rng.uniform(0, 2 * math.pi)
        length = rng.uniform((x1 - x0) * 0.3, (x1 - x0) * 0.55)
        pts = [(x, y)]
        for _ in range(rng.randint(2, 3)):
            angle += rng.uniform(-0.6, 0.6)
            step = length / 3
            x, y = x + math.cos(angle) * step, y + math.sin(angle) * step
            pts.append((x, y))
        draw.line([(px * SCALE, py * SCALE) for px, py in pts], fill=255, width=max(1, SCALE // 3))
    return (np.array(img) > 127) & mask


def _weather_pits(mask, seed, n=4):
    """A handful of small pockmarks - again few and distinct, not a stippled field."""
    rng = random.Random(seed)
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return np.zeros((S, S), dtype=bool)
    x0, x1, y0, y1 = xs.min() / SCALE, xs.max() / SCALE, ys.min() / SCALE, ys.max() / SCALE
    img = Image.new("L", (S, S), 0)
    draw = ImageDraw.Draw(img)
    for _ in range(n):
        x, y = rng.uniform(x0 + 1, x1 - 1), rng.uniform(y0 + 1, y1 - 1)
        r = rng.uniform(0.5, 1.3)
        draw.ellipse([(x - r) * SCALE, (y - r) * SCALE, (x + r) * SCALE, (y + r) * SCALE], fill=255)
    return (np.array(img) > 127) & mask


def _stone(canvas, cx, cy, rx, ry, color, seed, rng):
    """One faceted stone: blended boulder silhouette, crosshatch shading, a couple of
    crack facets and weather pits. Factored out of rock_pile so rock_boulder/rock_cluster
    can reuse the exact same per-stone construction at different counts/sizes instead of
    duplicating it."""
    pts = _blended_rock_points(rng, cx, cy, rx, ry)
    mask = poly(jagged_poly(pts, rng, amp=0.7, segments_per_edge=3, smooth_passes=1))
    out_rgb = hatch_fill(mask, color, seed)
    canvas.rgb[mask] = out_rgb[mask]
    canvas.alpha |= mask
    facets = _stone_facets(mask, seed + 3, n=rng.randint(2, 3))
    canvas.flat(facets, darken(color, 0.45))
    pits = _weather_pits(mask, seed + 5, n=rng.randint(2, 4))
    canvas.flat(pits, darken(color, 0.35))


def rock_pile():
    """Three medium stones leant together - the original rock shape, kept as the
    "medium" member of the family now that rock_boulder/rock_cluster exist alongside it
    (todo #4: rocks need more than one shape/size, not just a random scale on one)."""
    seed = seed_for("rock_pile")
    rng = random.Random(seed)
    c = Canvas(seed)
    stone = rgb(0.5, 0.5, 0.52)
    stones = [
        (22, 51, 14, 11, stone, 0),
        (40, 53, 13, 10, darken(stone, 0.08), 1),
        (32, 43, 11, 10, lighten(stone, 0.1), 2),
    ]
    for cx, cy, rx, ry, color, i in stones:
        _stone(c, cx, cy, rx, ry, color, seed + i * 9, rng)
    c.flat(rect(19, 49, 25, 50), darken(stone, 0.35))
    c.flat(rect(36, 51, 42, 52), darken(stone, 0.35))
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def rock_boulder():
    """A single large boulder - the biggest, simplest member of the rock family."""
    seed = seed_for("rock_boulder")
    rng = random.Random(seed)
    c = Canvas(seed)
    stone = rgb(0.48, 0.48, 0.51)
    _stone(c, 32, 46, 21, 17, stone, seed, rng)
    c.flat(rect(13, 55, 51, 57), darken(stone, 0.35))
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def rock_cluster():
    """A scatter of several small stones - loose scree rather than a deliberate pile,
    the smallest and most numerous member of the rock family."""
    seed = seed_for("rock_cluster")
    rng = random.Random(seed)
    c = Canvas(seed)
    stone = rgb(0.52, 0.52, 0.55)
    stones = [
        (15, 54, 7, 6, stone, 0),
        (27, 58, 6, 5, darken(stone, 0.06), 1),
        (40, 55, 8, 6, lighten(stone, 0.08), 2),
        (50, 51, 6, 5, darken(stone, 0.1), 3),
        (34, 47, 6, 5, stone, 4),
    ]
    for cx, cy, rx, ry, color, i in stones:
        _stone(c, cx, cy, rx, ry, color, seed + i * 9, rng)
    c.flat(rect(11, 58, 55, 59), darken(stone, 0.3))
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def _fruit_tree_canopy():
    return (ellipse(32, 26, 22, 17) | ellipse(18, 30, 13, 12)
            | ellipse(46, 30, 13, 12) | ellipse(32, 12, 15, 12))


def _fruit_tree_split(name):
    trunk = rgb(0.34, 0.24, 0.15)
    foliage = rgb(0.30, 0.38, 0.22)
    trunk_mask = rect(29, 44, 35, GROUND_CONTACT_Y)
    return split_trunk_canopy(name, trunk_mask, [_fruit_tree_canopy()], trunk, foliage)


def _fruit_tree_bare(name):
    """Shared deciduous canopy for the fruit-tree sprites - only the fruit color/
    placement differs between kinds, so the two trees stay readable as "the same kind
    of tree" at a glance. The canopy is a union of ellipses, not jagged (see the note
    above jagged_poly) - rough_outline alone carries the hand-inked edge.

    This is the WHOLE tree - a picked-clean node renders exactly this, with no fruit.
    _fruit_overlay is a second, separately composited layer (see ResourceNodeView) so a
    node with no stock left doesn't need its own distinct "bare" texture asset."""
    return _fruit_tree_split(name)[0]


def _fruit_overlay(name, fruit_color, fruit_spots):
    """Just the fruit dots, on an otherwise-transparent canvas, masked to the same
    canopy footprint _fruit_tree_bare fills - no outline of its own, since it's always
    composited on top of the bare tree's already-outlined canopy, never shown alone."""
    seed = seed_for(name)
    c = Canvas(seed)
    canopy = _fruit_tree_canopy()
    for px, py, r in fruit_spots:
        c.flat(ellipse(px, py, r, r) & canopy, fruit_color)
    return c


def apple_tree():
    return _fruit_tree_bare("apple_tree")


def apple_tree_trunk():
    return _fruit_tree_split("apple_tree")[1]


def apple_tree_canopy():
    return _fruit_tree_split("apple_tree")[2]


def apple_tree_fruit():
    return _fruit_overlay(
        "apple_tree", rgb(0.70, 0.18, 0.16),
        ((22, 24, 3), (40, 20, 3), (30, 34, 3), (46, 32, 2), (18, 38, 2)))


def pear_tree():
    return _fruit_tree_bare("pear_tree")


def pear_tree_trunk():
    return _fruit_tree_split("pear_tree")[1]


def pear_tree_canopy():
    return _fruit_tree_split("pear_tree")[2]


def pear_tree_fruit():
    return _fruit_overlay(
        "pear_tree", rgb(0.62, 0.68, 0.20),
        ((24, 22, 3), (42, 22, 3), (32, 36, 3), (44, 34, 2), (20, 36, 2)))


def deciduous_tree():
    """Same bare-canopy shape and construction as the fruit trees (see _fruit_tree_bare) -
    purely decorative background filler (TerrainRenderer.ScatterDecoration), not a gameplay
    resource, so it never needs a fruit overlay."""
    return _fruit_tree_bare("deciduous_tree")


def deciduous_tree_trunk():
    return _fruit_tree_split("deciduous_tree")[1]


def deciduous_tree_canopy():
    return _fruit_tree_split("deciduous_tree")[2]


def bush():
    """A low, trunkless clump - the same union-of-ellipses construction as the tree
    canopies, just wider and closer to the ground."""
    seed = seed_for("bush")
    c = Canvas(seed)
    foliage = rgb(0.26, 0.36, 0.18)
    mask = (ellipse(32, 49, 20, 14) | ellipse(15, 53, 12, 10)
            | ellipse(49, 53, 12, 10) | ellipse(32, 40, 15, 12))
    c.fill(mask, foliage)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def grass():
    """A handful of jagged blades of varying height and lean - thin enough that
    rough_outline's ring, not crosshatch density, carries most of the shape's read."""
    seed = seed_for("grass")
    rng = random.Random(seed)
    c = Canvas(seed)
    green = rgb(0.32, 0.42, 0.18)
    for i in range(6):
        bx = 10 + i * 8 + rng.uniform(-2.0, 2.0)
        height = rng.uniform(16, 30)
        lean = rng.uniform(-5.0, 5.0)
        blade = jagged_poly(
            [(bx - 2.2, GROUND_CONTACT_Y), (bx + lean, GROUND_CONTACT_Y - height), (bx + 2.2, GROUND_CONTACT_Y)],
            rng, amp=0.5, segments_per_edge=2, smooth_passes=1)
        c.fill(poly(blade), green)
    c.rough_outline(width=1)
    return c


def wild_grass():
    """A denser, wider clump than the terrain-decoration `grass` tuft - reads as a patch
    worth gathering rather than a stray blade underfoot."""
    seed = seed_for("wild_grass")
    rng = random.Random(seed)
    c = Canvas(seed)
    greens = [rgb(0.34, 0.46, 0.20), rgb(0.30, 0.42, 0.18), rgb(0.38, 0.48, 0.22)]
    for i in range(11):
        bx = 8 + i * 4.7 + rng.uniform(-1.6, 1.6)
        height = rng.uniform(20, 34)
        lean = rng.uniform(-6.0, 6.0)
        blade = jagged_poly(
            [(bx - 2.0, GROUND_CONTACT_Y), (bx + lean, GROUND_CONTACT_Y - height), (bx + 2.0, GROUND_CONTACT_Y)],
            rng, amp=0.5, segments_per_edge=2, smooth_passes=1)
        c.fill(poly(blade), rng.choice(greens))
    c.rough_outline(width=1)
    return c


def tree_stump():
    """A cut stump: bark rind, a ring-marked top, a couple of surface roots - still
    rooted in the ground, unlike fallen_log lying on its side."""
    seed = seed_for("tree_stump")
    rng = random.Random(seed)
    c = Canvas(seed)
    bark = rgb(0.36, 0.24, 0.14)
    core = rgb(0.68, 0.52, 0.32)
    root_l = jagged_poly([(14, GROUND_CONTACT_Y), (24, 51), (28, GROUND_CONTACT_Y)], rng, amp=0.6, segments_per_edge=3, smooth_passes=1)
    root_r = jagged_poly([(50, GROUND_CONTACT_Y), (40, 51), (36, GROUND_CONTACT_Y)], rng, amp=0.6, segments_per_edge=3, smooth_passes=1)
    c.fill(poly(root_l) | poly(root_r), darken(bark, 0.1))
    _wood_log(c, 32, 47, 17, 15, bark, core, seed)
    _ground_shadow_dashes(c, 32, GROUND_CONTACT_Y, 16, seed + 99)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def fallen_log():
    """A log lying on its side - one cut end shows growth rings, the long body is just
    crosshatched bark with a few seam lines (rings only read at an actual cut face)."""
    seed = seed_for("fallen_log")
    rng = random.Random(seed)
    c = Canvas(seed)
    bark = rgb(0.38, 0.26, 0.15)
    core = rgb(0.70, 0.53, 0.33)
    body = poly(jagged_poly(
        [(14, 51), (50, 45), (52, 57), (16, GROUND_CONTACT_Y)], rng, amp=0.8, segments_per_edge=4, smooth_passes=1))
    c.fill(body, bark)
    for x in (22, 30, 38):
        seam_x = x + rng.uniform(-1, 1)
        c.flat(rect(seam_x, 47, seam_x + 0.8, 61) & body, darken(bark, 0.22))
    _wood_log(c, 14, 52, 9, 11, bark, core, seed + 1)
    _ground_shadow_dashes(c, 34, GROUND_CONTACT_Y, 20, seed + 99)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def fern():
    """A low fan of arched fronds radiating from one base point - a fuller forest-floor
    spray than grass's simple upright tuft."""
    seed = seed_for("fern")
    rng = random.Random(seed)
    c = Canvas(seed)
    green = rgb(0.28, 0.40, 0.20)
    base_x, base_y = 32, GROUND_CONTACT_Y
    n_fronds = 7
    for i in range(n_fronds):
        t = i / (n_fronds - 1)
        angle = math.radians(-155 + t * 130)
        length = rng.uniform(16, 25)
        dx, dy = math.cos(angle) * length, math.sin(angle) * length
        tip = (base_x + dx, base_y + dy)
        perp_len = math.hypot(dy, dx) or 1.0
        perp = (-dy / perp_len * 1.6, dx / perp_len * 1.6)
        blade = jagged_poly(
            [(base_x - perp[0], base_y - perp[1]), tip, (base_x + perp[0], base_y + perp[1])],
            rng, amp=0.4, segments_per_edge=2, smooth_passes=1)
        c.fill(poly(blade), green)
    c.rough_outline(width=1)
    return c


def flower():
    """A short stem topped with a ring of petals around a bright center - the one spot of
    saturated color the muted palette (docs/Many Winters visual plan, "art constraints")
    allows, since it's a tiny accent rather than a large area."""
    seed = seed_for("flower")
    c = Canvas(seed)
    stem = rgb(0.30, 0.40, 0.20)
    petal = rgb(0.82, 0.52, 0.62)
    center = rgb(0.90, 0.75, 0.25)
    c.fill(rect(31, 38, 33, GROUND_CONTACT_Y), stem)
    petals = (ellipse(32, 28, 6, 9) | ellipse(23, 33, 8, 6) | ellipse(41, 33, 8, 6)
              | ellipse(27, 24, 7, 7) | ellipse(37, 24, 7, 7))
    c.fill(petals, petal)
    c.flat(ellipse(32, 31, 4, 4), center)
    c.rough_outline(width=1)
    return c


def selection_marker():
    """A bright downward-pointing marker floated above a selected unit's head - a flat
    engraved emblem, not a physical object, so it keeps a clean triangle (only a whisper
    of jag) rather than a hand-cut/organic edge."""
    seed = seed_for("selection_marker")
    rng = random.Random(seed)
    c = Canvas(seed)
    gold = rgb(0.95, 0.78, 0.20)
    tip = poly(jagged_poly([(32, 46), (18, 18), (46, 18)], rng, amp=0.4, segments_per_edge=3, smooth_passes=1))
    c.fill(tip, gold)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


SPRITES = {
    "person": person,
    "person_dead": person_dead,
    "person_body_male": person_body_male,
    "person_body_female": person_body_female,
    "person_body_male_dead": person_body_male_dead,
    "person_body_female_dead": person_body_female_dead,
    "hair_short": hair_short,
    "hair_long": hair_long,
    "hair_tied": hair_tied,
    "hair_short_dead": hair_short_dead,
    "hair_long_dead": hair_long_dead,
    "hair_tied_dead": hair_tied_dead,
    "clothing_robe": clothing_robe,
    "clothing_tunic": clothing_tunic,
    "clothing_cloak": clothing_cloak,
    "clothing_robe_dead": clothing_robe_dead,
    "clothing_tunic_dead": clothing_tunic_dead,
    "clothing_cloak_dead": clothing_cloak_dead,
    "wood": wood,
    "apple": apple,
    "pear": pear,
    "potato": potato,
    "mushroom": mushroom,
    "storage_hut": storage_hut,
    "grave_unmarked": grave_unmarked,
    "grave_marked": grave_marked,
    "conifer_tree": conifer_tree,
    "conifer_tree_trunk": conifer_tree_trunk,
    "conifer_tree_canopy": conifer_tree_canopy,
    "deciduous_tree": deciduous_tree,
    "deciduous_tree_trunk": deciduous_tree_trunk,
    "deciduous_tree_canopy": deciduous_tree_canopy,
    "apple_tree": apple_tree,
    "apple_tree_trunk": apple_tree_trunk,
    "apple_tree_canopy": apple_tree_canopy,
    "apple_tree_fruit": apple_tree_fruit,
    "pear_tree": pear_tree,
    "pear_tree_trunk": pear_tree_trunk,
    "pear_tree_canopy": pear_tree_canopy,
    "pear_tree_fruit": pear_tree_fruit,
    "bush": bush,
    "grass": grass,
    "wild_grass": wild_grass,
    "flower": flower,
    "fern": fern,
    "rock_pile": rock_pile,
    "rock_boulder": rock_boulder,
    "rock_cluster": rock_cluster,
    "tree_stump": tree_stump,
    "fallen_log": fallen_log,
    "selection_marker": selection_marker,
}


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else "out"
    os.makedirs(out_dir, exist_ok=True)

    images = {}
    for name, fn in SPRITES.items():
        img = fn().image()
        img.save(os.path.join(out_dir, f"{name}.png"))
        images[name] = img
        print(f"wrote {name}.png")

    # contact sheet at a fixed per-cell display size for eyeballing the result
    disp, cols = 300, 4
    rows = (len(images) + cols - 1) // cols
    pad, label = 8, 14
    sheet = Image.new("RGBA", (cols * (disp + pad) + pad,
                               rows * (disp + pad + label) + pad),
                      (46, 48, 54, 255))
    draw = ImageDraw.Draw(sheet)
    for i, (name, img) in enumerate(images.items()):
        cx = pad + (i % cols) * (disp + pad)
        cy = pad + (i // cols) * (disp + pad + label)
        draw.rectangle([cx, cy, cx + disp - 1, cy + disp - 1], fill=(96, 104, 92, 255))
        sheet.alpha_composite(img.resize((disp, disp), Image.LANCZOS), (cx, cy))
        draw.text((cx + 2, cy + disp + 2), name, fill=(230, 230, 230, 255))
    sheet.save(os.path.join(out_dir, "_contact_sheet.png"))
    print("wrote _contact_sheet.png")


if __name__ == "__main__":
    main()
