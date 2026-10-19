"""
Eye-annotation pass 1 (see docs/PIXEL_ICONS.md): full-figure sheets of every enemy at exactly 8x the pixel
sprite's geometry (same knockout + trim + fit as tools/make_pixel_icons.py), with sprite-pixel ticks every 4.
Use them to find each head; record head centres in tools/eye_heads.json.

    python tools/eyesheet.py [id,id,...]      -> artifacts/eyes/sheetNN.png
"""
import os, subprocess, sys, json
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_pixel_icons as m

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "artifacts", "eyes")
os.makedirs(OUT, exist_ok=True)
classes = json.load(open(os.path.join(m.RES, "Content", "classes.json"), encoding="utf-8"))
ids = [c["Id"] for c in classes if c["Team"] == "Enemy"]
only = sys.argv[1].split(",") if len(sys.argv) > 1 and sys.argv[1] else None
if only: ids = [i for i in ids if i in only]
Z = 8  # panel px per sprite px
font = ImageFont.truetype("arial.ttf", 13)
big = ImageFont.truetype("arialbd.ttf", 18)


def panel(i):
    src = os.path.join(m.SRC, i + ".png"); hi = os.path.join(OUT, i + "_hi.png")
    args = m.magick_args(src, hi)
    # same knockout + trim, but resize to 8x the sprite geometry and stop before quantising
    cut = args.index("-statistic")
    head = args[:cut]
    if not os.path.exists(hi): subprocess.run(head + ["-filter", "Triangle", "-resize", f"{32*Z}x{32*Z}", "-background", "none", "-gravity", "center",
                           "-extent", f"{36*Z}x{36*Z}", "PNG32:" + hi], check=True)
    im = Image.open(hi).convert("RGBA")
    bg = Image.new("RGBA", im.size, (90, 96, 120, 255))
    bg.alpha_composite(im)
    pad = 26
    can = Image.new("RGBA", (bg.width + pad, bg.height + pad + 22), (20, 22, 40, 255))
    can.paste(bg, (pad, pad + 22))
    d = ImageDraw.Draw(can)
    for k in range(37):
        x = pad + k * Z; y = pad + 22 + k * Z
        col = (255, 255, 0, 70) if k % 4 == 0 else None
        if col:
            d.line([(x, pad + 22), (x, can.height)], fill=col, width=1)
            d.line([(pad, y), (can.width, y)], fill=col, width=1)
        if k % 4 == 0 and k < 36:
            d.text((x + 1, pad + 8), str(k), font=font, fill=(255, 255, 0, 255))
            d.text((2, y + 1), str(k), font=font, fill=(255, 255, 0, 255))
    d.text((pad, 0), i, font=big, fill=(255, 255, 255, 255))
    return can


panels = [panel(i) for i in ids]
per = 9
for n in range(0, len(panels), per):
    grp = panels[n:n + per]
    w = grp[0].width; h = grp[0].height
    sheet = Image.new("RGBA", (w * 3, h * ((len(grp) + 2) // 3)), (0, 0, 0, 255))
    for k, p in enumerate(grp): sheet.paste(p, ((k % 3) * w, (k // 3) * h))
    sheet.save(os.path.join(OUT, f"sheet{n // per:02d}.png"))
print(len(panels), "panels")
