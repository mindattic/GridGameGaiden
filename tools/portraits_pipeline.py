"""
Cleans up and visually previews the eyes-in-frame crop for hand-generated portrait art dropped into
content-drafts/portraits_src/ — NOT the GridGame2026 reference tree tools/import_art.py expects.

    python tools/portraits_pipeline.py clean  [--only id1,id2] [--fuzz 3]
    python tools/portraits_pipeline.py crop   [--only id1,id2] [--kind Face|Figure]
    python tools/portraits_pipeline.py sheet  [--only id1,id2] [--kind Face|Figure]

There is deliberately no "import into the live game" step here: that requires a finalized roster
(content-drafts/new_heroes.json / new_enemies.json), which doesn't exist yet — the owner hasn't decided
the new heroes/enemies roster. This tool only prepares art (clean background, preview crop framing) ahead
of that decision; once a roster exists, a separate step wires Portrait/Crop into it and promotes finished
files into Resources/Portraits + Thumbs via import_art.save_portrait_assets (same as it always has).

  1. `clean` — background-knockout + trim, IN PLACE on whatever's sitting in portraits_src/ (re-roll
     variants, not-yet-renamed generic names, all of it). The true original is kept once per file under
     content-drafts/portraits_src_originals/ before anything is overwritten.
  2. `crop` / `sheet` — compute and visually review the eyes-in-frame PortraitCrop. `--kind` picks the
     framing heuristic (Face: alpha-weighted head centroid, for humanoid art; Figure: whole-body square,
     for creatures) — there's no roster to infer this from, so it's an explicit flag, default Figure.

The actual pixel work (background-cleanup un-mixing, face/figure crop-box heuristics) is NOT implemented
here — it lives in the sibling `AssetForge` repo (github.com/mindattic/AssetForge,
`D:/Projects/MindAttic/AssetForge` locally), a generic, non-Gaiden-specific tool shared across projects.
This script shells out to its CLI and owns only what's actually specific to this game: the
`new_*_overrides.json` conventions, and the eyes-in-frame crop math below, which mirrors
BattleScreen.CutIns.cs's ActionCard bust-window layout exactly (background-size: Contain inside an
oversized, offset box, clipped by a smaller window) — see cutin_geometry(), each constant commented with
its source line.

Key finding (confirmed against the actual C# layout, not assumed): PortraitCrop.Y has zero visual effect
for an ordinary tall/narrow full-body portrait — the bust window always shows image-height fraction
[2%, 42%] from the top regardless of Y, because the box's height is the binding "contain" axis for any
image aspect ratio below ~0.549. Only X (horizontal) moves anything for normal art. Y only matters once a
portrait's aspect ratio crosses that threshold (a creature drawn low/wide). So: X is always computed; Y is
only computed (and only written) for the rarer aspect ratios where it's actually live. For everything
else, "eyes in frame" is a trim-tightness question, not a crop-math one — `sheet` renders the exact
visible rectangle so a tight trim can be visually confirmed (or a miss caught and overridden).
"""
import argparse
import json
import os
import shutil
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "content-drafts", "portraits_src")
BACKUP_DIR = os.path.join(ROOT, "content-drafts", "portraits_src_originals")
SHEET_OUT = os.path.join(ROOT, "artifacts", "portrait_crops")
CROP_OVERRIDES_PATH = os.path.join(ROOT, "tools", "new_portrait_crop_overrides.json")
BG_OVERRIDES_PATH = os.path.join(ROOT, "tools", "new_portrait_bg_overrides.json")

ASSETFORGE_ROOT = os.path.join(os.path.dirname(ROOT), "AssetForge")
ASSETFORGE_CLI_DLL = os.path.join(
    ASSETFORGE_ROOT, "MindAttic.AssetForge.Cli", "bin", "Debug", "net10.0", "assetforge.dll"
)


def ensure(path):
    os.makedirs(path, exist_ok=True)
    return path


def ensure_assetforge_built():
    if os.path.exists(ASSETFORGE_CLI_DLL):
        return
    print("building AssetForge CLI (one-time)...")
    subprocess.run(
        ["dotnet", "build", os.path.join(ASSETFORGE_ROOT, "AssetForge.slnx"), "-v", "quiet"],
        check=True,
    )


