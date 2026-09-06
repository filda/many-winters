#!/usr/bin/env python3
"""
Generates the boot-splash title page for the Godot build: an engraved landscape plate in
the vein of Karel Zeman's paper dioramas (see docs/ZemanConceptArt.png for the target
look - the top-left title block of that sheet is what this reproduces at game size).

Same drawing rules as generate_sprites.py: tone comes from the density of hand-ruled
crosshatch lines, never from a blended gradient; every shape gets an uneven ink contour;
the palette is the concept sheet's own swatch row. The generator is self-contained rather
than importing generate_sprites.py because that module hard-wires a 256x256 sprite canvas.

Run:  python3 generate_splash.py <output.png>
"""

import math
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H = 1920, 1080
YY, XX = np.mgrid[0:H, 0:W]

# ---------------------------------------------------------------- palette (concept sheet swatches)
INK = (36, 26, 20)
PAPER = (224, 215, 196)
PAPER_EDGE = (192, 178, 152)
TAN = (196, 176, 138)
GREY_BLUE = (122, 136, 150)
SLATE = (74, 92, 114)
MOSS = (88, 104, 76)
OLIVE = (94, 94, 54)
BROWN = (100, 68, 44)
RUST = (142, 60, 38)
DARK_BROWN = (56, 36, 30)
CHARCOAL = (44, 42, 42)
MOON = (238, 228, 202)
SNOW = (230, 228, 220)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def lighten(c, t):
    return mix(c, (255, 255, 255), t)


def darken(c, t):
    return mix(c, (0, 0, 0), t)


# ---------------------------------------------------------------- masks

def blank_mask():
    return np.zeros((H, W), dtype=bool)


def draw_mask(fn):
    img = Image.new("L", (W, H), 0)
    fn(ImageDraw.Draw(img))
    return np.array(img) > 127


def poly(points):
    return draw_mask(lambda d: d.polygon([tuple(p) for p in points], fill=255))


def ellipse(cx, cy, rx, ry):
    return draw_mask(lambda d: d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255))


def _bbox(mask, pad=0):
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return None
    return max(xs.min() - pad, 0), min(xs.max() + pad + 1, W), max(ys.min() - pad, 0), min(ys.max() + pad + 1, H)


def _dilate_local(m):
    out = m.copy()
    out[1:, :] |= m[:-1, :]
    out[:-1, :] |= m[1:, :]
    out[:, 1:] |= m[:, :-1]
    out[:, :-1] |= m[:, 1:]
    out[1:, 1:] |= m[:-1, :-1]
    out[:-1, :-1] |= m[1:, 1:]
    out[1:, :-1] |= m[:-1, 1:]
    out[:-1, 1:] |= m[1:, :-1]
    return out


def dilate(mask, r=1):
    """Works on the mask's own bounding box only - a full-plate morphology per shape made
    the ~150-shape scene take minutes."""
    box = _bbox(mask, pad=r + 1)
    if box is None:
        return mask.copy()
    x0, x1, y0, y1 = box
    local = mask[y0:y1, x0:x1]
    for _ in range(r):
        local = _dilate_local(local)
    out = blank_mask()
    out[y0:y1, x0:x1] = local
    return out


def erode(mask, r=1):
    box = _bbox(mask, pad=r + 1)
    if box is None:
        return mask.copy()
    x0, x1, y0, y1 = box
    local = ~mask[y0:y1, x0:x1]
    for _ in range(r):
        local = _dilate_local(local)
    out = blank_mask()
    out[y0:y1, x0:x1] = ~local
    return out


def jagged(points, rng, amp=6.0, segments_per_edge=6):
    """Break each straight edge into wobbly segments so nothing reads as ruler-drawn."""
    out = []
    n = len(points)
    for i in range(n):
        x0, y0 = points[i]
        x1, y1 = points[(i + 1) % n]
        for k in range(segments_per_edge):
            t = k / segments_per_edge
            x = x0 + (x1 - x0) * t
            y = y0 + (y1 - y0) * t
            if k > 0:
                x += rng.uniform(-amp, amp)
                y += rng.uniform(-amp, amp)
            out.append((x, y))
    return out


