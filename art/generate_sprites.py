#!/usr/bin/env python3
"""
Woodcut-style sprites for the Godot prototype: crosshatch shading where line density carries
tone (never a blended gradient) over silhouettes built from several primitives, after Karel
Zeman's engraved paper cut-outs (docs/ZemanConceptArt.png, docs/ZemanSprites.png,
art/zeman-sprite-prompts.md).

Drawn at 4x the 64-unit authoring grid (SCALE) so the hatch lines survive BillboardSprite.cs's
mipmapped linear filtering instead of collapsing into single pixels.

Redrawing a sprite after the concept art means following its drawing, not just its motif: crop
and enlarge the relevant region of docs/ZemanConceptArt.png first and reproduce how it is drawn
(silhouette construction, how the hatching follows the form), keeping this file's palette and
diagonal hatch fill underneath. Render a contact sheet at full size and at in-game size (about
80 to 130 px), look at it, and iterate a few rounds before calling the sprite done.

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
S = 64 * SCALE  # canvas size; shape coordinates are authored on the 64-unit grid

# Where every ground-standing shape's lowest point sits. The engine treats the canvas bottom
# (64) as ground level and places the shadow there (BillboardSprite, GroundShadow), so a base
# authored well above the edge reads as floating above its own shadow.
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
    """Stable per-sprite seed, so re-running the generator reproduces the same output."""
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
# Turns a clean polygon into a hand-cut edge. Not used on shapes already unioned from several
# primitives (apple, potato, grave mounds, rock piles, fruit-tree canopies): edge noise on a
# union of blobs reads as gnawed, not drawn. Those get their lopsidedness from off-centre
# primitives and their inked edge from rough_outline(), which works on the final raster.

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
    """Small pointed sprigs scattered over a triangle's footprint, unioned and closed - a clump
    of branches, not a cone. Few big overlapping lobes and strong closing; many small sharp
    lobes looked chewed."""
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
# Each hatch line has its own seeded personality (lateral offset, curvature, thickness,
# pen-lift breaks). One shared noise field bent every line in lockstep: corrugated sheet metal.

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

    # chop into segments and randomly skip some, like a pen lifting mid-stroke
    seg_len = 10.0
    seg_idx = np.floor(along_coord / seg_len).astype(np.int64)
    combined = raw_idx * np.int64(100003) + seg_idx
    drawn = _line_hash(combined, salt_base + 5) > 0.16

    width = np.clip(tone - tone_offset, 0.0, 1.0) * PERIOD * density_scale * thickness_jitter
    return drawn & (wobbled < width)


def hatch_fill(mask, base_color, seed):
    """Fills mask with base_color plus ink crosshatch whose density follows a diagonal light
    gradient (upper-left lit, lower-right shadowed): tone from line density, never a painterly
    gradient."""
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return np.zeros((S, S, 3), dtype=np.uint8)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    diag = max((x1 - x0) + (y1 - y0), 1)
    # capped short of 1.0 so the darkest corner keeps a hint of base colour
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
        """Base colour plus crosshatch shading, for anything with enough area to show tone."""
        out_rgb = hatch_fill(mask, base_color, self.seed)
        self.rgb[mask] = out_rgb[mask]
        self.alpha |= mask

    def flat(self, mask, color):
        """Flat fill, no hatching, for accents too small for line density to read."""
        self.rgb[mask] = color
        self.alpha |= mask

    def ink(self, mask):
        """Ink linework over an existing fill, for marks outside the crosshatch grid."""
        self.rgb[mask] = INK
        self.alpha |= mask

    def rough_outline(self, width=2):
        """A hand-inked contour: an uneven ring that thins and thickens in patches."""
        ring = dilate(self.alpha, width) & ~self.alpha
        idx = np.floor((_XX + _YY) / 3).astype(np.int64)
        keep = _line_hash(idx, self.seed * 13 + 7) > 0.22
        ring &= keep
        self.ink(ring)

    def image(self):
        out = np.zeros((S, S, 4), dtype=np.uint8)
        out[..., :3] = self.rgb
        out[..., 3] = np.where(self.alpha, 255, 0)
        return Image.fromarray(out, "RGBA")


# ---------------------------------------------------------------- trunk/canopy split
# The occlusion fade (Main.UpdateOcclusionFade, docs/Camera.png) ghosts a tree's canopy but
# must keep its trunk solid. The tree is inked as one Canvas and its final pixels are then
# partitioned into two layers, so stacking the layers reproduces the single image exactly.
_tree_split_cache = {}


def split_trunk_canopy(name, trunk_mask, canopy_masks, trunk_color, canopy_color):
    """canopy_masks: pieces filled in order, each with its own Canvas.fill, not pre-unioned -
    a conifer's tiers each need their own hatch gradient (see hatch_fill). Fruit trees pass a
    single-item list."""
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

    # Canopy is filled second, so it wins any overlap; trunk keeps only what canopy never
    # touches. The outline ring is split the same way: a ring pixel is trunk's only if it is
    # near the trunk and not near the canopy - an ambiguous seam pixel goes to the canopy on top.
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
# Person parts (robe, arm, hood, boot) are multi-point silhouettes, not primitives jagged
# after the fact.

# Three hand-authored robe silhouettes, 13 points each in the same walk order (shoulder_L,
# shoulder_R, arm_notch_R, waist_R, hip_R, hem_R1, hem_R2, hem_center, hem_L2, hem_L1, hip_L,
# waist_L, arm_notch_L). Each is a different asymmetric drape: noise jittered onto a symmetric
# trapezoid read as "primitive with texture" however smooth or jagged its edge. Blending them
# per instance keeps per-seed variety while every result stays genuinely asymmetric.
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
    """Perimeter order, so the polygon stays simple - mirroring by a multiplier tangled the
    inner and outer edges into a bowtie."""
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


# The left boot: cuff, toe box and a heel past the ankle. The right boot mirrors around x=32.
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

    # legs stay simple rects (thin, identity-critical; jag would muddy them), feet get a boot
    # silhouette with heel and toe
    c.fill(rect(26, 46, 30, 56) | rect(34, 46, 38, 56), BOOT)
    boot_l = poly(jagged_poly(LEFT_BOOT_PTS, rng, amp=0.5, segments_per_edge=3, smooth_passes=1))
    boot_r = poly(jagged_poly(RIGHT_BOOT_PTS, rng, amp=0.5, segments_per_edge=3, smooth_passes=1))
    c.fill(boot_l | boot_r, darken(BOOT, 0.25))

    # cloak: an authored asymmetric drape (ROBE_VARIANTS), lightly jagged for hand-cut texture
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
    """The living sprite rotated onto its side and drained of colour: it has to read as "that
    person, but down", not as a separate entity."""
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
# Separate layers PersonView composites at runtime: a bare body plus swappable hair and
# clothing, each drawn in a light neutral tone so Modulate can recolour it - a light grey times
# a colour approximates that colour while the dark ink hatching stays dark. person() and
# person_dead() remain the single flat sprites.

