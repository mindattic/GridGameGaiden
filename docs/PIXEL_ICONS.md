# Pixel art: enemy timeline sprites (with enhanced eyes) and every game icon

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


## Every other icon: statuses, abilities, items, cursor (`tools/make_icons.py`)

Owner, 2026-10-10:

> "instead of letter placeholders go out and find the images, and then use the same sprite art techniques to turn them into classically pixelated icons"
>
> "a continuous effort of building up graphics that match the aesthetic"

- **Sources.** Silhouettes come from **game-icons.net** (CC BY 3.0). The tool downloads each SVG once into `tools/icon_src/` and records every one, with its author, in `tools/icon_src/SOURCES.md`; the Credits screen carries the attribution.
- **Manifest.** `tools/icons.json` maps each game id to `[author/icon, palette, options…]`:
  - `status`: buff ids and `stunned`.
  - `ability`: every ability id.
  - `item`: overrides for consumables and materials.
  - `ui`: the glove `cursor`, rotated −45° so it points right.

  Equipment needs no manifest entry. A rule picks the silhouette from the weapon type or the name (ring, amulet, helm, boots, robe…), and the palette from the material (rusty, iron, steel, bronze, gold…) or the enchant suffix (Flame, Frost, Spark, Shadow).
- **Pipeline.**
  1. Rasterise the white SVG at 64 px.
  2. Paint it with a three-stop vertical gradient from the palette.
  3. Shrink to 20 px with a hard alpha edge.
  4. Reduce to 8 colours, no dithering.
  5. Add a 1 px dark outline, centre it on 22×22, then scale ×4 nearest-neighbour.
  6. Write a point-filter `.meta`.
- **Output.** `Resources/StatusIcons`, `AbilityIcons`, `ItemIcons` and `UI`. `Art.StatusIcon`, `Art.AbilityIcon`, `Art.ItemIcon` and `Art.Cursor` load them; `Ui.StatusPip` draws a status icon on a dark disc ringed in the status colour.
- **Gates.**
  - `python tools/make_icons.py --check` fails if any ability, item or status has no mapping.
  - `tools/check_assets.py` fails if any icon PNG is missing.
- **Adding content** means adding a manifest line (or letting the item rule cover it) and re-running the tool.
