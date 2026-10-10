"""
Eye-annotation pass 2 (see docs/PIXEL_ICONS.md): zoomed head crops centred on tools/eye_heads.json, with a
per-sprite-pixel grid and labels in sprite coordinates. Read each eye's pixel off the grid and record it in
tools/eyes.json. Each panel's y labels sit at its own left edge.

    python tools/eyesheet.py && python tools/eyezoom.py [id,id,...]   -> artifacts/eyes/zoomNN.png
"""
import os, sys, json
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import import_art as ia

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "artifacts", "eyes")
RES = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Gaiden", "Resources")
classes = json.load(open(os.path.join(RES, "Content", "classes.json"), encoding="utf-8"))
ids = [c["Id"] for c in classes if c["Team"] == "Enemy"]
# optional overrides: id -> (cx, cy, size) in sprite px
OV = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "eye_heads.json"))) if os.path.exists(os.path.join(os.path.dirname(os.path.abspath(__file__)), "eye_heads.json")) else {}
if len(sys.argv) > 1 and sys.argv[1]: ids = [i for i in ids if i in sys.argv[1].split(",")]
Z = 8; D = 280
font = ImageFont.truetype("arial.ttf", 13); big = ImageFont.truetype("arialbd.ttf", 16)
panels = []
for i in ids:
    hi = Image.open(os.path.join(OUT, i + "_hi.png")).convert("RGBA")
    if i in OV:
        cx, cy, sz = OV[i]; cx *= Z; cy *= Z; sz *= Z
    else:
        l, t, r, b = ia.face_crop(hi); cx, cy = (l + r) / 2, (t + b) / 2; sz = max(r - l, 12 * Z) * 0.9
    sz = int(round(sz / Z)) * Z
    x0 = int(round((cx - sz / 2) / Z)) * Z; y0 = int(round((cy - sz / 2) / Z)) * Z
    crop = Image.new("RGBA", (sz, sz), (90, 96, 120, 255))
    crop.alpha_composite(hi.crop((x0, y0, x0 + sz, y0 + sz)))
    crop = crop.resize((D, D), Image.LANCZOS)
    pad = 24
    can = Image.new("RGBA", (D + pad, D + pad + 20), (20, 22, 40, 255)); can.paste(crop, (pad, pad + 20))
    d = ImageDraw.Draw(can); k = D / (sz / Z)
    for n in range(sz // Z + 1):
        sx = x0 // Z + n; sy = y0 // Z + n
        x = pad + n * k; y = pad + 20 + n * k
        d.line([(x, pad + 20), (x, pad + 20 + 6)], fill=(255, 255, 0, 255)); d.line([(x, can.height - 6), (x, can.height)], fill=(255, 255, 0, 255))
        d.line([(pad, y), (pad + 6, y)], fill=(255, 255, 0, 255)); d.line([(can.width - 6, y), (can.width, y)], fill=(255, 255, 0, 255))
        if sx % 2 == 0: d.text((x + 1, pad + 5), str(sx), font=font, fill=(255, 255, 0, 255))
        if sy % 2 == 0: d.text((1, y + 1), str(sy), font=font, fill=(255, 255, 0, 255))
    # faint pixel grid
    ov = Image.new("RGBA", can.size, (0, 0, 0, 0)); od = ImageDraw.Draw(ov)
    for n in range(sz // Z + 1):
        x = pad + n * k; y = pad + 20 + n * k
        od.line([(x, pad + 20), (x, can.height)], fill=(255, 255, 255, 38)); od.line([(pad, y), (can.width, y)], fill=(255, 255, 255, 38))
    can.alpha_composite(ov)
    d.text((pad, 0), i, font=big, fill=(255, 255, 255, 255))
    panels.append(can)
per = 9
for n in range(0, len(panels), per):
    grp = panels[n:n + per]; w = grp[0].width; h = grp[0].height
    sheet = Image.new("RGBA", (w * 3, h * ((len(grp) + 2) // 3)), (0, 0, 0, 255))
    for j, p in enumerate(grp): sheet.paste(p, ((j % 3) * w, (j // 3) * h))
    sheet.save(os.path.join(OUT, f"zoom{n // per:02d}.png"))
print(len(panels))