def assetforge(*args):
    """Runs the AssetForge CLI and returns its parsed JSON result. Raises with the CLI's own stderr on
    failure, so a pixel-math problem surfaces as a normal Python exception here, not a silent no-op."""
    ensure_assetforge_built()
    proc = subprocess.run(
        ["dotnet", ASSETFORGE_CLI_DLL, *args], capture_output=True, text=True
    )
    if proc.returncode != 0:
        raise RuntimeError(f"assetforge {' '.join(args)} failed: {proc.stderr.strip()}")
    return json.loads(proc.stdout.strip().splitlines()[-1])


def load_overrides(path):
    return json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {}


def portraits_in_src():
    return sorted(os.path.splitext(f)[0] for f in os.listdir(SRC) if f.lower().endswith(".png"))


# ── Phase 1: background removal + trim (delegates to AssetForge) ────────────

def cmd_clean(ids, fuzz_pct):
    # Default: every PNG actually sitting in portraits_src/ — re-roll variants (arbalist_2.png,
    # imp_37.png, ...) and not-yet-renamed generic names (antmonster.png, female_12.png, ...) all still
    # need cleaning before anyone sorts/renames them.
    todo = ids or portraits_in_src()
    ensure(BACKUP_DIR)
    overrides = load_overrides(BG_OVERRIDES_PATH)
    done = 0
    for cid in todo:
        src_path = os.path.join(SRC, cid + ".png")
        if not os.path.exists(src_path):
            print(f"  {cid}: no source art at {src_path}, skipped")
            continue
        backup_path = os.path.join(BACKUP_DIR, cid + ".png")
        if not os.path.exists(backup_path):
            shutil.copyfile(src_path, backup_path)  # keep the true original exactly once, before any cleaning touches it

        mode = overrides.get(cid, {}).get("mode", "Auto")
        if mode == "Skip":
            print(f"  {cid}: mode=Skip, left untouched")
            done += 1
            continue

        report = assetforge("clean", src_path, src_path, "--fuzz", str(fuzz_pct), "--mode", mode)
        knocked = "yes" if report["knockout"] else "no"
        print(
            f"  {cid}: knockout={knocked}  "
            f"{report['originalWidth']}x{report['originalHeight']} -> {report['trimmedWidth']}x{report['trimmedHeight']}"
        )
        done += 1
    print(f"cleaned {done}/{len(todo)} (originals kept in {BACKUP_DIR})")


# ── Phase 2: eyes-in-frame crop math (mirrors BattleScreen.CutIns.cs exactly) ─

def cutin_geometry():
    """Every constant here is read directly off BattleScreen.CutIns.cs's ActionCard(), lines 47/77-93 —
    change them together if that layout ever changes. Units are "h" (the card's own height), so only
    ratios matter; h is left symbolic as 1.0.
    """
    h = 1.0
    w = 0.9 * h                                   # CutIns.cs:47  float w = h * 0.9f;
    win_w = w - 2 * (w * 0.14)                    # CutIns.cs:79  left/right = w*0.14
    win_h = h - (h * 0.08) - (h * 0.12)            # CutIns.cs:79  top = h*0.08, bottom = h*0.12
    box_left = -(w * 0.25)                        # CutIns.cs:88  portrait.left = -w*0.25 (relative to window)
    box_w = win_w + 2 * (w * 0.25)                 # CutIns.cs:88  portrait.right = -w*0.25 too
    box_top = -(h * 0.04)                          # CutIns.cs:88  portrait.top = -h*0.04
    box_h = 2.0 * h                                # CutIns.cs:88  portrait.height = h*2.0
    return dict(win_w=win_w, win_h=win_h, box_left=box_left, box_w=box_w, box_top=box_top, box_h=box_h)


def y_live_threshold():
    """The image aspect ratio (W/H) at or above which height stops being the binding 'contain' axis, so
    Y starts to have a visible effect. Derived from the geometry, not hand-picked."""
    g = cutin_geometry()
    return g["box_w"] / g["box_h"]


def fit_contain(img_w, img_h, g):
    img_aspect = img_w / img_h
    box_aspect = g["box_w"] / g["box_h"]
    if img_aspect <= box_aspect:
        disp_h = g["box_h"]
        disp_w = img_aspect * disp_h
    else:
        disp_w = g["box_w"]
        disp_h = disp_w / img_aspect
    return disp_w, disp_h