# ---------------------------------------------------------------- hatching
# Line density follows a per-shape tone array (0 = lit, 1 = shadow). Each line has its own
# seeded lateral offset, curvature, thickness and pen-lift breaks - see generate_sprites.py
# for why a shared noise field was rejected (it read as corrugated sheet metal).

PERIOD = 5


def _line_hash(idx, salt):
    h = (idx.astype(np.int64) * np.int64(2654435761) + np.int64(salt) * np.int64(40503)) & 0xFFFFFFFF
    h = (h ^ (h >> np.int64(13))) & 0xFFFFFFFF
    return (h % 10007) / 10007.0


def _hatch_direction(diag_coord, along_coord, tone, density_scale, tone_offset, salt_base, period=PERIOD):
    raw_idx = np.floor(diag_coord / period).astype(np.int64)
    lateral = (_line_hash(raw_idx, salt_base + 0) - 0.5) * 2.2
    freq = 0.006 + _line_hash(raw_idx, salt_base + 1) * 0.012
    phase = _line_hash(raw_idx, salt_base + 2) * 2 * np.pi
    curve_amp = 1.5 + _line_hash(raw_idx, salt_base + 3) * 2.5
    thickness_jitter = 0.55 + _line_hash(raw_idx, salt_base + 4) * 0.9

    curve = np.sin(along_coord * freq + phase) * curve_amp
    wobbled = (diag_coord - lateral - curve) % period

    seg_len = 14.0
    seg_idx = np.floor(along_coord / seg_len).astype(np.int64)
    combined = raw_idx * np.int64(100003) + seg_idx
    drawn = _line_hash(combined, salt_base + 5) > 0.14

    width = np.clip(tone - tone_offset, 0.0, 1.0) * period * density_scale * thickness_jitter
    return drawn & (wobbled < width)


def tone_diag(mask, strength=0.86):
    """Upper-left lit, lower-right shadowed, across the shape's own bounding box."""
    box = _bbox(mask)
    if box is None:
        return tone_flat(0.0)
    x0, x1, y0, y1 = box
    diag = max((x1 - x0) + (y1 - y0), 1)
    return np.clip(((XX - x0) + (YY - y0)) / diag, 0.0, 1.0) * strength


def tone_vertical(mask, top, bottom):
    box = _bbox(mask)
    if box is None:
        return tone_flat(0.0)
    _, _, y0, y1 = box
    t = np.clip((YY - y0) / max(y1 - y0, 1), 0.0, 1.0)
    return top + (bottom - top) * t


def tone_flat(value):
    return np.full((H, W), value, dtype=np.float32)


class Plate:
    def __init__(self, seed):
        self.rng = random.Random(seed)
        self.seed = seed
        self.rgb = np.zeros((H, W, 3), dtype=np.uint8)
        self._salt = 0

    def _next_salt(self):
        self._salt += 17
        return (self.seed % 97) * 11 + self._salt

    def fill(self, mask, base, tone, density=(1.3, 2.0), offsets=(0.12, 0.55), period=PERIOD, ink=INK):
        salt = self._next_salt()
        box = _bbox(mask)
        if box is None:
            return
        x0, x1, y0, y1 = box
        sl = (slice(y0, y1), slice(x0, x1))
        xx, yy, tn, m = XX[sl], YY[sl], tone[sl], mask[sl]
        line_a = _hatch_direction(xx - yy, xx + yy, tn, density[0], offsets[0], salt + 1, period)
        line_b = _hatch_direction(xx + yy, xx - yy, tn, density[1], offsets[1], salt + 11, period)
        region = self.rgb[sl]
        region[m] = base
        region[m & (line_a | line_b)] = ink

    def flat(self, mask, color):
        self.rgb[mask] = color

    def outline(self, mask, width=2, drop=0.18, color=INK):
        """Uneven ink contour: thins and breaks in patches like a hand-held nib."""
        box = _bbox(mask, pad=width + 2)
        if box is None:
            return
        x0, x1, y0, y1 = box
        sl = (slice(y0, y1), slice(x0, x1))
        ring = (dilate(mask, 1) & ~erode(mask, max(width - 1, 1)))[sl]
        noise = np.random.default_rng(self._next_salt()).random(ring.shape)
        blotch = np.array(
            Image.fromarray((noise * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3))
        ) / 255.0
        keep = blotch > 0.5 - 0.5 * (1 - drop) * 0.9
        self.rgb[sl][ring & keep] = color

    def ink_line(self, points, width=2, color=INK):
        img = Image.new("L", (W, H), 0)
        ImageDraw.Draw(img).line([tuple(p) for p in points], fill=255, width=width)
        self.rgb[np.array(img) > 127] = color

    def image(self):
        return Image.fromarray(self.rgb, "RGB")


