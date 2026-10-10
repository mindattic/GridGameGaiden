"""
Biome board tiles from GridGame2026's original tile (owner, 2026-10-10: "each area should use a different tile
style; reuse the original when the biome matches its colours").

    python tools/make_tiles.py

Reads Assets/Gaiden/Resources/Tiles/tile.png (the original slate tile, imported by tools/import_art.py) and writes
Tiles/<themeId>.png per campaign theme. Each recolour keeps every pixel's brightness — so the light bevel rim and
the dark fill survive — and moves its hue toward the biome colour. Themes whose biome already matches the slate
(the caverns) use the original unchanged. BoardView tints alternate squares light/dark for the checkerboard.
"""
import os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TILES = os.path.join(ROOT, "Assets", "Gaiden", "Resources", "Tiles")

# themeId -> (biome colour, strength 0..1); None = use the original slate tile as-is.
BIOMES = {
    "greenvalley": ((104, 168, 72), 0.62),   # moss
    "desert": ((214, 168, 96), 0.66),        # sandstone
    "swamp": ((62, 150, 132), 0.6),          # murky teal
    "cave": None,                            # the original slate already reads as frozen rock
    "cityruins": ((176, 104, 124), 0.55),    # rust-rose masonry
}


def recolour(src, colour, strength):
    im = src.convert("RGBA")
    cr, cg, cb = colour
    cl = 0.299 * cr + 0.587 * cg + 0.114 * cb
    out = Image.new("RGBA", im.size)
    px, op = im.load(), out.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            lum = 0.299 * r + 0.587 * g + 0.114 * b
            k = lum / cl * 1.4  # biome hue at a touch more than the base brightness
            tr, tg, tb = cr * k, cg * k, cb * k
            op[x, y] = tuple(int(max(0, min(255, c0 + (c1 - c0) * strength))) for c0, c1 in ((r, tr), (g, tg), (b, tb))) + (a,)
    return out


def main():
    base = Image.open(os.path.join(TILES, "tile.png"))
    for theme, spec in BIOMES.items():
        dst = os.path.join(TILES, theme + ".png")
        (base.copy() if spec is None else recolour(base, *spec)).save(dst)
        print("tile:", theme, "original" if spec is None else spec)


if __name__ == "__main__":
    main()