def solve_crop_pct(img_w, img_h, target_fx, target_fy):
    """Solves for the (X, Y) background-position percentages that place image-fraction point
    (target_fx, target_fy) at the center of the visible bust window. `background-size: contain` only
    ever leaves slack on ONE axis (whichever isn't binding) — for a narrow/tall portrait that's X
    (height binds), for a square-ish/wide one it flips to Y (width binds) — so exactly one of
    (x_is_live, y_is_live) is normally true, never both. Returns (x_pct, y_pct, x_is_live, y_is_live).
    Self-checks its own algebra on whichever axis is live by re-running the forward (CSS) formula and
    asserting it recovers the window-center target, so a transcription slip here fails loudly instead of
    silently mis-cropping."""
    g = cutin_geometry()
    disp_w, disp_h = fit_contain(img_w, img_h, g)
    slack_w = g["box_w"] - disp_w
    slack_h = g["box_h"] - disp_h
    x_is_live = slack_w > 1e-6
    y_is_live = slack_h > 1e-6

    def solve_axis(target_f, disp, slack, box_lo, win_span):
        win_center = win_span / 2.0
        if slack <= 1e-9:
            return 50.0, False  # no slack on this axis: position can't move anything, pick the harmless default
        pct = 100.0 * (win_center - box_lo - target_f * disp) / slack
        clamped = pct < 0.0 or pct > 100.0
        return max(0.0, min(100.0, pct)), clamped

    x_pct, x_clamped = solve_axis(target_fx, disp_w, slack_w, g["box_left"], g["win_w"]) if x_is_live else (50.0, False)
    y_pct, y_clamped = solve_axis(target_fy, disp_h, slack_h, g["box_top"], g["win_h"]) if y_is_live else (0.0, False)

    # Self-check: forward-simulate and confirm the window center maps back near the target fraction, but
    # only on whichever axis actually has slack to move — the other axis is pinned by contain and can't
    # be expected to hit anything. Skipped when the solved percentage had to be clamped into [0,100]: that
    # means the target point is genuinely out of reach for this image's geometry (e.g. a near-dead-center
    # centroid against an asymmetric box offset), not an algebra bug — clamping to the nearest edge is the
    # correct behavior there, and it can never recover the exact target by definition.
    fx_check, fy_check = visible_fraction_at(g["win_w"] / 2.0, g["win_h"] / 2.0, img_w, img_h, x_pct, y_pct, g, disp_w, disp_h)
    if x_is_live and not x_clamped:
        assert abs(fx_check - target_fx) < 0.02, f"crop-x self-check failed: {fx_check} vs {target_fx}"
    if y_is_live and not y_clamped:
        assert abs(fy_check - target_fy) < 0.02, f"crop-y self-check failed: {fy_check} vs {target_fy}"
    return x_pct, y_pct, x_is_live, y_is_live


def visible_fraction_at(wx, wy, img_w, img_h, x_pct, y_pct, g, disp_w, disp_h):
    slack_w = g["box_w"] - disp_w
    slack_h = g["box_h"] - disp_h
    img_left = g["box_left"] + slack_w * (x_pct / 100.0)
    img_top = g["box_top"] + slack_h * (y_pct / 100.0)
    return (wx - img_left) / disp_w, (wy - img_top) / disp_h


def visible_image_box(img_w, img_h, x_pct, y_pct):
    """The (l, t, r, b) rectangle, in the SOURCE image's own pixel coordinates, that the CutIns bust
    window actually shows for this crop. Used by `sheet` to render exactly what the game would show."""
    g = cutin_geometry()
    disp_w, disp_h = fit_contain(img_w, img_h, g)
    fx0, fy0 = visible_fraction_at(0, 0, img_w, img_h, x_pct, y_pct, g, disp_w, disp_h)
    fx1, fy1 = visible_fraction_at(g["win_w"], g["win_h"], img_w, img_h, x_pct, y_pct, g, disp_w, disp_h)
    fx0c, fx1c = max(0.0, fx0), min(1.0, fx1)
    fy0c, fy1c = max(0.0, fy0), min(1.0, fy1)
    return fx0c * img_w, fy0c * img_h, fx1c * img_w, fy1c * img_h


def estimate_crop(path, kind):
    """Head-position proxy: AssetForge's face/figure crop-box heuristic (the generic version of the
    logic Thumbs generation already trusts) gives the target point to keep centered in the bust window.
    Returns (x_pct, y_pct, x_is_live, y_is_live)."""
    box = assetforge("crop", path, "--kind", kind)
    w, h = box["imageWidth"], box["imageHeight"]
    target_fx = ((box["left"] + box["right"]) / 2.0) / w
    target_fy = ((box["top"] + box["bottom"]) / 2.0) / h
    return solve_crop_pct(w, h, target_fx, target_fy)