# ---------------------------------------------------------------- scene elements

def paper(plate):
    rng = np.random.default_rng(plate.seed)
    # low-frequency blotches (aged paper) + fine grain, then a darkened vignette to the edges
    blotch = rng.random((H // 40, W // 40))
    blotch = np.array(Image.fromarray((blotch * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)) / 255.0
    grain = rng.normal(0, 1, (H, W))
    cx, cy = (XX - W / 2) / (W / 2), (YY - H / 2) / (H / 2)
    vignette = np.clip((cx * cx + cy * cy) ** 0.5 - 0.55, 0.0, 1.0) / 0.6
    t = np.clip(0.25 * blotch + 0.65 * vignette, 0.0, 1.0)
    base = np.array(PAPER, dtype=np.float32)
    edge = np.array(PAPER_EDGE, dtype=np.float32)
    rgb = base[None, None, :] * (1 - t[..., None]) + edge[None, None, :] * t[..., None]
    rgb += grain[..., None] * 4.5
    plate.rgb[:] = np.clip(rgb, 0, 255).astype(np.uint8)


def moon(plate, cx, cy, r):
    disc = ellipse(cx, cy, r, r)
    # only the single hatch direction, faint - the moon is the lightest thing on the plate
    tone = tone_diag(disc, 0.55)
    plate.fill(disc, MOON, tone, density=(0.9, 0.0), offsets=(0.3, 9.0), period=6, ink=mix(MOON, INK, 0.75))
    rng = plate.rng
    for _ in range(6):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(0.15, 0.8) * r
        crater = ellipse(cx + math.cos(a) * d, cy + math.sin(a) * d, rng.uniform(8, 26), rng.uniform(6, 18))
        plate.outline(crater & disc, width=1, drop=0.5, color=mix(MOON, INK, 0.55))
    plate.outline(disc, width=2, drop=0.1)
    return disc


def cloud(plate, cx, cy, scale, puffs=6):
    rng = plate.rng
    mask = blank_mask()
    xs = np.linspace(-1.0, 1.0, puffs)
    for i, ox in enumerate(xs):
        rx = scale * rng.uniform(0.45, 0.7) * (1.0 - 0.35 * abs(ox))
        ry = rx * rng.uniform(0.7, 0.95)
        mask |= ellipse(cx + ox * scale * 1.1, cy - (1.0 - abs(ox)) * scale * 0.25 + rng.uniform(-6, 6), rx, ry)
    # flat underside, like the concept sheet's stacked engraved clouds
    mask &= ~poly([(cx - 3 * scale, cy + scale * 0.35), (cx + 3 * scale, cy + scale * 0.35), (cx + 3 * scale, H), (cx - 3 * scale, H)])
    tone = tone_vertical(mask, 0.05, 0.9)
    plate.fill(mask, lighten(GREY_BLUE, 0.45), tone, period=5)
    # a few engraved curls inside the puffs
    for _ in range(puffs):
        px = cx + rng.uniform(-scale, scale)
        py = cy + rng.uniform(-scale * 0.3, scale * 0.1)
        rr = rng.uniform(scale * 0.12, scale * 0.25)
        pts = [(px + math.cos(a) * rr * (1 - a / 9), py + math.sin(a) * rr * (1 - a / 9)) for a in np.linspace(0, 4.5, 30)]
        curl = blank_mask()
        img = Image.new("L", (W, H), 0)
        ImageDraw.Draw(img).line(pts, fill=255, width=2)
        curl |= np.array(img) > 127
        plate.rgb[curl & mask] = INK
    plate.outline(mask, width=2, drop=0.15)
    return mask


def mountain_range(plate, peaks, base_y, color, snow, amp):
    """Each peak is its own triangle so its right-hand slope can take the heavier hatch."""
    rng = plate.rng
    total = blank_mask()
    for px, py, half in peaks:
        rise = base_y - py
        ls = (px - half * rng.uniform(0.4, 0.6), py + rise * rng.uniform(0.35, 0.55))
        rs = (px + half * rng.uniform(0.35, 0.55), py + rise * rng.uniform(0.3, 0.5))
        left = jagged([(px - half, base_y), ls, (px, py), (px, base_y)], rng, amp=amp, segments_per_edge=8)
        right = jagged([(px, py), rs, (px + half, base_y), (px, base_y)], rng, amp=amp, segments_per_edge=8)
        lm, rm = poly(left) & ~total, poly(right) & ~total
        plate.fill(lm, color, tone_vertical(lm, 0.15, 0.55))
        plate.fill(rm, darken(color, 0.12), tone_vertical(rm, 0.5, 0.95))
        shape = lm | rm
        if snow:
            cap_h = (base_y - py) * rng.uniform(0.22, 0.32)
            cap = poly(jagged([(px - half * cap_h / (base_y - py) * 1.3, py + cap_h), (px, py - 2), (px + half * cap_h / (base_y - py) * 1.3, py + cap_h)], rng, amp=amp * 0.8, segments_per_edge=6))
            plate.fill(cap & shape, SNOW, tone_diag(cap, 0.35), density=(0.8, 0.0), offsets=(0.2, 9.0), ink=mix(SNOW, INK, 0.7))
        ridge_pts = [(px - half, base_y), ls, (px, py), rs, (px + half, base_y)]
        plate.ink_line(ridge_pts, width=2)
        plate.outline(shape, width=2, drop=0.3)
        total |= shape
    return total


def conifer(plate, x, base, height, color, tiers=4):
    rng = plate.rng
    mask = blank_mask()
    half = height * 0.28
    trunk = poly([(x - height * 0.03, base), (x + height * 0.03, base), (x + height * 0.02, base - height * 0.2), (x - height * 0.02, base - height * 0.2)])
    for i in range(tiers):
        t = i / tiers
        top = base - height * (1.0 - t * 0.72)
        bottom = base - height * (0.72 - t * 0.72) * 0.95 - height * 0.05
        hw = half * (0.35 + 0.65 * t)
        tier = jagged([(x, top), (x + hw, bottom), (x - hw, bottom)], rng, amp=height * 0.025, segments_per_edge=5)
        mask |= poly(tier)
    plate.fill(trunk, darken(BROWN, 0.3), tone_flat(0.6))
    plate.fill(mask, color, tone_diag(mask, 0.95), period=4)
    plate.outline(mask, width=2, drop=0.25)
    return mask | trunk


def forest_band(plate, y_fn, x0, x1, color, h_range, step):
    rng = plate.rng
    total = blank_mask()
    x = x0
    while x < x1:
        h = rng.uniform(*h_range)
        total |= conifer(plate, x + rng.uniform(-step * 0.3, step * 0.3), y_fn(x), h, color, tiers=rng.choice((3, 4, 5)))
        x += step
    return total


def river(plate, ctrl, width_fn):
    """Ribbon along a polyline of control points, Catmull-Rom smoothed."""
    def spline(pts, n=200):
        out = []
        for i in range(len(pts) - 1):
            p0 = pts[max(i - 1, 0)]
            p1, p2 = pts[i], pts[i + 1]
            p3 = pts[min(i + 2, len(pts) - 1)]
            for k in range(n // (len(pts) - 1)):
                t = k / (n // (len(pts) - 1))
                t2, t3 = t * t, t * t * t
                x = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3)
                y = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)
                out.append((x, y))
        return out

    centre = spline(ctrl)
    left, right = [], []
    for i, (x, y) in enumerate(centre):
        nx, ny = centre[min(i + 1, len(centre) - 1)]
        px, py = centre[max(i - 1, 0)]
        dx, dy = nx - px, ny - py
        ln = math.hypot(dx, dy) or 1.0
        w = width_fn(i / len(centre))
        left.append((x - dy / ln * w, y + dx / ln * w))
        right.append((x + dy / ln * w, y - dx / ln * w))
    mask = poly(left + right[::-1])
    plate.fill(mask, lighten(SLATE, 0.35), tone_vertical(mask, 0.25, 0.75), period=5)
    # current lines running along the flow
    rng = plate.rng
    for _ in range(26):
        i = rng.randrange(5, len(centre) - 5)
        off = rng.uniform(-0.7, 0.7)
        seg = []
        for j in range(i, min(i + rng.randrange(8, 20), len(centre))):
            lx, ly = left[j]
            rx, ry = right[j]
            seg.append((lx + (rx - lx) * (0.5 + off * 0.5), ly + (ry - ly) * (0.5 + off * 0.5)))
        plate.ink_line(seg, width=1, color=mix(SLATE, INK, 0.6))
    plate.outline(mask, width=2, drop=0.2)
    return mask


def rock(plate, cx, cy, rx, ry):
    m = poly(jagged([(cx - rx, cy + ry * 0.6), (cx - rx * 0.6, cy - ry), (cx + rx * 0.5, cy - ry * 0.9), (cx + rx, cy + ry * 0.5), (cx, cy + ry)], plate.rng, amp=min(rx, ry) * 0.18, segments_per_edge=4))
    plate.fill(m, lighten(GREY_BLUE, 0.15), tone_diag(m, 0.95), period=4)
    plate.outline(m, width=2, drop=0.2)
    return m


def hut(plate, x, base, size):
    rng = plate.rng
    wall = poly(jagged([(x - size * 0.45, base), (x + size * 0.45, base), (x + size * 0.42, base - size * 0.42), (x - size * 0.42, base - size * 0.42)], rng, amp=size * 0.02))
    plate.fill(wall, darken(TAN, 0.25), tone_diag(wall, 0.9), period=4)
    roof = poly(jagged([(x - size * 0.65, base - size * 0.36), (x, base - size * 1.05), (x + size * 0.65, base - size * 0.36)], rng, amp=size * 0.03, segments_per_edge=8))
    plate.fill(roof, BROWN, tone_diag(roof, 0.9), period=4)
    # thatch strokes following the roof slope
    for _ in range(int(size * 0.5)):
        sx = x + rng.uniform(-size * 0.6, size * 0.6)
        sy = base - size * 0.38 - rng.uniform(0, size * 0.55) * (1 - abs(sx - x) / (size * 0.65))
        plate.ink_line([(sx, sy), (sx + rng.uniform(-3, 3), sy + rng.uniform(6, 14))], width=1)
    door = poly([(x - size * 0.1, base), (x + size * 0.1, base), (x + size * 0.1, base - size * 0.3), (x, base - size * 0.36), (x - size * 0.1, base - size * 0.3)])
    plate.flat(door, INK)
    plate.outline(wall, width=2, drop=0.2)
    plate.outline(roof, width=2, drop=0.2)
    # smoke: a wavering ink line rising from the peak
    pts = [(x + math.sin(t * 0.9) * (6 + t * 4) + t * 6, base - size * 1.05 - t * 11) for t in range(24)]
    plate.ink_line(pts, width=2, color=mix(INK, PAPER, 0.35))
    return wall | roof


def bush(plate, x, base, r):
    rng = plate.rng
    m = blank_mask()
    for _ in range(4):
        m |= ellipse(x + rng.uniform(-r * 0.6, r * 0.6), base - r * rng.uniform(0.4, 0.8), r * rng.uniform(0.5, 0.8), r * rng.uniform(0.4, 0.7))
    plate.fill(m, mix(MOSS, OLIVE, 0.4), tone_diag(m, 0.95), period=4)
    plate.outline(m, width=2, drop=0.3)
    return m


def figure(plate, x, base, h, facing=1):
    rng = plate.rng
    head = ellipse(x, base - h * 0.85, h * 0.11, h * 0.13)
    body = poly(jagged([(x - h * 0.16, base), (x + h * 0.16, base), (x + h * 0.14, base - h * 0.45), (x + h * 0.09, base - h * 0.72), (x - h * 0.09, base - h * 0.72), (x - h * 0.14, base - h * 0.45)], rng, amp=h * 0.015))
    plate.fill(body, DARK_BROWN, tone_diag(body, 0.9), period=3)
    plate.flat(head, mix(DARK_BROWN, TAN, 0.35))
    # spear / staff
    plate.ink_line([(x + facing * h * 0.22, base), (x + facing * h * 0.26, base - h * 1.05)], width=2)
    plate.outline(body | head, width=1, drop=0.3)
    return body | head


def campfire(plate, x, base):
    rng = plate.rng
    for i in range(7):
        a = i / 7 * 2 * math.pi
        rock(plate, x + math.cos(a) * 26, base + math.sin(a) * 9, 9, 6)
    flame = poly(jagged([(x - 12, base - 2), (x + 12, base - 2), (x + 6, base - 22), (x, base - 40), (x - 6, base - 20)], rng, amp=2))
    plate.flat(flame, RUST)
    plate.flat(ellipse(x, base - 12, 4, 9), lighten(RUST, 0.45))
    plate.outline(flame, width=1, drop=0.3)


def bare_tree(plate, x, base, height):
    """Winter-bare foreground tree: recursive ink branches, framing the right edge."""
    rng = plate.rng

    def branch(x0, y0, angle, length, depth, width):
        x1 = x0 + math.cos(angle) * length
        y1 = y0 - math.sin(angle) * length
        plate.ink_line([(x0, y0), (x1, y1)], width=max(int(width), 1))
        if depth == 0:
            return
        for _ in range(rng.choice((2, 2, 3))):
            branch(x1, y1, angle + rng.uniform(-0.75, 0.75), length * rng.uniform(0.55, 0.75), depth - 1, width * 0.6)

    trunk = poly(jagged([(x - height * 0.045, base), (x + height * 0.045, base), (x + height * 0.02, base - height * 0.35), (x - height * 0.02, base - height * 0.35)], rng, amp=3))
    plate.fill(trunk, DARK_BROWN, tone_diag(trunk, 0.9), period=3)
    branch(x, base - height * 0.33, math.pi / 2 + 0.15, height * 0.3, 5, 7)
    branch(x, base - height * 0.3, math.pi / 2 - 0.9, height * 0.25, 4, 5)
    branch(x, base - height * 0.25, math.pi / 2 + 1.0, height * 0.22, 4, 5)


def birds(plate, pts):
    for x, y in pts:
        plate.ink_line([(x - 16, y - 6), (x - 5, y + 2), (x, y - 1), (x + 5, y + 2), (x + 16, y - 6)], width=3)


def frame(plate, inset, gap):
    for d in (inset, inset + gap):
        pts = [(d, d), (W - d, d), (W - d, H - d), (d, H - d), (d, d)]
        plate.ink_line(pts, width=3 if d == inset else 1, color=mix(INK, PAPER, 0.15))


def text_block(plate):
    img = plate.image()
    draw = ImageDraw.Draw(img)
    title_font = ImageFont.truetype("C:/Windows/Fonts/georgiab.ttf", 118)
    over_font = ImageFont.truetype("C:/Windows/Fonts/georgiab.ttf", 52)
    sub_font = ImageFont.truetype("C:/Windows/Fonts/constani.ttf", 34)

    def spaced(x, y, text, font, fill, spacing, stroke=0):
        for ch in text:
            draw.text((x, y), ch, font=font, fill=fill, stroke_width=stroke, stroke_fill=INK)
            x += draw.textlength(ch, font=font) + spacing
        return x

    x0, y0 = 112, 96
    spaced(x0 + 8, y0, "OF FOLK AND", over_font, DARK_BROWN, 9, stroke=1)
    ty = y0 + 62
    spaced(x0 + 4, ty, "MANY WINTERS", title_font, DARK_BROWN, 6, stroke=2)
    draw.text((x0 + 8, ty + 148), "a world of our ancestors, remembered across many generations", font=sub_font, fill=RUST)
    draw.text((x0 + 8, ty + 194), "real and unreal · solid and poetic · a chronicled land revealed one winter at a time", font=sub_font, fill=mix(DARK_BROWN, PAPER, 0.25))
    plate.rgb[:] = np.array(img)


# ---------------------------------------------------------------- composition

def render(seed=7):
    plate = Plate(seed)
    rng = plate.rng
    paper(plate)

    moon(plate, 1500, 250, 175)
    birds(plate, [(1200, 330), (1250, 372), (1300, 322), (1160, 380)])

    cloud(plate, 1760, 130, 80, puffs=5)
    cloud(plate, 1400, 300, 105, puffs=7)
    cloud(plate, 1690, 400, 120, puffs=7)
    cloud(plate, 1230, 450, 90, puffs=6)

    # far range: pale, snow-capped, tallest in the middle-right like the sheet's
    far = [(520, 430, 260), (760, 380, 300), (1000, 355, 320), (1240, 400, 300), (1500, 445, 280), (1750, 470, 260), (300, 470, 240)]
    mountain_range(plate, far, 640, lighten(GREY_BLUE, 0.3), snow=True, amp=5)
    near = [(160, 520, 260), (450, 500, 300), (800, 520, 280), (1150, 545, 300), (1500, 560, 300), (1850, 560, 260)]
    mountain_range(plate, near, 700, GREY_BLUE, snow=False, amp=6)

    # forested hills in front of the mountains, two depths
    forest_band(plate, lambda x: 700 + math.sin(x / 210) * 14, -20, W + 40, mix(MOSS, GREY_BLUE, 0.35), (70, 120), 34)
    forest_band(plate, lambda x: 760 + math.sin(x / 160 + 1.5) * 16, -20, W + 40, MOSS, (90, 150), 42)

    # meadow ground plane
    ground = poly([(0, 740), (W, 740), (W, H), (0, H)])
    ground_tone = tone_vertical(ground, 0.1, 0.55)
    plate.fill(ground, TAN, ground_tone, period=7)
    for _ in range(18):
        gx, gy = rng.uniform(0, W), rng.uniform(790, H)
        patch = ellipse(gx, gy, rng.uniform(60, 180), rng.uniform(14, 34))
        plate.fill(patch & ground, mix(TAN, OLIVE, 0.55), ground_tone, period=6)

    water = river(plate, [(-40, 1030), (300, 1000), (700, 975), (1050, 935), (1350, 880), (1620, 820), (1960, 772)], lambda t: 12 + (1 - t) * 60)
    banks = dilate(water, 14) & ~water
    placed = 0
    while placed < 26:
        rx, ry = rng.uniform(60, 1700), rng.uniform(760, H - 20)
        if banks[int(ry), int(rx)] or (rng.random() < 0.25 and not water[int(ry), int(rx)]):
            rock(plate, rx, ry, rng.uniform(10, 26), rng.uniform(7, 16))
            placed += 1

    for _ in range(14):
        bx, by = rng.uniform(1150, 1800), rng.uniform(790, 900)
        if water[int(by), int(bx)]:
            continue
        bush(plate, bx, by, rng.uniform(16, 34))
    hut(plate, 1420, 800, 90)
    hut(plate, 560, 860, 150)
    hut(plate, 1040, 830, 105)
    campfire(plate, 790, 890)
    figure(plate, 705, 910, 82, facing=1)
    figure(plate, 880, 905, 78, facing=-1)
    figure(plate, 1150, 870, 64, facing=1)

    # framing trees on either edge
    conifer(plate, 150, 1060, 460, darken(MOSS, 0.2), tiers=6)
    conifer(plate, 60, 1075, 330, darken(MOSS, 0.35), tiers=5)
    bare_tree(plate, 1740, 1075, 640)

    frame(plate, 30, 8)
    text_block(plate)
    return plate.image()


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(1)
    render().save(sys.argv[1], optimize=True)
    print(f"wrote {sys.argv[1]}")


if __name__ == "__main__":
    main()
