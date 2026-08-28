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
import random
import zlib
import numpy as np
from PIL import Image, ImageDraw

SCALE = 4
S = 64 * SCALE  # canvas size; shape coordinates below are still authored on the old 64-unit grid


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


# ---------------------------------------------------------------- richer base shapes
# (person) - a nipped-waist, scalloped-hem robe; a tapered bent arm; a hood with an
# actual cowl point; a boot with a heel and a toe - built as multi-point silhouettes
# instead of a trapezoid/quad/ellipse-ring/rectangle before any jagging is applied.

def robe_silhouette(rng, sx, sy):
    waist_pull = rng.uniform(1.5, 3.0)
    hem_y = sy(52)
    fold_count = rng.randint(3, 4)
    hem_xs = np.linspace(sx(44), sx(20), fold_count * 2 + 1)
    pts = [
        (sx(26), sy(20)), (sx(38), sy(20)),
        (sx(42), sy(25)),
        (sx(40) - waist_pull, sy(35)),
        (sx(44), sy(46)),
    ]
    for i, x in enumerate(hem_xs):
        y = hem_y if i % 2 == 0 else hem_y - rng.uniform(3.0, 5.0)
        pts.append((x, y))
    pts += [
        (sx(20), sy(46)),
        (sx(24) + waist_pull, sy(35)),
        (sx(22), sy(25)),
    ]
    return pts


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

    # cloak: nipped waist + scalloped hem, jagged on top for hand-cut texture
    body_pts = robe_silhouette(rng, sx, sy)
    body = poly(jagged_poly(body_pts, rng, amp=1.6, segments_per_edge=3, smooth_passes=2))
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


def wood():
    seed = seed_for("wood")
    c = Canvas(seed)
    bark = rgb(0.40, 0.25, 0.10)
    core = rgb(0.72, 0.55, 0.34)
    ring = darken(core, 0.28)
    for cx, cy, r1, r2, r3, r4 in (
        (32, 22, (14, 12), (9, 8), (5, 4), (3, 2)),
        (20, 42, (15, 13), (10, 9), (6, 5), (3, 3)),
        (45, 44, (14, 12), (9, 8), (5, 4), (3, 2)),
    ):
        c.fill(ellipse(cx, cy, *r1), bark)
        c.fill(ellipse(cx, cy, *r2), core)
        c.flat(ellipse(cx, cy, *r3) & ~ellipse(cx, cy, *r4), ring)
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
        (52 + rng.uniform(-1, 1), 58 + wall_h_jitter),
        (12 + rng.uniform(-1, 1), 58 - wall_h_jitter),
    ]
    walls = poly(jagged_poly(wall_pts, rng, amp=0.9, segments_per_edge=4, smooth_passes=1))
    c.fill(walls, wall)
    for x in (20, 28, 36, 44):
        seam_x = x + rng.uniform(-0.6, 0.6)
        c.flat(rect(seam_x, 31, seam_x + 0.6, 57) & walls, darken(wall, 0.28))

    roof_apex = (32 + rng.uniform(-1.5, 1.5), 6 + rng.uniform(-1, 1))
    roof_pts = [roof_apex, (58, 32), (6, 32)]
    roof_body = poly(jagged_poly(roof_pts, rng, amp=1.1, segments_per_edge=5, smooth_passes=2))
    # kept inset from the triangle's own corners, which taper to a sliver too thin to
    # reliably fuse with a fringe drip even after closing
    fringe = thatch_fringe_mask(11, 53, 32, rng)
    close_r = max(3, SCALE * 2)
    roof_mask = erode(dilate(roof_body | fringe, close_r), close_r)
    c.fill(roof_mask, roof)

    door = poly(jagged_poly([(26, 40), (38, 40), (37, 58), (27, 58)], rng, amp=0.5, segments_per_edge=3))
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