BODY_UNDERCLOTHES = rgb(0.55, 0.50, 0.45)
NEUTRAL_RECOLOURABLE = rgb(0.82, 0.80, 0.78)


def _body_layer(gender):
    """Boots, hands, head and a plain covered torso/legs, kept simple since clothing and hair
    cover nearly all of it. Only the hip width differs by gender."""
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
    """A close-cropped cap on its own transparent layer."""
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
    """One of the ROBE_VARIANTS used directly, not blended: a discrete clothing type to pick
    at runtime."""
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
    """Ground-contact drop shared by every _dead layer, computed from the body (whose boots
    define the ground) so hair and clothing shift by exactly as much as the body they are
    paired with. Each layer's own lowest pixel differs and would misalign them."""
    alpha = np.array(person_body_male().image())[..., 3] > 127
    rows = np.flatnonzero(np.rot90(alpha, k=1).any(axis=1))
    return (S - 6 * SCALE) - rows.max() if len(rows) else 0


def _lay_down(image, seed):
    """Rotates a standing cutout 90 degrees onto its side and re-seats it at the shared ground
    line (_dead_layer_drop), so every composited layer gets a matching dead variant."""
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
    """A log end: crosshatch shading kept underneath, with concentric growth rings, a few
    radiating cracks and short bark dashes on the rim. A flat-colour version read as MS Paint."""
    rng = random.Random(seed)
    bark_mask = ellipse(cx, cy, rx, ry) & ~ellipse(cx, cy, rx * 0.86, ry * 0.86)
    core_mask = ellipse(cx, cy, rx * 0.86, ry * 0.86)
    canvas.fill(bark_mask, bark_color)
    canvas.fill(core_mask, core_color)

    ring_img = Image.new("L", (S, S), 0)
    draw = ImageDraw.Draw(ring_img)
    n_rings = rng.randint(7, 10)  # fine and numerous; a few bold bands read as a target
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
    """A wrapped-cord band across the stack, drawn on top."""
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
    """Sparse hatch dashes grounding the object - the reference sprites never skip a contact
    shadow."""
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
    """Body edge not jagged (see the jagged shapes note): lopsidedness from off-centre
    ellipses, inked edge from rough_outline."""
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
    skin = rgb(0.62, 0.68, 0.20)  # muted towards the earthy palette
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
    """Small pointed straw drips along the eave - what makes a roof read as thatched rather
    than a plain triangle."""
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
    # inset from the triangle's corners, which taper too thin to fuse with a drip even after
    # closing
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
    """A pine as an irregular stack of 3-5 tiers: count, width, spacing and drift all vary."""
    tier_count = rng.randint(3, 5)
    # Headroom for lobe_cluster_mask's topmost sprig, whose tip reaches ~1.4x its radius (which
    # scales with the top tier's half_width) above this y. With less, worst-case rolls clip the
    # tip flat against the canvas top.
    apex_y = rng.uniform(14, 20)
    apex_x = 32 + rng.uniform(-2, 2)
    half_width = rng.uniform(9, 12)

    # Rolled up front so the unscaled stack's final base_y and width can be projected before
    # anything is placed: a tall, wide stack runs past the canvas bottom and clips flat. Tier
    # heights are then scaled down uniformly (never up), keeping proportions; clamping each
    # base_y would bunch overflowing tiers onto one line.
    raw_heights = [rng.uniform(11, 15) for _ in range(tier_count)]
    gap_fracs = [rng.uniform(0.28, 0.4) for _ in range(tier_count)]
    width_growths = [rng.uniform(3.0, 5.0) for _ in range(tier_count - 1)]
    final_half_width = half_width + sum(width_growths)

    cur_y = apex_y
    for tier_h, gap_frac in zip(raw_heights, gap_fracs):
        base_y = cur_y + tier_h
        cur_y = base_y - tier_h * gap_frac
    projected_last_base_y = base_y

    # Safe overestimate of how far lobe_cluster_mask's bottom row reaches past base_y (last row
    # at ~0.87 of the tier, sprig bottoms 0.7x the lobe radius below it, plus closing), as a
    # fraction of final_half_width.
    required_margin = (final_half_width * 0.75) + 3
    max_base_y = 63 - required_margin
    if projected_last_base_y > max_base_y:
        scale = (max_base_y - apex_y) / (projected_last_base_y - apex_y)
        raw_heights = [h * scale for h in raw_heights]

    tiers = []
    cur_y = apex_y
    last_base_y = apex_y
    for i, (tier_h, gap_frac) in enumerate(zip(raw_heights, gap_fracs)):
        base_y = cur_y + tier_h
        last_base_y = base_y
        drift = rng.uniform(-2.5, 2.5)
        left = (apex_x + drift - half_width, base_y)
        right = (apex_x + drift + half_width, base_y)
        apex = (apex_x + rng.uniform(-1.5, 1.5), cur_y - rng.uniform(0, 3))
        tiers.append([apex, right, left])
        cur_y = base_y - tier_h * gap_frac
        if i < len(width_growths):
            half_width += width_growths[i]
    return tiers, last_base_y


