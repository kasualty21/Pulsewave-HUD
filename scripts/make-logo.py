"""Draws the Lumen HUD logo from the Lumen tokens (src/Pulsewave/Themes/Lumen.cs).

A glass tile on the near-black canvas with a faint aurora; a Lumen gauge (a 270-degree arc on a faint track, cyan ->
violet -> magenta) whose end is a point of light, around a DIN-style "L". Writes LumenHUD-logo.png (1024 px) and
LumenHUD-logo.ico (16-256 px; the small sizes drop the ticks and thicken the strokes so they stay crisp).

    python scripts/make-logo.py src/Pulsewave/Assets
"""
import math, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops

CYAN, VIOLET, MAGENTA = (0x4D, 0xE8, 0xFF), (0xA7, 0x8B, 0xFF), (0xFF, 0x5C, 0xCB)
CANVAS_TOP, CANVAS_BOTTOM = (0x10, 0x14, 0x24), (0x04, 0x05, 0x0A)


def lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def along(t):
    """The gauge gradient: cyan to violet to magenta."""
    return lerp(CYAN, VIOLET, t * 2) if t < 0.5 else lerp(VIOLET, MAGENTA, (t - 0.5) * 2)


def draw(side, small=False):
    s = 4  # supersample
    n = side * s
    img = Image.new('RGBA', (n, n), (0, 0, 0, 0))

    # Glass tile: a rounded square with a vertical canvas gradient, a hairline edge and a top highlight.
    radius = n * 0.225
    mask = Image.new('L', (n, n), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, n - 1, n - 1], radius=radius, fill=255)
    tile = Image.new('RGBA', (n, n))
    td = ImageDraw.Draw(tile)
    for y in range(n):
        td.line([(0, y), (n, y)], fill=lerp(CANVAS_TOP, CANVAS_BOTTOM, y / n) + (255,))
    # Aurora: soft cyan top-left, magenta bottom-right.
    aurora = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    ad = ImageDraw.Draw(aurora)
    for (cx, cy, r, color, a) in [(0.18, 0.12, 0.55, (0x12, 0xA8, 0xE0), 90), (0.95, 1.0, 0.6, (0xE0, 0x40, 0x9C), 70), (0.6, 0.45, 0.45, (0x6A, 0x45, 0xF0), 40)]:
        ad.ellipse([(cx - r) * n, (cy - r) * n, (cx + r) * n, (cy + r) * n], fill=color + (a,))
    aurora = aurora.filter(ImageFilter.GaussianBlur(n * 0.12))
    tile = Image.alpha_composite(tile, aurora)
    edge = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    ImageDraw.Draw(edge).rounded_rectangle([s, s, n - 1 - s, n - 1 - s], radius=radius, outline=(255, 255, 255, 46), width=max(s, round(n * 0.006)))
    hl = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    ImageDraw.Draw(hl).rounded_rectangle([n * 0.06, s * 2, n * 0.94, n * 0.42], radius=radius * 0.8, fill=(255, 255, 255, 16))
    hl = hl.filter(ImageFilter.GaussianBlur(n * 0.06))
    tile = Image.alpha_composite(Image.alpha_composite(tile, hl), edge)
    img.paste(tile, (0, 0), mask)

    c = n / 2
    r = n * 0.33
    w = n * (0.085 if small else 0.062)
    start, sweep = 135, 270  # the Lumen gauge: open at the bottom
    level = 0.78  # how far the light has travelled

    layer = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    rb = r + w / 2  # PIL draws an arc's stroke inside its box: centre the stroke on r
    box = [c - rb, c - rb, c + rb, c + rb]
    ld.arc(box, start, start + sweep, fill=(255, 255, 255, 34), width=round(w))  # track
    if not small:  # ticks outside the ring, as on Lumen's gauges
        for i in range(28):
            a = math.radians(start + sweep * i / 27)
            r1, r2 = r + w * 0.95, r + w * (1.55 if i % 9 == 0 else 1.3)
            ld.line([(c + r1 * math.cos(a), c + r1 * math.sin(a)), (c + r2 * math.cos(a), c + r2 * math.sin(a))],
                    fill=(255, 255, 255, 70 if i % 9 == 0 else 40), width=max(s, round(n * 0.006)))
    arc = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    ad2 = ImageDraw.Draw(arc)
    steps = 180
    end = sweep * level
    for i in range(steps):
        a0 = start + end * i / steps
        a1 = start + end * (i + 1) / steps + 0.6
        ad2.arc(box, a0, a1, fill=along(i / steps) + (255,), width=round(w))
    # Round cap at the start.
    a = math.radians(start)
    px, py = c + r * math.cos(a), c + r * math.sin(a)
    ad2.ellipse([px - w / 2, py - w / 2, px + w / 2, py + w / 2], fill=CYAN + (255,))
    glow = arc.filter(ImageFilter.GaussianBlur(n * 0.03))
    layer = Image.alpha_composite(layer, glow)
    layer = Image.alpha_composite(layer, arc)

    # The point of light at the arc's end.
    a = math.radians(start + end)
    px, py = c + r * math.cos(a), c + r * math.sin(a)
    light = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    lg = ImageDraw.Draw(light)
    for rr, alpha in [(w * 2.6, 60), (w * 1.6, 120)]:
        lg.ellipse([px - rr, py - rr, px + rr, py + rr], fill=MAGENTA + (alpha,))
    light = light.filter(ImageFilter.GaussianBlur(w * 0.9))
    core = w * 0.78
    ImageDraw.Draw(light).ellipse([px - core, py - core, px + core, py + core], fill=(255, 255, 255, 255))
    layer = Image.alpha_composite(layer, light)

    # The L: a DIN-like letter, light with a soft cyan glow, set slightly left of centre like a reading in a gauge.
    letter = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    lt = ImageDraw.Draw(letter)
    stroke = n * (0.075 if small else 0.06)
    x0, y0, h, foot = c - n * 0.085, c - n * 0.14, n * 0.27, n * 0.19
    lt.rounded_rectangle([x0, y0, x0 + stroke, y0 + h], radius=stroke * 0.18, fill=(255, 255, 255, 255))
    lt.rounded_rectangle([x0, y0 + h - stroke, x0 + foot, y0 + h], radius=stroke * 0.18, fill=(255, 255, 255, 255))
    lglow = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    ImageDraw.Draw(lglow).bitmap((0, 0), letter.getchannel('A'), fill=CYAN + (150,))
    lglow = lglow.filter(ImageFilter.GaussianBlur(n * 0.025))
    layer = Image.alpha_composite(layer, lglow)
    layer = Image.alpha_composite(layer, letter)

    # Keep everything inside the tile.
    layer.putalpha(ImageChops.multiply(layer.getchannel('A'), mask))
    img = Image.alpha_composite(img, layer)
    return img.resize((side, side), Image.Resampling.LANCZOS)


def main(out):
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    big = draw(1024)
    big.save(out / 'LumenHUD-logo.png', optimize=True)
    sizes = [16, 24, 32, 48, 64, 128, 256]
    frames = [draw(sz, small=sz <= 48) for sz in sizes]
    frames[-1].save(out / 'LumenHUD-logo.ico', format='ICO', sizes=[(sz, sz) for sz in sizes], append_images=frames[:-1])
    # A review sheet of every size on dark and light backgrounds.
    sheet = Image.new('RGBA', (sum(sizes) + 20 * len(sizes) + 20, 2 * 280), (0, 0, 0, 0))
    for row, back in enumerate([(32, 32, 32, 255), (240, 240, 240, 255)]):
        ImageDraw.Draw(sheet).rectangle([0, row * 280, sheet.width, row * 280 + 279], fill=back)
        x = 20
        for sz, f in zip(sizes, frames):
            sheet.alpha_composite(f, (x, row * 280 + 140 - sz // 2))
            x += sz + 20
    sheet.save(out / 'LumenHUD-logo-sizes.png')
    print('wrote', out)


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else '.')