def conifer_tree():
    seed = seed_for("conifer_tree")
    rng = random.Random(seed)
    c = Canvas(seed)
    trunk_base = rgb(0.34, 0.24, 0.15)
    foliage = rgb(0.36, 0.42, 0.26)

    tiers, trunk_top_y = random_conifer_tiers(rng)

    trunk_w = rng.uniform(2.5, 4.0)
    trunk_lean = rng.uniform(-1.5, 1.5)
    trunk_top = 32 + rng.uniform(-1, 1)
    trunk = jagged_poly([
        (trunk_top - trunk_w, trunk_top_y),
        (trunk_top + trunk_w, trunk_top_y),
        (trunk_top + trunk_w + trunk_lean, 58),
        (trunk_top - trunk_w + trunk_lean, 58),
    ], rng, amp=0.8, segments_per_edge=3, smooth_passes=1)
    c.fill(poly(trunk), trunk_base)

    for apex, right, left in tiers:
        tier_mask = lobe_cluster_mask(apex, left, right, rng, rows=rng.choice([2, 3, 3]))
        c.fill(tier_mask, foliage)

    c.rough_outline(width=max(1, SCALE // 2))
    return c


def rock_pile():
    seed = seed_for("rock_pile")
    c = Canvas(seed)
    stone = rgb(0.5, 0.5, 0.52)
    c.fill(ellipse(22, 44, 14, 11), stone)
    c.fill(ellipse(40, 46, 13, 10), darken(stone, 0.08))
    c.fill(ellipse(32, 36, 11, 10), lighten(stone, 0.1))
    c.flat(rect(19, 42, 25, 43), darken(stone, 0.35))
    c.flat(rect(36, 44, 42, 45), darken(stone, 0.35))
    c.rough_outline(width=max(1, SCALE // 2))
    return c


def _fruit_tree_canopy():
    return (ellipse(32, 26, 22, 17) | ellipse(18, 30, 13, 12)
            | ellipse(46, 30, 13, 12) | ellipse(32, 12, 15, 12))


def _fruit_tree_bare(name):
    """Shared deciduous canopy for the fruit-tree sprites - only the fruit color/
    placement differs between kinds, so the two trees stay readable as "the same kind
    of tree" at a glance. The canopy is a union of ellipses, not jagged (see the note
    above jagged_poly) - rough_outline alone carries the hand-inked edge.

    This is the WHOLE tree - a picked-clean node renders exactly this, with no fruit.
    _fruit_overlay is a second, separately composited layer (see ResourceNodeView) so a
    node with no stock left doesn't need its own distinct "bare" texture asset."""
    seed = seed_for(name)
    c = Canvas(seed)
    trunk = rgb(0.34, 0.24, 0.15)
    foliage = rgb(0.30, 0.38, 0.22)
    c.fill(rect(29, 44, 35, 58), trunk)
    c.fill(_fruit_tree_canopy(), foliage)
    c.rough_outline(width=max(1, SCALE // 2))
    return c


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


def apple_tree_fruit():
    return _fruit_overlay(
        "apple_tree", rgb(0.70, 0.18, 0.16),
        ((22, 24, 3), (40, 20, 3), (30, 34, 3), (46, 32, 2), (18, 38, 2)))


def pear_tree():
    return _fruit_tree_bare("pear_tree")


def pear_tree_fruit():
    return _fruit_overlay(
        "pear_tree", rgb(0.62, 0.68, 0.20),
        ((24, 22, 3), (42, 22, 3), (32, 36, 3), (44, 34, 2), (20, 36, 2)))


def deciduous_tree():
    """Same bare-canopy shape and construction as the fruit trees (see _fruit_tree_bare) -
    purely decorative background filler (TerrainRenderer.ScatterDecoration), not a gameplay
    resource, so it never needs a fruit overlay."""
    return _fruit_tree_bare("deciduous_tree")


def bush():
    """A low, trunkless clump - the same union-of-ellipses construction as the tree
    canopies, just wider and closer to the ground."""
    seed = seed_for("bush")
    c = Canvas(seed)
    foliage = rgb(0.26, 0.36, 0.18)
    mask = (ellipse(32, 42, 20, 14) | ellipse(15, 46, 12, 10)
            | ellipse(49, 46, 12, 10) | ellipse(32, 33, 15, 12))
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
            [(bx - 2.2, 58), (bx + lean, 58 - height), (bx + 2.2, 58)],
            rng, amp=0.5, segments_per_edge=2, smooth_passes=1)
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
    c.fill(rect(31, 38, 33, 58), stem)
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
    "wood": wood,
    "apple": apple,
    "pear": pear,
    "potato": potato,
    "mushroom": mushroom,
    "storage_hut": storage_hut,
    "grave_unmarked": grave_unmarked,
    "grave_marked": grave_marked,
    "conifer_tree": conifer_tree,
    "deciduous_tree": deciduous_tree,
    "apple_tree": apple_tree,
    "apple_tree_fruit": apple_tree_fruit,
    "pear_tree": pear_tree,
    "pear_tree_fruit": pear_tree_fruit,
    "bush": bush,
    "grass": grass,
    "flower": flower,
    "rock_pile": rock_pile,
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
