#!/usr/bin/env python3
"""
Generates 64x64 placeholder pixel-art sprites for the ManyWinters Godot prototype.

Everything is drawn on an exact 64x64 grid with hard edges (no antialiasing), so the
output is real pixel art rather than a downscaled photo. Each sprite gets a three-tone
shading pass (light from the upper left) plus a dark silhouette outline.

Run:  python3 generate_sprites.py <output_dir>
"""

import sys
import os
import numpy as np
from PIL import Image, ImageDraw

S = 64  # canvas size


# ---------------------------------------------------------------- colour utils

def rgb(*c):
    return tuple(int(round(v * 255)) if isinstance(v, float) else int(v) for v in c)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def lighten(c, t):
    return mix(c, (255, 255, 255), t)


def darken(c, t):
    return mix(c, (0, 0, 0), t)


# ---------------------------------------------------------------- mask helpers

def _blank():
    return Image.new("L", (S, S), 0)


def _to_mask(img):
    return np.array(img) > 127


def ellipse(cx, cy, rx, ry):
    img = _blank()
    ImageDraw.Draw(img).ellipse(
        [cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
    return _to_mask(img)


def rect(x0, y0, x1, y1):
    img = _blank()
    ImageDraw.Draw(img).rectangle([x0, y0, x1, y1], fill=255)
    return _to_mask(img)


def poly(points):
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


def dilate(mask):
    out = mask.copy()
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        out |= shift(mask, dx, dy)
    return out


# ---------------------------------------------------------------- canvas

class Canvas:
    def __init__(self):
        self.rgb = np.zeros((S, S, 3), dtype=np.uint8)
        self.alpha = np.zeros((S, S), dtype=bool)

    def paint(self, mask, color):
        self.rgb[mask] = color
        self.alpha |= mask

    def shape(self, mask, base, shade=2, light=0.28, dark=0.26):
        """Paint a mask with an auto three-tone shading pass."""
        hi = mask & ~shift(mask, shade, shade)          # upper-left rim
        lo = mask & ~shift(mask, -shade, -shade)        # lower-right rim
        self.paint(mask, base)
        self.paint(lo & ~hi, darken(base, dark))
        self.paint(hi, lighten(base, light))

    def flat(self, mask, color):
        self.paint(mask, color)

    def outline(self, color):
        ring = dilate(self.alpha) & ~self.alpha
        self.rgb[ring] = color
        self.alpha |= ring

    def image(self):
        out = np.zeros((S, S, 4), dtype=np.uint8)
        out[..., :3] = self.rgb
        out[..., 3] = np.where(self.alpha, 255, 0)
        return Image.fromarray(out, "RGBA")


# ---------------------------------------------------------------- sprites
# Base colours are taken from the existing *.tres visual definitions so the
# sprites stay recognisable next to anything still rendering as flat colour.

SKIN = rgb(0.92, 0.74, 0.56)
CLOAK = rgb(0.42, 0.30, 0.44)
CLOAK_TRIM = rgb(0.72, 0.62, 0.45)
BOOT = rgb(0.30, 0.22, 0.18)
HAIR = rgb(0.35, 0.24, 0.16)


def person():
    c = Canvas()
    # legs
    c.shape(rect(26, 46, 30, 57) | rect(34, 46, 38, 57), BOOT, shade=1)
    # boots
    c.flat(rect(24, 55, 31, 58) | rect(33, 55, 40, 58), darken(BOOT, 0.25))
    # cloak / body: a trapezoid that flares towards the ground
    body = poly([(26, 20), (38, 20), (44, 52), (20, 52)])
    c.shape(body, CLOAK, shade=3)
    # ragged hem, drawn as notches rather than a straight band
    hem = rect(20, 50, 44, 52) & body
    for x in range(20, 45, 5):
        hem &= ~rect(x, 50, x + 1, 51)
    c.flat(hem, darken(CLOAK, 0.42))
    # belt
    c.flat(rect(24, 38, 40, 39) & body, CLOAK_TRIM)
    # arms
    c.shape(poly([(22, 24), (26, 24), (24, 44), (19, 43)]), darken(CLOAK, 0.12), shade=1)
    c.shape(poly([(38, 24), (42, 24), (45, 43), (40, 44)]), darken(CLOAK, 0.12), shade=1)
    # hands
    c.shape(ellipse(21, 45, 3, 3), SKIN, shade=1)
    c.shape(ellipse(43, 45, 3, 3), SKIN, shade=1)
    # head
    c.shape(ellipse(32, 15, 8, 9), SKIN, shade=2)
    # hood over the head
    hood = ellipse(32, 13, 10, 10) & ~ellipse(32, 17, 7, 8)
    c.shape(hood, CLOAK, shade=2)
    c.shape(rect(22, 18, 42, 22) & ellipse(32, 13, 10, 11), CLOAK, shade=1)
    # hair peeking out and eyes
    c.flat(rect(27, 8, 37, 10) & ellipse(32, 15, 8, 9), HAIR)
    c.flat(rect(28, 15, 29, 16), rgb(30, 28, 32))
    c.flat(rect(35, 15, 36, 16), rgb(30, 28, 32))
    c.outline(rgb(26, 20, 28))
    return c


def person_dead():
    """Literally the living sprite: rotated onto its side and drained of colour.

    Reusing the same silhouette is deliberate — at a glance it has to read as
    "that person, but down", not as a separate entity.
    """
    arr = np.array(person().image()).astype(np.float32)
    lum = arr[..., :3] @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    # desaturate towards a cold grey without crushing the values to black
    for i, tint in enumerate((0.94, 0.97, 1.08)):
        arr[..., i] = np.clip(lum * 0.85 * tint + 22, 0, 255)
    # exactly 90 degrees keeps the pixel grid intact (no resampling artefacts)
    rot = np.rot90(arr.astype(np.uint8), k=1)

    c = Canvas()
    mask = rot[..., 3] > 127
    c.rgb[mask] = rot[..., :3][mask]
    c.alpha |= mask
    # drop the body onto the ground line
    rows = np.flatnonzero(c.alpha.any(axis=1))
    drop = (S - 6) - rows.max()
    c.rgb = np.roll(c.rgb, drop, axis=0)
    c.alpha = np.roll(c.alpha, drop, axis=0)
    c.outline(rgb(24, 24, 28))
    return c


def wood():
    """Stacked logs — cut ends facing the camera."""
    c = Canvas()
    bark = rgb(0.40, 0.25, 0.10)
    core = rgb(0.72, 0.55, 0.34)
    ring = darken(core, 0.28)
    # back log
    c.shape(ellipse(32, 22, 14, 12), bark, shade=2)
    c.shape(ellipse(32, 22, 9, 8), core, shade=1)
    c.flat(ellipse(32, 22, 5, 4) & ~ellipse(32, 22, 3, 2), ring)
    # front-left log
    c.shape(ellipse(20, 42, 15, 13), bark, shade=2)
    c.shape(ellipse(20, 42, 10, 9), core, shade=1)
    c.flat(ellipse(20, 42, 6, 5) & ~ellipse(20, 42, 3, 3), ring)
    # front-right log
    c.shape(ellipse(45, 44, 14, 12), bark, shade=2)
    c.shape(ellipse(45, 44, 9, 8), core, shade=1)
    c.flat(ellipse(45, 44, 5, 4) & ~ellipse(45, 44, 3, 2), ring)
    c.outline(rgb(28, 18, 10))
    return c


def apple():
    c = Canvas()
    skin = rgb(0.82, 0.12, 0.12)
    body = ellipse(32, 38, 21, 20) | ellipse(22, 34, 12, 12) | ellipse(42, 34, 12, 12)
    body &= ~rect(0, 0, S, 21)
    c.shape(body, skin, shade=4, light=0.42, dark=0.30)
    # specular glint
    c.flat(ellipse(23, 29, 4, 3), lighten(skin, 0.72))
    # stalk and leaf
    c.shape(rect(31, 12, 34, 24), rgb(0.36, 0.24, 0.14), shade=1)
    c.shape(ellipse(42, 17, 10, 5) & ~ellipse(48, 12, 10, 6), rgb(0.30, 0.62, 0.24), shade=2)
    c.outline(rgb(40, 12, 14))
    return c


def pear():
    c = Canvas()
    skin = rgb(0.72, 0.86, 0.22)
    body = ellipse(32, 44, 18, 16) | ellipse(32, 28, 11, 12)
    c.shape(body, skin, shade=4, light=0.40, dark=0.28)
    c.flat(ellipse(25, 38, 4, 5), lighten(skin, 0.55))
    # freckles so it does not read as a plain blob
    for px, py in ((38, 44), (34, 52), (42, 36), (28, 50), (36, 30)):
        c.flat(rect(px, py, px, py), darken(skin, 0.35))
    c.shape(rect(31, 10, 34, 20), rgb(0.36, 0.24, 0.14), shade=1)
    c.shape(ellipse(41, 15, 9, 4) & ~ellipse(46, 11, 9, 5), rgb(0.34, 0.58, 0.20), shade=2)
    c.outline(rgb(46, 56, 14))
    return c


def potato():
    c = Canvas()
    skin = rgb(0.82, 0.66, 0.36)
    body = ellipse(30, 34, 22, 16) | ellipse(40, 40, 15, 12) | ellipse(20, 40, 12, 10)
    c.shape(body, skin, shade=4, light=0.34, dark=0.26)
    # eyes / dimples
    for px, py in ((22, 30), (36, 28), (44, 38), (28, 42), (16, 38), (38, 46)):
        c.flat(ellipse(px, py, 2, 1), darken(skin, 0.34))
        c.flat(rect(px - 1, py - 1, px, py - 1), darken(skin, 0.5))
    # a bit of soil clinging to the bottom
    c.flat(ellipse(26, 48, 6, 2), darken(skin, 0.52))
    c.flat(ellipse(45, 47, 4, 2), darken(skin, 0.52))
    c.outline(rgb(56, 40, 20))
    return c


def mushroom():
    c = Canvas()
    cap = rgb(0.56, 0.34, 0.20)
    stem = rgb(0.88, 0.84, 0.72)
    gills = rgb(0.70, 0.64, 0.52)
    # stem
    c.shape(poly([(27, 30), (37, 30), (39, 54), (25, 54)]), stem, shade=2)
    # gills under the cap
    c.flat(rect(20, 30, 44, 33) & ellipse(32, 30, 24, 8), gills)
    # cap
    capmask = ellipse(32, 30, 26, 20) & ~rect(0, 31, S, S)
    c.shape(capmask, cap, shade=4, light=0.34, dark=0.28)
    # spots
    for px, py, r in ((22, 24, 4), (38, 20, 5), (45, 27, 3), (30, 16, 3), (14, 29, 3)):
        c.flat(ellipse(px, py, r, r - 1) & capmask, lighten(cap, 0.62))
    # base flare
    c.shape(ellipse(32, 54, 10, 4), darken(stem, 0.12), shade=1)
    c.outline(rgb(38, 22, 14))
    return c


def storage_hut():
    c = Canvas()
    wall = rgb(0.52, 0.41, 0.26)
    roof = rgb(0.36, 0.29, 0.20)
    door = rgb(0.24, 0.17, 0.11)
    # walls
    walls = rect(12, 30, 52, 58)
    c.shape(walls, wall, shade=3)
    # plank seams
    for x in (20, 28, 36, 44):
        c.flat(rect(x, 31, x, 57), darken(wall, 0.28))
    # thatched roof
    roofmask = poly([(32, 6), (58, 32), (6, 32)])
    c.shape(roofmask, roof, shade=3)
    for i in range(3):
        y = 16 + i * 6
        half = int((y - 6) * (26 / 26))
        c.flat(rect(32 - half, y, 32 + half, y) & roofmask, darken(roof, 0.3))
    # ridge beam and eaves
    c.flat(rect(6, 30, 58, 32) & (roofmask | dilate(roofmask)), darken(roof, 0.45))
    # door
    c.shape(rect(26, 40, 38, 58), door, shade=1)
    c.flat(ellipse(35, 49, 1, 1), rgb(0.85, 0.78, 0.5))
    # small window
    c.shape(rect(15, 36, 22, 42), rgb(0.20, 0.24, 0.26), shade=1)
    c.flat(rect(18, 36, 18, 42) | rect(15, 39, 22, 39), darken(wall, 0.15))
    c.outline(rgb(26, 19, 12))
    return c


def grave_unmarked():
    """A bare dirt mound - no stone, no name, nothing left to read."""
    c = Canvas()
    dirt = rgb(0.36, 0.26, 0.16)
    dirt_dark = darken(dirt, 0.32)
    mound = (ellipse(32, 46, 24, 15) | ellipse(18, 50, 12, 9) | ellipse(47, 49, 12, 9)) & ~rect(0, 0, S, 32)
    c.shape(mound, dirt, shade=3, light=0.30, dark=0.30)
    for px, py in ((22, 42), (36, 38), (46, 44), (26, 52), (42, 52)):
        c.flat(ellipse(px, py, 3, 2) & mound, dirt_dark)
    stone = rgb(0.5, 0.5, 0.52)
    for px, py, r in ((16, 46, 3), (49, 44, 2), (30, 34, 2)):
        c.shape(ellipse(px, py, r, r - 1), stone, shade=1)
    c.outline(rgb(18, 12, 6))
    return c


def grave_marked():
    """A carved headstone planted in a small mound - the record survives."""
    c = Canvas()
    dirt = rgb(0.36, 0.26, 0.16)
    stone = rgb(0.62, 0.62, 0.66)
    rune = darken(stone, 0.45)
    mound = ellipse(32, 54, 20, 8) & ~rect(0, 0, S, 48)
    c.shape(mound, dirt, shade=2, light=0.22, dark=0.30)
    slab = rect(22, 24, 42, 50) | (ellipse(32, 24, 10, 10) & ~rect(0, 24, S, S))
    c.shape(slab, stone, shade=4, light=0.32, dark=0.26)
    c.flat(rect(30, 28, 34, 44) & slab, rune)
    c.flat(rect(25, 33, 39, 37) & slab, rune)
    c.outline(rgb(18, 18, 20))
    return c


def conifer_tree():
    c = Canvas()
    trunk = rgb(0.32, 0.22, 0.14)
    foliage = rgb(0.20, 0.34, 0.20)
    c.shape(rect(29, 46, 35, 58), trunk, shade=1)
    tier3 = poly([(32, 28), (56, 54), (8, 54)])
    tier2 = poly([(32, 16), (52, 40), (12, 40)])
    tier1 = poly([(32, 4), (48, 26), (16, 26)])
    c.shape(tier3, foliage, shade=3, light=0.22, dark=0.30)
    c.shape(tier2, lighten(foliage, 0.06), shade=3, light=0.24, dark=0.28)
    c.shape(tier1, lighten(foliage, 0.12), shade=2, light=0.26, dark=0.26)
    c.outline(rgb(14, 22, 12))
    return c


def rock_pile():
    c = Canvas()
    stone = rgb(0.5, 0.5, 0.52)
    c.shape(ellipse(22, 44, 14, 11), stone, shade=2)
    c.shape(ellipse(40, 46, 13, 10), darken(stone, 0.08), shade=2)
    c.shape(ellipse(32, 36, 11, 10), lighten(stone, 0.1), shade=1)
    c.flat(rect(19, 42, 25, 43), darken(stone, 0.35))
    c.flat(rect(36, 44, 42, 45), darken(stone, 0.35))
    c.outline(rgb(30, 30, 32))
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
    "rock_pile": rock_pile,
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

    # contact sheet at 6x zoom for eyeballing the result
    zoom, cols = 6, 4
    rows = (len(images) + cols - 1) // cols
    pad, label = 8, 14
    cell = S * zoom
    sheet = Image.new("RGBA", (cols * (cell + pad) + pad,
                               rows * (cell + pad + label) + pad),
                      (46, 48, 54, 255))
    draw = ImageDraw.Draw(sheet)
    for i, (name, img) in enumerate(images.items()):
        cx = pad + (i % cols) * (cell + pad)
        cy = pad + (i // cols) * (cell + pad + label)
        draw.rectangle([cx, cy, cx + cell - 1, cy + cell - 1], fill=(96, 104, 92, 255))
        sheet.alpha_composite(img.resize((cell, cell), Image.NEAREST), (cx, cy))
        draw.text((cx + 2, cy + cell + 2), name, fill=(230, 230, 230, 255))
    sheet.save(os.path.join(out_dir, "_contact_sheet.png"))
    print("wrote _contact_sheet.png")


if __name__ == "__main__":
    main()
