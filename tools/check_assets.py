"""Fails if any sound cue or music track the code references has no bundled file (no silent events)."""
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
if missing:
    print("MISSING AUDIO:", ", ".join(missing)); sys.exit(1)
print(f"audio check OK ({len(cues)} cues, {len(tracks)} tracks)")
