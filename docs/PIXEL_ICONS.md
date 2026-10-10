# Enemy timeline icons: pixel sprites with enhanced eyes

Owner requests, 2026-10-10:

> "use imagemagick to reduce and enhance the enemy icons so that they stand out and look like pixel art versions of the full enemy"
>
> "the icons need to super impose the eyes … put a couple of black dots where the enemy eyes should be … you need to 'enhance' them"
>
> "save and record the method"

This document is the method. Everything is reproducible from the repo.

## Output

`Assets/Gaiden/Resources/Pixel/<classId>.png`, one sprite per enemy class (110).

- **Size:** 144×144, which is a 36×36 sprite scaled ×4 with nearest-neighbour.
- **Import:** each file has a `.meta` with `filterMode: 0` (point) and no compression, so the pixels stay crisp.
- **Use:** `Art.Pixel(id)` loads them for the timeline turn icons.
- **Gate:** `tools/check_assets.py` fails the build if an enemy has no sprite.

## Regenerate

```
python tools/make_pixel_icons.py            # all enemies
python tools/make_pixel_icons.py --only bat00,slime01
```

This needs ImageMagick 7 (`magick` on PATH) and Pillow.

## Pipeline (`tools/make_pixel_icons.py`)

1. **Knock out a solid backdrop.** About 15 portraits are painted on white instead of transparency. The tool detects this by checking whether the four corners are opaque. If they are, it flood-fills each corner to transparent with 14 % fuzz.
2. **Trim** the image to the figure's silhouette (`-trim`), so the sprite shows the whole enemy rather than a face crop.
3. **Median 5×5.** This removes painterly brush noise, which would otherwise turn into speckle when shrunk.
4. **Shrink to fit 32 px** (Triangle filter) and centre it on a 36×36 canvas.
5. **Hard alpha edge** at a 45 % threshold, so there are no semi-transparent fringes.
6. **Enhance colour and contrast:** `-modulate 112,155` (brighter, much more saturated), then `-sigmoidal-contrast 5,45%`.
7. **16-colour palette, no dithering** (`-dither None -colors 16`). This step produces the "pixel art" look.
8. **Sticker outline:** a 1 px dark outline (`#140c1c`), then a 1 px cream rim (`#fff4dc`). Both are made by dilating the alpha mask with `Diamond:1` and compositing underneath. The result reads on both dark and light timeline backgrounds.
9. **Scale ×4 nearest-neighbour** and write PNG32.
10. **Re-ink the eyes.** See below.

## Enhanced eyes

Eyes are the detail that shrinking a painting to 32 px destroys, and they are what makes a monster read as a creature at a glance. So they are hand-placed and re-inked.

- **`tools/eyes.json`** maps each `classId` to a list of `[x, y]` eye pixels in sprite coordinates (0–35).
  - Most enemies have two eyes, cyclopes one, and profile faces one.
  - Faceless helms and hoods get eyes painted where the visor or face would be.
- **`ink_eyes()`** paints each eye as one solid sprite pixel (a 4×4 block at ×4).
  - **Snapping:** hand-placed points can be a pixel off, so each eye snaps to the nearest *interior* pixel: opaque, not the outline and not the rim. It searches within a radius of 2, and two eyes never share a pixel.
  - **Dark faces:** if the eye's neighbours average a luma below 70, a black dot would vanish. Those eyes are inked as a pale glint (`#fff4c8`) instead, which reads as glowing eyes on hooded hags and shamblers. Everything else gets near-black `#07040c`.

### How the eye positions were found (repeat this for new enemies)

1. **Full-figure sheets.** `python tools/eyesheet.py` writes `artifacts/eyes/sheetNN.png`.
   - Each sheet shows nine enemies at exactly 8× the sprite's geometry: same knockout, trim and fit, just a larger resize.
   - Ticks every 4 sprite pixels are labelled in sprite coordinates.
   - Use these to locate each head, and record its centre and crop size in `tools/eye_heads.json` as `{"id": [cx, cy, size]}`.
2. **Head zooms.** `python tools/eyezoom.py` writes `artifacts/eyes/zoomNN.png`.
   - Each panel is a crop around the head with a per-sprite-pixel grid.
   - Labels are in sprite coordinates, and each panel's y labels sit at its own left edge.
   - Read each eye's pixel off the grid and record it in `tools/eyes.json`.
3. **Regenerate and check.** Run `python tools/make_pixel_icons.py` again, then check the result at 1:1 or an integer scale.
   - Don't point-resize to a non-integer scale. It drops whole rows of pixels, and the eyes with them.

## In the game

On the timeline, each enemy turn icon shows its sprite inside a circle.

- **Circle:** an opaque dark disc, masked with `overflow: hidden` so sprites never poke out the side. The ring is red for foes and purple for bosses.
- **Next attacker:** the enemy closest to ACT gets a gold ring and a 1.18× lift.
- **Status:** a frozen, asleep or stunned enemy shows a status badge on the circle and its sprite is dimmed.

See bible §2.6.