def cmd_crop(ids, kind):
    overrides = load_overrides(CROP_OVERRIDES_PATH)
    todo = ids or portraits_in_src()
    print(f"{'id':22s} {'aspect':>7s}  {'Y-live':6s} {'X':>6s} {'Y':>6s}  source")
    for cid in todo:
        path = os.path.join(SRC, cid + ".png")
        if not os.path.exists(path):
            print(f"  {cid}: not cleaned yet (run `clean` first), skipped")
            continue
        with Image.open(path) as im:
            w, h = im.size
        aspect = w / h
        if cid in overrides:
            x_pct, y_pct = overrides[cid]["X"], overrides[cid].get("Y", 0.0)
            y_live = aspect >= y_live_threshold()
            x_live = not y_live
            source = "override"
        else:
            x_pct, y_pct, x_live, y_live = estimate_crop(path, kind)
            source = "auto"
        live_axis = "Y" if y_live else "X" if x_live else "none"
        print(f"{cid:22s} {aspect:7.3f}  live={live_axis:4s} {x_pct:6.1f} {y_pct:6.1f}  {source}")


# ── Phase 3: visual contact sheet ─────────────────────────────────────────────

def cmd_sheet(ids, kind):
    overrides = load_overrides(CROP_OVERRIDES_PATH)
    todo = ids or portraits_in_src()
    ensure(SHEET_OUT)
    try:
        font = ImageFont.truetype("arialbd.ttf", 16)
    except OSError:
        font = ImageFont.load_default()
    panels = []
    D = 320
    for cid in todo:
        path = os.path.join(SRC, cid + ".png")
        if not os.path.exists(path):
            continue
        im = Image.open(path).convert("RGBA")
        w, h = im.size
        if cid in overrides:
            x_pct, y_pct = overrides[cid]["X"], overrides[cid].get("Y", 0.0)
        else:
            x_pct, y_pct, _, _ = estimate_crop(path, kind)
        l, t, r, b = visible_image_box(w, h, x_pct, y_pct)
        crop = im.crop((int(l), int(t), int(r), int(b)))
        canvas = Image.new("RGBA", (int(r - l), int(b - t)), (40, 20, 50, 255))
        canvas.alpha_composite(crop, (0, 0))
        canvas = canvas.resize((D, int(D * canvas.height / max(1, canvas.width))), Image.LANCZOS)
        panel = Image.new("RGBA", (D, D + 24), (20, 22, 40, 255))
        panel.alpha_composite(canvas, (0, 24 + max(0, (D - canvas.height) // 2)))
        ImageDraw.Draw(panel).text((4, 2), cid, font=font, fill=(255, 255, 255, 255))
        panels.append(panel)
    per_row = 3
    for n in range(0, len(panels), per_row * 3):
        grp = panels[n:n + per_row * 3]
        if not grp:
            continue
        pw, ph = grp[0].size
        rows = (len(grp) + per_row - 1) // per_row
        sheet = Image.new("RGBA", (pw * per_row, ph * rows), (0, 0, 0, 255))
        for j, p in enumerate(grp):
            sheet.paste(p, ((j % per_row) * pw, (j // per_row) * ph))
        out_path = os.path.join(SHEET_OUT, f"sheet{n // (per_row * 3):02d}.png")
        sheet.save(out_path)
        print(f"  wrote {out_path} ({len(grp)} portraits)")
    print(f"sheet: {len(panels)} portraits rendered")


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    p_clean = sub.add_parser("clean")
    p_clean.add_argument("--only", default="")
    p_clean.add_argument("--fuzz", type=float, default=3.0)
    p_crop = sub.add_parser("crop")
    p_crop.add_argument("--only", default="")
    p_crop.add_argument("--kind", default="Figure", choices=["Face", "Figure"])
    p_sheet = sub.add_parser("sheet")
    p_sheet.add_argument("--only", default="")
    p_sheet.add_argument("--kind", default="Figure", choices=["Face", "Figure"])
    args = ap.parse_args()
    ids = args.only.split(",") if args.only else []
    if args.cmd == "clean":
        cmd_clean(ids, args.fuzz)
    elif args.cmd == "crop":
        cmd_crop(ids, args.kind)
    elif args.cmd == "sheet":
        cmd_sheet(ids, args.kind)


if __name__ == "__main__":
    sys.exit(main())