def _conifer_split(name, variant=0):
    # Already procedurally randomised (tiers, trunk lean), so a variant is another roll of the
    # same dice via a distinct seed.
    split_name = _variant_name(name, variant)
    seed = seed_for(split_name)
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

    return split_trunk_canopy(split_name, trunk_mask, tier_masks, trunk_base, foliage)


def conifer_tree():
    return _conifer_split("conifer_tree")[0]


def conifer_tree_trunk():
    return _conifer_split("conifer_tree")[1]


def conifer_tree_canopy():
    return _conifer_split("conifer_tree")[2]


def conifer_tree_trunk_v1():
    return _conifer_split("conifer_tree", 1)[1]


def conifer_tree_canopy_v1():
    return _conifer_split("conifer_tree", 1)[2]


def conifer_tree_trunk_v2():
    return _conifer_split("conifer_tree", 2)[1]


def conifer_tree_canopy_v2():
    return _conifer_split("conifer_tree", 2)[2]


# Three hand-authored boulder outlines (8 points each, same walk order: top-left facet, top,
# top-right facet, right, bottom-right facet, bottom, bottom-left facet, left), unit-scaled
# around the origin. Stones read as flat facets meeting at corners; overlapping ellipses read
# as a circle.
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
    """A few irregular crack lines per stone: a handful of distinct fracture lines, not a
    repeated micro-pattern, which reads as stamped."""
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
    """One faceted stone: blended boulder silhouette, crosshatch shading, a couple of cracks
    and weather pits. Shared by rock_pile, rock_boulder and rock_cluster."""
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
    """Three medium stones leant together - the "medium" member of the rock family."""
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


# ---------------------------------------------------------------- carried items and worked forms
# The four of these are drawn at carried-item size (one small thing centred and grounded), not
# world-decoration size (rock_pile and friends above) - a single subject rather than a scatter.

