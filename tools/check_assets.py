"""Fails if any sound cue or music track the code references has no bundled file (no silent events),
or an enemy has no pixel-art timeline sprite."""
import glob, os, re, sys
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SFX = os.path.join(ROOT, "Assets", "Gaiden", "Resources", "Audio", "Sfx")
MUSIC = os.path.join(ROOT, "Assets", "Gaiden", "Resources", "Audio", "Music")
SPECIAL = {"ui.click": "ui_click", "ui.back": "ui_click", "ui.select": "ui_select", "ui.buy": "ui_coin", "ui.error": "ui_error", "move": "move_0", "hit": "hit_0"}
code = "".join(open(p, encoding="utf-8").read() for p in glob.glob(os.path.join(ROOT, "Assets", "Gaiden", "App", "**", "*.cs"), recursive=True))
cues = set(re.findall(r'Sfx\("([^"]+)"', code))
cues |= set(re.findall(r'\? "([a-z.]+)" : "([a-z.]+)"', code) and [c for pair in re.findall(r'Sfx\([^;]*?"([a-z.]+)"\s*:\s*"([a-z.]+)"', code) for c in pair] or [])
for m in re.findall(r'case DamageType\.\w+: return "([a-z]+)";', code): cues.add(m)
missing = [c for c in sorted(cues) if not os.path.exists(os.path.join(SFX, SPECIAL.get(c, c) + ".ogg"))]
tracks = set(re.findall(r'Music => "([A-Za-z]+)"', code)) | set(re.findall(r'\? "([A-Z][A-Za-z]+)" : "([A-Z][A-Za-z]+)"', code) and [t for p in re.findall(r'Music => [^;]*?"([A-Z][A-Za-z]+)"\s*:\s*"([A-Z][A-Za-z]+)"', code) for t in p] or [])
missing += [f"music:{t}" for t in sorted(tracks) if not os.path.exists(os.path.join(MUSIC, t + ".mp3"))]
import json
classes = json.load(open(os.path.join(ROOT, "Assets", "Gaiden", "Resources", "Content", "classes.json"), encoding="utf-8"))
classes = next(iter(classes.values())) if isinstance(classes, dict) else classes
PIXEL = os.path.join(ROOT, "Assets", "Gaiden", "Resources", "Pixel")
missing += [f"pixel:{c['Id']} (run tools/make_pixel_icons.py)" for c in classes
            if c.get("Team") == "Enemy" and not os.path.exists(os.path.join(PIXEL, c["Id"] + ".png"))]
RESD = os.path.join(ROOT, "Assets", "Gaiden", "Resources")
def _ids(name): return [x["Id"] for x in json.load(open(os.path.join(RESD, "Content", name), encoding="utf-8"))]
for folder, ids in (("AbilityIcons", _ids("abilities.json")), ("ItemIcons", _ids("items.json")), ("StatusIcons", _ids("buffs.json") + ["stunned"])):
    missing += [f"icon:{folder}/{i} (run tools/make_icons.py)" for i in ids if not os.path.exists(os.path.join(RESD, folder, i + ".png"))]
if missing:
    print("MISSING ASSETS:", ", ".join(missing)); sys.exit(1)
print(f"asset check OK ({len(cues)} cues, {len(tracks)} tracks, enemy pixel sprites, pixel icons)")