def stone():
    """A single hand-held stone, small enough to carry - the same faceted construction as the
    rock family (_stone), one piece rather than a pile."""
    seed = seed_for("stone")
    rng = random.Random(seed)
    c = Canvas(seed)
    color = rgb(0.5, 0.5, 0.52)
    _stone(c, 32, 40, 17, 14, color, seed, rng)
    _ground_shadow_dashes(c, 32, 56, 14, seed + 99)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def wedge():
    """A knapped stone wedge: struck into flat facets that meet at sharp ridges, angular where
    the raw stone (see stone) is rounded - flat planes catching the light differently rather
    than the raw stone's crosshatch curvature and scattered cracks (_stone_facets)."""
    seed = seed_for("wedge")
    rng = random.Random(seed)
    c = Canvas(seed)
    color = rgb(0.46, 0.46, 0.5)
    tip = (34, 57)
    body = poly(jagged_poly(
        [(23, 21), (40, 17), (47, 33), tip, (24, 50), (19, 33)],
        rng, amp=0.7, segments_per_edge=3, smooth_passes=1,
    ))
    c.fill(body, color)

    # Two struck facets fanning down to the same sharpened tip, each its own hatch pass at a
    # different tone - a distinct plane, the way the log rings (_wood_log) sit apart from their
    # bark, rather than one curved gradient.
    ridge = (32, 19)
    facets = (
        ([(23, 21), ridge, tip, (24, 50), (19, 33)], lighten(color, 0.16), seed + 11),
        ([ridge, (40, 17), (47, 33), tip], darken(color, 0.14), seed + 23),
    )
    for points, tone, facet_seed in facets:
        facet_mask = poly(jagged_poly(points, rng, amp=0.3, segments_per_edge=2, smooth_passes=1)) & body
        out_rgb = hatch_fill(facet_mask, tone, facet_seed)
        c.rgb[facet_mask] = out_rgb[facet_mask]
        c.alpha |= facet_mask

    # The ridge line between the facets, and the edge along the tip, are what a struck flake
    # actually shows - inked rather than hatched, since a hatch line here would just blend in.
    ridge_img = Image.new("L", (S, S), 0)
    ImageDraw.Draw(ridge_img).line(
        [(ridge[0] * SCALE, ridge[1] * SCALE), (tip[0] * SCALE, tip[1] * SCALE)],
        fill=255, width=max(1, SCALE // 3))
    c.ink((np.array(ridge_img) > 127) & body)

    glint = poly(jagged_poly([tip, (47, 33), (43, 36), (33, 53)], rng, amp=0.3, segments_per_edge=2)) & body
    c.flat(glint, lighten(color, 0.3))

    _ground_shadow_dashes(c, 30, 58, 12, seed + 99)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def cord():
    """A coil of twisted cordage, wound back on itself rather than stretched taut the way
    _rope_tie binds two things - the same twist ticks along the length, looped."""
    seed = seed_for("cord")
    rng = random.Random(seed)
    c = Canvas(seed)
    color = rgb(0.58, 0.48, 0.28)

    loops = ((32, 38, 20, 13, 0.0), (30, 43, 16, 10, 0.35), (34, 34, 14, 9, -0.3))
    for cx, cy, rx, ry, twist_phase in loops:
        ring = ellipse(cx, cy, rx, ry) & ~ellipse(cx, cy, rx - 3, ry - 2.2)
        out_rgb = hatch_fill(ring, color, seed)
        c.rgb[ring] = out_rgb[ring]
        c.alpha |= ring

        n_ticks = max(10, int(2 * math.pi * max(rx, ry) / 2.2))
        tick_img = Image.new("L", (S, S), 0)
        tick_draw = ImageDraw.Draw(tick_img)
        for i in range(n_ticks):
            a = (i / n_ticks) * 2 * math.pi + twist_phase
            tick_draw.line([((cx + math.cos(a) * rx * 0.6) * SCALE, (cy + math.sin(a) * ry * 0.6) * SCALE),
                            ((cx + math.cos(a) * rx) * SCALE, (cy + math.sin(a) * ry) * SCALE)],
                           fill=255, width=max(1, SCALE // 4))
        c.flat((np.array(tick_img) > 127) & ring, darken(color, 0.32))

    _ground_shadow_dashes(c, 32, 56, 16, seed + 99)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def bag():
    """A drawstring pouch of woven plant fibre, cinched at the neck with a tied cord
    (_rope_tie) - the same material as cord and grass."""
    seed = seed_for("bag")
    rng = random.Random(seed)
    c = Canvas(seed)
    weave = rgb(0.56, 0.46, 0.28)

    body = poly(jagged_poly(
        [(20, 26), (44, 26), (48, 40), (44, 56), (20, 56), (16, 40)],
        rng, amp=0.8, segments_per_edge=3, smooth_passes=1,
    ))
    c.fill(body, weave)

    # Woven creases where the sack sags.
    for x in (24, 30, 36, 41):
        seam_x = x + rng.uniform(-0.6, 0.6)
        c.flat(rect(seam_x, 30, seam_x + 0.6, 54) & body, darken(weave, 0.22))

    neck = poly(jagged_poly([(24, 20), (40, 20), (42, 27), (22, 27)], rng, amp=0.5, segments_per_edge=2))
    c.fill(neck, darken(weave, 0.1))
    _rope_tie(c, (20, 23), (44, 23), rgb(0.62, 0.52, 0.30), seed + 5, width=2.2)

    _ground_shadow_dashes(c, 32, 57, 16, seed + 99)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


# Three lobe arrangements per fruit-tree canopy (main, left, right, top - each (cx, cy, rx,
# ry)): the same four-ellipse formula keeps the silhouette readable as one kind of tree, but
# the proportions genuinely differ per variant, not just the hatch noise.
# Each lobe's top (cy - ry) must stay a couple of pixels below the canvas top (y=0), or
# rasterization clips it to a straight line (a "cut off" canopy in-game). Trim ry, not cy: the
# fruit dots in _APPLE_FRUIT_SPOT_VARIANTS sit at each lobe's (cx, cy).
_FRUIT_TREE_CANOPY_VARIANTS = [
    [(32, 26, 22, 17), (18, 30, 13, 12), (46, 30, 13, 12), (32, 12, 15, 10)],
    [(34, 24, 20, 19), (16, 34, 12, 11), (47, 26, 15, 13), (30, 9, 13, 7)],
    [(31, 29, 24, 15), (14, 28, 14, 13), (49, 32, 12, 11), (33, 15, 17, 10)],
]

# Matching trunk quads (4 corners each) - plain, not jagged, like the canopy (see
# _fruit_tree_bare).
_FRUIT_TREE_TRUNK_VARIANTS = [
    [(29, 44), (35, 44), (35, GROUND_CONTACT_Y), (29, GROUND_CONTACT_Y)],
    [(27, 44), (32, 44), (35, GROUND_CONTACT_Y), (30, GROUND_CONTACT_Y)],
    [(30, 45), (37, 45), (34, GROUND_CONTACT_Y), (27, GROUND_CONTACT_Y)],
]

# Fruit spots per canopy variant (same lobe centres, so the dots land inside whichever canopy
# shows).
_APPLE_FRUIT_SPOT_VARIANTS = [
    ((22, 24, 3), (40, 20, 3), (30, 34, 3), (46, 32, 2), (18, 38, 2)),
    ((34, 24, 3), (16, 34, 3), (47, 26, 3), (30, 9, 2), (38, 30, 2)),
    ((31, 29, 3), (14, 28, 3), (49, 32, 3), (33, 15, 2), (38, 34, 2)),
]
_PEAR_FRUIT_SPOT_VARIANTS = [
    ((24, 22, 3), (42, 22, 3), (32, 36, 3), (44, 34, 2), (20, 36, 2)),
    ((30, 26, 3), (18, 32, 3), (44, 28, 3), (32, 11, 2), (40, 32, 2)),
    ((28, 30, 3), (17, 30, 3), (46, 34, 3), (35, 17, 2), (36, 36, 2)),
]

# Branches radiate from the canopy's centre, not the trunk: bare twig tips poke out anywhere
# around a real crown. The canopy union is nearly convex, so most angles need about the same
# 20-32 unit reach to clear it; two twigs per variant, at angles that land in different places
# on the crown. Screen-space degrees (0 = right, 90 = down, 180 = left, 270 = up, clockwise)
# from BRANCH_ORIGIN.
BRANCH_ORIGIN = (32, 24)
_BRANCH_ANGLE_VARIANTS = [
    [70, 110],   # low, near the trunk seam
    [210, 330],  # up near the shoulders, poking out of the crown itself
    [110, 210],  # one of each, asymmetric; 90/270 would sit behind the trunk, invisible
]


def _variant_name(name, variant):
    return name if variant == 0 else f"{name}_v{variant}"


def _fruit_tree_canopy(variant=0):
    mask = None
    for cx, cy, rx, ry in _FRUIT_TREE_CANOPY_VARIANTS[variant]:
        lobe = ellipse(cx, cy, rx, ry)
        mask = lobe if mask is None else (mask | lobe)
    return mask


def _fruit_tree_split(name, variant=0):
    trunk = rgb(0.34, 0.24, 0.15)
    foliage = rgb(0.30, 0.38, 0.22)
    split_name = _variant_name(name, variant)
    trunk_mask = poly(_FRUIT_TREE_TRUNK_VARIANTS[variant])
    return split_trunk_canopy(split_name, trunk_mask, [_fruit_tree_canopy(variant)], trunk, foliage)


# ---------------------------------------------------------------- branches (own layer)
# A branch routed under the canopy reads as a stub, since the lobes nearly reach the trunk top
# (_FRUIT_TREE_CANOPY_VARIANTS). Branches therefore live entirely outside every canopy
# variant's footprint, like the twig tips past a real tree's leaf mass, composited on top - no
# trunk/canopy/branch combination can draw one across the leaves.

def _canopy_variant_union():
    mask = None
    for i in range(len(_FRUIT_TREE_CANOPY_VARIANTS)):
        piece = _fruit_tree_canopy(i)
        mask = piece if mask is None else (mask | piece)
    return mask


def _twig_anchor(origin, angle_deg, union_mask, margin=2, max_radius=40):
    """Walks outward from origin at angle_deg (64-unit grid) and returns a point margin units
    past the last radius still inside union_mask, so the twig clears every canopy variant even
    when origin sits at the edge of one. union_mask is S-by-S, so probes are scaled up before
    indexing."""
    ox, oy = origin
    rad = math.radians(angle_deg)
    dx, dy = math.cos(rad), math.sin(rad)
    exit_r = 0
    for r in range(max_radius):
        px = int(round((ox + (r * dx)) * SCALE))
        py = int(round((oy + (r * dy)) * SCALE))
        if 0 <= px < S and 0 <= py < S and union_mask[py, px]:
            exit_r = r
    return ox + ((exit_r + margin) * dx), oy + ((exit_r + margin) * dy)


def _twig_tuft(anchor_x, anchor_y, angle_deg, spread_deg, length, width, rng):
    """A two-pronged twig - reads as a branch where a single stick would not."""
    mask = None
    for sign in (-1, 1):
        rad = math.radians(angle_deg + (sign * spread_deg / 2))
        tip_x = anchor_x + (length * math.cos(rad))
        tip_y = anchor_y + (length * math.sin(rad))
        points = [
            (anchor_x - (math.sin(rad) * width / 2), anchor_y + (math.cos(rad) * width / 2)),
            (anchor_x + (math.sin(rad) * width / 2), anchor_y - (math.cos(rad) * width / 2)),
            (tip_x + (math.sin(rad) * width * 0.1), tip_y - (math.cos(rad) * width * 0.1)),
            (tip_x - (math.sin(rad) * width * 0.1), tip_y + (math.cos(rad) * width * 0.1)),
        ]
        prong = poly(jagged_poly(points, rng, amp=0.35, segments_per_edge=2, smooth_passes=1))
        mask = prong if mask is None else (mask | prong)
    return mask


def _generic_tree_branch_layer(branch_variant):
    """One branches.png (plus _v1/_v2) shared by every kind with the fruit-tree canopy (apple,
    pear, deciduous_tree - ResourceNodeView.BranchesTexturePathFor). Seeded off a fixed name,
    since it belongs to no kind."""
    split_name = _variant_name("tree_branches", branch_variant)
    seed = seed_for(split_name)
    rng = random.Random(seed)
    trunk_color = rgb(0.34, 0.24, 0.15)
    union_mask = _canopy_variant_union()

    mask = None
    for angle in _BRANCH_ANGLE_VARIANTS[branch_variant]:
        anchor_x, anchor_y = _twig_anchor(BRANCH_ORIGIN, angle, union_mask)
        tuft = _twig_tuft(anchor_x, anchor_y, angle, 30, 6, 2.0, rng)
        mask = tuft if mask is None else (mask | tuft)

    c = Canvas(seed)
    c.fill(mask, trunk_color)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def _fruit_tree_bare(name, variant=0):
    """Shared deciduous canopy for the fruit trees - only the fruit differs, so both read as the
    same kind of tree. A union of ellipses, not jagged (see the jagged shapes note);
    rough_outline carries the inked edge.

    This is the whole tree: a picked-clean node renders exactly this. _fruit_overlay is a
    separate layer (ResourceNodeView), so no bare texture per kind is needed."""
    return _fruit_tree_split(name, variant)[0]


def _fruit_overlay(name, fruit_color, fruit_spots, variant=0):
    """Only the fruit dots, masked to the canopy footprint; no outline, since it is always
    composited over the bare tree."""
    seed = seed_for(_variant_name(name, variant))
    c = Canvas(seed)
    canopy = _fruit_tree_canopy(variant)
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
    return _fruit_overlay("apple_tree", rgb(0.70, 0.18, 0.16), _APPLE_FRUIT_SPOT_VARIANTS[0])


def apple_tree_trunk_v1():
    return _fruit_tree_split("apple_tree", 1)[1]


def apple_tree_canopy_v1():
    return _fruit_tree_split("apple_tree", 1)[2]


def apple_tree_fruit_v1():
    return _fruit_overlay("apple_tree", rgb(0.70, 0.18, 0.16), _APPLE_FRUIT_SPOT_VARIANTS[1], variant=1)


def apple_tree_trunk_v2():
    return _fruit_tree_split("apple_tree", 2)[1]


def apple_tree_canopy_v2():
    return _fruit_tree_split("apple_tree", 2)[2]


def apple_tree_fruit_v2():
    return _fruit_overlay("apple_tree", rgb(0.70, 0.18, 0.16), _APPLE_FRUIT_SPOT_VARIANTS[2], variant=2)


def pear_tree():
    return _fruit_tree_bare("pear_tree")


def pear_tree_trunk():
    return _fruit_tree_split("pear_tree")[1]


def pear_tree_canopy():
    return _fruit_tree_split("pear_tree")[2]


def pear_tree_fruit():
    return _fruit_overlay("pear_tree", rgb(0.62, 0.68, 0.20), _PEAR_FRUIT_SPOT_VARIANTS[0])


def pear_tree_trunk_v1():
    return _fruit_tree_split("pear_tree", 1)[1]


def pear_tree_canopy_v1():
    return _fruit_tree_split("pear_tree", 1)[2]


def pear_tree_fruit_v1():
    return _fruit_overlay("pear_tree", rgb(0.62, 0.68, 0.20), _PEAR_FRUIT_SPOT_VARIANTS[1], variant=1)


def pear_tree_trunk_v2():
    return _fruit_tree_split("pear_tree", 2)[1]


def pear_tree_canopy_v2():
    return _fruit_tree_split("pear_tree", 2)[2]


def pear_tree_fruit_v2():
    return _fruit_overlay("pear_tree", rgb(0.62, 0.68, 0.20), _PEAR_FRUIT_SPOT_VARIANTS[2], variant=2)


def deciduous_tree():
    """The fruit-tree canopy (_fruit_tree_bare) as a wood resource (MapLoader): nothing to
    pick, so no fruit overlay."""
    return _fruit_tree_bare("deciduous_tree")


def deciduous_tree_trunk():
    return _fruit_tree_split("deciduous_tree")[1]


def deciduous_tree_canopy():
    return _fruit_tree_split("deciduous_tree")[2]


def deciduous_tree_trunk_v1():
    return _fruit_tree_split("deciduous_tree", 1)[1]


def deciduous_tree_canopy_v1():
    return _fruit_tree_split("deciduous_tree", 1)[2]


def deciduous_tree_trunk_v2():
    return _fruit_tree_split("deciduous_tree", 2)[1]


def deciduous_tree_canopy_v2():
    return _fruit_tree_split("deciduous_tree", 2)[2]


def tree_branches():
    return _generic_tree_branch_layer(0)


def tree_branches_v1():
    return _generic_tree_branch_layer(1)


def tree_branches_v2():
    return _generic_tree_branch_layer(2)


def bush():
    """A low, trunkless clump - the tree canopies' union of ellipses, wider and lower."""
    seed = seed_for("bush")
    c = Canvas(seed)
    foliage = rgb(0.26, 0.36, 0.18)
    mask = (ellipse(32, 49, 20, 14) | ellipse(15, 53, 12, 10)
            | ellipse(49, 53, 12, 10) | ellipse(32, 40, 15, 12))
    c.fill(mask, foliage)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def grass():
    """A few jagged blades of varying height and lean - thin enough that rough_outline's ring,
    not crosshatch density, carries the read."""
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
    """Denser and wider than the `grass` tuft - a patch worth gathering, not a stray blade."""
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


def _bark_dashes_along(c, bark, x0, x1, ys, rng):
    """Long broken dashes along a lying trunk - bark grain follows the wood's axis, as in the
    reference logs, not a crosshatch."""
    for y in ys:
        x = x0 + rng.uniform(0, 3)
        while x < x1:
            seg = rng.uniform(3.5, 8.0)
            gap = rng.uniform(1.2, 3.0)
            yy = y + rng.uniform(-0.5, 0.5)
            c.flat(rect(x, yy, min(x + seg, x1), yy + 0.7), darken(bark, 0.3))
            x += seg + gap


def _log_knot(c, cx, cy, bark, rng):
    c.flat(ellipse(cx, cy, 2.2, 1.6), darken(bark, 0.45))
    c.flat(ellipse(cx + 0.3, cy - 0.2, 1.0, 0.7), lighten(bark, 0.15))


def tree_stump():
    """A cut stump: bark rind around a short flared drum with splayed roots, a foreshortened
    cut face and a torn splinter at the rim - broken, not machined."""
    seed = seed_for("tree_stump")
    rng = random.Random(seed)
    c = Canvas(seed)
    bark = rgb(0.36, 0.24, 0.14)
    core = rgb(0.68, 0.52, 0.32)

    side_pts = [(17.5, 37.5), (46.5, 37.5), (47.5, 48), (55, GROUND_CONTACT_Y), (46, GROUND_CONTACT_Y),
                (38, GROUND_CONTACT_Y + 0.5), (26, GROUND_CONTACT_Y + 0.5), (18, GROUND_CONTACT_Y),
                (9, GROUND_CONTACT_Y), (16.5, 48)]
    side = poly(jagged_poly(side_pts, rng, amp=0.7, segments_per_edge=3, smooth_passes=1))
    c.fill(side, bark)
    for x in (22, 27.5, 33, 38.5, 43):
        sx = x + rng.uniform(-0.8, 0.8)
        y0 = 40 + rng.uniform(0, 3)
        c.flat(rect(sx, y0, sx + 0.8, GROUND_CONTACT_Y - rng.uniform(0.5, 3)) & side, darken(bark, 0.28))
    _log_knot(c, 41, 49, bark, rng)

    splinter = poly(jagged_poly([(19, 38), (23, 38), (21.5, 32.5)], rng, amp=0.3,
                                 segments_per_edge=2, smooth_passes=1))
    c.fill(splinter, darken(bark, 0.1))

    _wood_log(c, 32, 37.5, 15, 5.8, bark, core, seed)
    _ground_shadow_dashes(c, 32, GROUND_CONTACT_Y, 17, seed + 99)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def fallen_log():
    """A lying log: a barrel tapering from one end to the other, a snapped branch stub, and the
    cut end seen obliquely as a narrow upright ellipse."""
    seed = seed_for("fallen_log")
    rng = random.Random(seed)
    c = Canvas(seed)
    bark = rgb(0.38, 0.26, 0.15)
    core = rgb(0.70, 0.53, 0.33)

    body_pts = [(15, 44), (30, 43.5), (49, 45.5), (53.5, 50), (52.5, 56.5),
                (34, 58), (16, GROUND_CONTACT_Y), (11.5, 51.5)]
    body = poly(jagged_poly(body_pts, rng, amp=0.7, segments_per_edge=3, smooth_passes=1))
    c.fill(body, bark)
    _bark_dashes_along(c, bark, 19, 51, (46.5, 49.5, 52.5, 55.5), rng)
    _log_knot(c, 31, 49, bark, rng)
    _log_knot(c, 43, 53.5, bark, rng)

    stub = poly(jagged_poly([(37, 44.5), (40.5, 44.5), (44.5, 37.5), (42.5, 36.5)],
                            rng, amp=0.4, segments_per_edge=2, smooth_passes=1))
    c.fill(stub, darken(bark, 0.08))
    c.flat(ellipse(43.6, 37.2, 1.3, 0.9), core)

    _wood_log(c, 14, 51.3, 5.2, 7.3, bark, core, seed + 1)
    _ground_shadow_dashes(c, 33, GROUND_CONTACT_Y, 21, seed + 99)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def fern():
    """A low fan of arched fronds from one base point - fuller than grass's upright tuft."""
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
    """A stem with a ring of petals round a bright centre - the one spot of saturated colour
    the palette allows (visual plan, "Art constraints"), being a tiny accent."""
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


# ---------------------------------------------------------------- clouds
# The engraved-cloud convention in docs/ZemanConceptArt.png (sky above the mountains, "Unknown"
# panel) is three things hatch_fill does not supply: each lobe's rim rolls inward into a volute
# (the curl is the edge, not a doodle in the middle), the shading lines follow the lobe's
# contour rather than a diagonal grid, and the clump is sheared flat along its underside. The
# reference's trailing wisp tails read as a plate under the cloud at sprite scale.


def _circle_points(cx, cy, r, n=12):
    return [(cx + math.cos(2 * math.pi * i / n) * r, cy + math.sin(2 * math.pi * i / n) * r)
            for i in range(n)]


def _polyline_mask(points, widths):
    """A polyline with a per-segment stroke width (grid units); PIL's line() takes one width,
    so it is drawn per segment with round joints."""
    img = _blank()
    draw = ImageDraw.Draw(img)
    for (x0, y0), (x1, y1), w in zip(points, points[1:], widths):
        draw.line([(x0 * SCALE, y0 * SCALE), (x1 * SCALE, y1 * SCALE)],
                  fill=255, width=max(1, round(w * SCALE)), joint="curve")
    return _to_mask(img)


def _cloud_volute(lobe, rim_angle, rng):
    """One rim curl: an inward spiral whose outermost point sits on the lobe's rim at rim_angle,
    so the outline ink runs into it like a wave crest. Always rolls up and over first, which in
    y-down coordinates means increasing angle on a lobe's left, decreasing on its right.
    Returns (ink_mask, (center_x, center_y, radius))."""
    cx, cy, r = lobe
    rs = r * rng.uniform(0.38, 0.50)
    ccx = cx + math.cos(rim_angle) * (r - rs - 0.4)
    ccy = cy + math.sin(rim_angle) * (r - rs - 0.4)
    direction = 1 if math.cos(rim_angle) < 0 else -1
    turns = rng.uniform(1.6, 2.1)
    n = 40
    pts, widths = [], []
    for i in range(n):
        t = i / (n - 1)
        angle = rim_angle + direction * turns * 2 * math.pi * t
        rad = rs * (1.0 - 0.9 * t)
        pts.append((ccx + math.cos(angle) * rad, ccy + math.sin(angle) * rad))
        widths.append(1.05 - 0.5 * t)
    ink = _polyline_mask(pts, widths)
    ink |= ellipse(pts[-1][0], pts[-1][1], 0.7, 0.7)
    return ink, (ccx, ccy, rs)


def _cloud_shade(mask, lobes, volutes, base_color, seed):
    """Contour hatching: each pixel belongs to its nearest lobe and its lines run concentric to
    it, dense on the underside and toward the base, sparse on the lit top. Inside a volute the
    lines run concentric to the spiral and darken toward its core. Reuses _hatch_direction with
    polar coordinates, so the line quality matches every other sprite."""
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return np.zeros((S, S, 3), dtype=np.uint8)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    px, py = _XX / SCALE, _YY / SCALE

    best = np.full((S, S), np.inf)
    contour = np.zeros((S, S))
    along = np.zeros((S, S))
    rel_x = np.zeros((S, S))
    rel_y = np.zeros((S, S))
    for cx, cy, r in lobes:
        dx, dy = px - cx, py - cy
        dist = np.hypot(dx, dy)
        sel = dist / r < best
        best = np.where(sel, dist / r, best)
        contour = np.where(sel, dist * SCALE, contour)
        # arc length, with the wrap seam at the lobe's bottom where another lobe or the
        # base usually covers it instead of on its exposed left flank
        seam = np.mod(np.arctan2(dy, dx) - math.pi / 2 + math.pi, 2 * math.pi) - math.pi
        along = np.where(sel, seam * r * SCALE, along)
        rel_x = np.where(sel, dx / r, rel_x)
        rel_y = np.where(sel, dy / r, rel_y)

    # Clean paper from crown to just past the equator, then contour lines gather on the
    # underside - a puff, not a target. The clump darkens only gently toward its shelf.
    t_glob = np.clip((_YY - y0) / max(y1 - y0, 1), 0.0, 1.0)
    t_lobe = np.clip((rel_y + 0.05) / 0.95, 0.0, 1.0)
    tone = 0.10 + 0.32 * t_lobe ** 1.4 + 0.06 * np.clip(rel_x, 0.0, 1.0) * t_lobe + 0.10 * t_glob ** 2

    # inside a volute the spiral's turns do the drawing: paper between them, a dark knot at the
    # core
    in_volute = np.zeros((S, S), dtype=bool)
    for ccx, ccy, rs in volutes:
        dx, dy = px - ccx, py - ccy
        dist = np.hypot(dx, dy)
        inside = dist < rs * 1.08
        in_volute |= inside
        contour = np.where(inside, dist * SCALE, contour)
        along = np.where(inside, np.arctan2(dy, dx) * rs * SCALE, along)
        core = np.clip(1.0 - dist / rs, 0.0, 1.0)
        tone = np.where(inside, 0.04 + 0.70 * core ** 3.2, tone)

    tone = np.clip(tone, 0.0, 0.86)
    salt = (seed % 97) * 11
    # thinner than hatch_fill's 1.3: contour lines that thicken into bands stop reading as pen
    # strokes
    line_a = _hatch_direction(contour, along, tone, 0.95, 0.12, salt + 1)
    line_b = _hatch_direction(_XX + _YY, _XX - _YY, tone, 2.0, 0.60, salt + 11)

    # hatch_fill's diagonal crosshatch over the contour lines, so the clouds sit in the same
    # drawing as every other sprite. Kept out of the volute discs, and lighter than
    # hatch_fill's 0.86 since the contour lines already ink the undersides.
    diag = max((x1 - x0) + (y1 - y0), 1)
    diag_tone = np.clip(((_XX - x0) + (_YY - y0)) / diag, 0.0, 1.0) * 0.58
    line_c = _hatch_direction(_XX - _YY, _XX + _YY, diag_tone, 1.3, 0.12, salt + 21)
    line_d = _hatch_direction(_XX + _YY, _XX - _YY, diag_tone, 2.0, 0.55, salt + 31)
    diagonal = (line_c | line_d) & ~in_volute

    ink_mask = mask & (line_a | line_b | diagonal)

    out_rgb = np.zeros((S, S, 3), dtype=np.uint8)
    out_rgb[mask] = base_color
    out_rgb[ink_mask] = INK
    return out_rgb


def _cloud(name, lobes, base_y, rng):
    """Round lobes (cx, cy, r) sheared flat along base_y, outer rims rolling into volutes. Not
    grounded: CloudScatter.cs floats these well above the terrain. Muted and cool, not white -
    shaded white read as snow."""
    seed = seed_for(name)
    c = Canvas(seed)
    paper = rgb(0.70, 0.73, 0.78)

    mask = np.zeros((S, S), dtype=bool)
    for cx, cy, r in lobes:
        mask |= poly(jagged_poly(_circle_points(cx, cy, r), rng, amp=r * 0.07,
                                 segments_per_edge=3, smooth_passes=2))
    # flat, slightly wavy underside - the reference clouds sit on a shelf
    wave = 0.7 * np.sin(_XX / SCALE * 0.55 + rng.uniform(0, 6.28))
    mask &= (_YY / SCALE) <= base_y + wave

    cloud_cx = sum(cx for cx, _, _ in lobes) / len(lobes)
    volutes, ink = [], np.zeros((S, S), dtype=bool)
    for i, (cx, cy, r) in enumerate(lobes):
        # curls sit on each lobe's outer upper flank, the side the wind would roll; the biggest
        # lobe always gets one, the rest usually
        biggest = r == max(l[2] for l in lobes)
        if not biggest and rng.random() > 0.8:
            continue
        outward_left = cx < cloud_cx - 2 or (abs(cx - cloud_cx) <= 2 and rng.random() < 0.5)
        # a rim point buried inside a neighbouring lobe cannot roll - try the outer flank, then
        # the other, before giving up on this lobe
        rim_angle = None
        for attempt in range(8):
            left = outward_left if attempt < 4 else not outward_left
            candidate = math.radians(rng.uniform(165, 240) if left else rng.uniform(300, 375))
            rim = (cx + math.cos(candidate) * r, cy + math.sin(candidate) * r)
            if not any(math.hypot(rim[0] - ox, rim[1] - oy) < orad * 0.9
                       for j, (ox, oy, orad) in enumerate(lobes) if j != i):
                rim_angle = candidate
                break
        if rim_angle is None:
            continue
        curl_ink, volute = _cloud_volute((cx, cy, r), rim_angle, rng)
        volutes.append(volute)
        ink |= curl_ink

    out_rgb = _cloud_shade(mask, lobes, volutes, paper, seed)
    c.rgb[mask] = out_rgb[mask]
    c.alpha |= mask
    c.ink(ink & mask)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


# Three independent lobe layouts, not one shape rescaled, so a scatter reads as a varied sky -
# the same reasoning as ROBE_VARIANTS.
def cloud_1():
    rng = random.Random(seed_for("cloud_1"))
    return _cloud("cloud_1", [(31, 31, 13), (17, 38, 9), (45, 35, 10), (55, 41, 6)],
                  base_y=46, rng=rng)


def cloud_2():
    rng = random.Random(seed_for("cloud_2"))
    return _cloud("cloud_2", [(36, 28, 13), (22, 34, 10), (49, 34, 9), (11, 40, 6)],
                  base_y=44, rng=rng)


def cloud_3():
    rng = random.Random(seed_for("cloud_3"))
    return _cloud("cloud_3", [(29, 28, 12), (41, 35, 10), (17, 37, 8)],
                  base_y=45, rng=rng)


def selection_marker():
    """A downward-pointing marker above a selected unit's head - a flat engraved emblem, so a
    clean triangle with only a whisper of jag."""
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
    "conifer_tree_trunk_v1": conifer_tree_trunk_v1,
    "conifer_tree_canopy_v1": conifer_tree_canopy_v1,
    "conifer_tree_trunk_v2": conifer_tree_trunk_v2,
    "conifer_tree_canopy_v2": conifer_tree_canopy_v2,
    "deciduous_tree": deciduous_tree,
    "deciduous_tree_trunk": deciduous_tree_trunk,
    "deciduous_tree_canopy": deciduous_tree_canopy,
    "deciduous_tree_trunk_v1": deciduous_tree_trunk_v1,
    "deciduous_tree_canopy_v1": deciduous_tree_canopy_v1,
    "deciduous_tree_trunk_v2": deciduous_tree_trunk_v2,
    "deciduous_tree_canopy_v2": deciduous_tree_canopy_v2,
    "apple_tree": apple_tree,
    "apple_tree_trunk": apple_tree_trunk,
    "apple_tree_canopy": apple_tree_canopy,
    "apple_tree_fruit": apple_tree_fruit,
    "apple_tree_trunk_v1": apple_tree_trunk_v1,
    "apple_tree_canopy_v1": apple_tree_canopy_v1,
    "apple_tree_fruit_v1": apple_tree_fruit_v1,
    "apple_tree_trunk_v2": apple_tree_trunk_v2,
    "apple_tree_canopy_v2": apple_tree_canopy_v2,
    "apple_tree_fruit_v2": apple_tree_fruit_v2,
    "pear_tree": pear_tree,
    "pear_tree_trunk": pear_tree_trunk,
    "pear_tree_canopy": pear_tree_canopy,
    "pear_tree_trunk_v1": pear_tree_trunk_v1,
    "pear_tree_canopy_v1": pear_tree_canopy_v1,
    "pear_tree_fruit_v1": pear_tree_fruit_v1,
    "pear_tree_trunk_v2": pear_tree_trunk_v2,
    "pear_tree_canopy_v2": pear_tree_canopy_v2,
    "pear_tree_fruit_v2": pear_tree_fruit_v2,
    "pear_tree_fruit": pear_tree_fruit,
    "tree_branches": tree_branches,
    "tree_branches_v1": tree_branches_v1,
    "tree_branches_v2": tree_branches_v2,
    "bush": bush,
    "grass": grass,
    "wild_grass": wild_grass,
    "flower": flower,
    "fern": fern,
    "cloud_1": cloud_1,
    "cloud_2": cloud_2,
    "cloud_3": cloud_3,
    "rock_pile": rock_pile,
    "rock_boulder": rock_boulder,
    "rock_cluster": rock_cluster,
    "stone": stone,
    "wedge": wedge,
    "cord": cord,
    "bag": bag,
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
