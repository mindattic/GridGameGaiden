"""
UI verification gate: drives the real development player through the screens and asserts behavior
(not just "it rendered"). Writes artifacts/uicheck/report.json and exits non-zero on any failure.

    python tools/uicheck.py
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import harness as h  # noqa: E402

results = []


def check(name, ok, detail=""):
    results.append({"check": name, "ok": bool(ok), "detail": detail})
    print(("PASS " if ok else "FAIL ") + name + (f" — {detail}" if detail and not ok else ""))


def texts():
    return h.call("texts")


def has_text(sub):
    return any(sub in t for t in texts())


def wait_idle(ms=300):
    return h.call("wait", ms=ms)


def battle_state():
    return h.call("state").get("battle")


def main():
    art = h.launch("uicheck", fresh=True)

    # ── Title / save slots ─────────────────────────────────────────────────
    check("title shows New Game", has_text("New Game"))
    check("title music", h.call("music")["track"] == "TellerOfTheTales")
    h.call("clicktext", text="New Game"); time.sleep(0.8)
    check("slot screen lists 3 slots", sum(1 for t in texts() if t.startswith("Slot ")) == 3)
    h.call("clicktext", text="Slot 1"); time.sleep(1.2)
    st = h.call("state")
    check("new game lands at camp", st["screen"] == "hub", st["screen"])
    check("camp music", h.call("music")["track"] == "MinstrelGuild")
    for item in ["Adventure", "Items", "Status", "Equip", "Abilities", "Party", "Merchant", "Blacksmith", "Alchemist", "Inn", "Summon Circle", "Bounties", "Bestiary", "Settings"]:
        if not has_text(item):
            check(f"camp menu has {item}", False)
            break
    else:
        check("camp menu has every command", True)

    # Overwrite confirm on an occupied slot
    h.call("clicktext", text="Save & Quit"); time.sleep(1.0)
    h.call("clicktext", text="New Game"); time.sleep(0.8)
    h.call("clicktext", text="Slot 1"); time.sleep(0.6)
    check("occupied slot asks to overwrite", has_text("Overwrite slot 1?"))
    h.call("clicktext", text="Cancel"); time.sleep(0.4)
    h.call("back"); time.sleep(0.8)
    h.call("clicktext", text="Continue"); time.sleep(1.2)
    check("continue resumes the save", h.call("state")["screen"] == "hub")

    # ── Adventure ordering ─────────────────────────────────────────────────
    h.call("clicktext", text="Adventure"); time.sleep(0.8)
    t = texts()
    check("adventure lists the first stage", any("Green Valley I" in x for x in t))
    check("locked stages collapse into one teaser", any("more stages ahead" in x for x in t))
    h.call("clicktext", text="Green Valley I"); time.sleep(1.5)
    if h.call("state")["screen"] == "crawl":
        check("story crawl plays on first entry", True)
        h.call("clicktext", text="Skip"); time.sleep(1.5)
    check("battle screen", h.call("state")["screen"] == "battle")
    time.sleep(1.0)
    check("first battle starts the guided tutorial", has_text("Tutorial 1/4"))
    time.sleep(1.0)
    check("tutorial shows the suggested move right away", (h.call("banner")["text"] or "").startswith(("Hint", "Tip")))

    check("battle music", h.call("music")["track"] == "Crusade")

    # ── HUD ────────────────────────────────────────────────────────────────
    names = h.call("names", prefix="")
    for el in ["board", "abilitybar", "orbs", "shield", "menu", "feed"]:
        check(f"HUD element {el}", el in names)
    check("timeline icons present", any(n.startswith("tl-") for n in names))
    check("enemy turn icons are pixel-art sprites", "px" in names, "no 'px' sprite under the timeline icons")
    b = battle_state()
    enemies = [u for u in b["units"] if u["team"] == "Enemy"]
    check("countdown badges show 'acts in' numbers", all(u["acts_in"] >= 0 for u in enemies) and any(x.isdigit() for x in texts()))
    fps = h.call("state")["fps"]
    check("frame rate ≥ 50", fps >= 50, f"fps={fps}")

    # ── Actor panel hides unscanned enemy HP ──────────────────────────────
    e = enemies[0]
    h.call("tap", x=h.tile(e["x"], e["y"])[0], y=h.tile(e["x"], e["y"])[1]); time.sleep(0.4)
    check("tapping an enemy shows it with hidden HP", has_text("???"))

    # ── Drag preview + pincer banner (bot's best move), then commit ───────
    sug = h.call("suggest")
    if isinstance(sug, dict) and sug.get("path"):
        pts = [h.tile(*sug["from"])] + [h.tile(x, y) for x, y in sug["path"]]
        h.call("cues", clear=1)
        h.call("drag", path=";".join(f"{x},{y}" for x, y in pts), step=0.1, hold=1)
        time.sleep(0.3)
        banner = h.call("banner")["text"] or ""
        if sug["pincers"] > 0:
            check("pincer preview banner while holding", "PINCER" in banner, banner)
        if sug["moved"]:
            m = sug["moved"][0]
            el = h.call("element", name=f"unit-{m['id']}")
            tx, ty = h.tile(m["x"], m["y"])
            ok = abs(el["x"] + el["w"] / 2 - tx) < 40 and abs(el["y"] + el["h"] / 2 - ty) < 40
            check("displaced unit slides to its preview tile while holding", ok, json.dumps(el))
        h.shot("drag-hold")
        h.call("release")
        st = wait_idle(400)["battle"]
        check("drop commits one action", st["results"]["ActionsTaken"] == 1, str(st["results"]))
        tut = [x for x in texts() if x.startswith("Tutorial ")]
        check("the tutorial advances after the first move", any(x in ("Tutorial 2/4", "Tutorial 3/4") for x in tut), f"tutorial texts={tut} panel={[x for x in texts()][-8:]}")
        h.call("click", name="tutorial-skip"); time.sleep(0.4)
        check("tutorial can be skipped", not any(x.startswith("Tutorial ") for x in texts()))
        cues = h.call("cues")
        check("pick-up and every step are voiced", "pickup" in cues and cues.count("move") >= len(sug["path"]), ",".join(cues[:20]))
        if sug["pincers"] > 0:
            check("a pincer plays the close-up cut-in", st.get("cutins", 0) >= 1, str(st.get("cutins")))
            check("pincer and hits are voiced", "pincer" in cues and any(c in cues for c in ("hit", "crit", "miss")), ",".join(cues[:30]))
    # Every cue the game can play resolves to a bundled clip (no silent events).
    else:
        check("bot suggested a move", False, str(sug))

    # ── Pincer close-up: portraits on screen mid-sweep, camera pushed in, then all restored ──
    h.call("democutin"); time.sleep(0.5)
    names = h.call("names")
    portraits = [n for n in names if str(n).startswith("cutin-")] if isinstance(names, list) else []
    check("cut-in shows both attackers' portraits", len(portraits) == 2, str(portraits))
    h.shot("cutin")
    time.sleep(1.4)
    names = h.call("names")
    check("cut-in cleans up after itself", isinstance(names, list) and not any(str(n).startswith("cutin") for n in names))

    # ── Targeting + ability card + cancel ─────────────────────────────────
    h.call("givemana", n=8)
    b = battle_state()
    hero = next(u for u in b["units"] if u["team"] == "Hero")
    hx, hy = h.tile(hero["x"], hero["y"])
    h.call("tap", x=hx, y=hy); time.sleep(0.3)
    slot0 = (25 + 108, 450 + 1440 + 20 + 98)
    h.call("tap", x=slot0[0], y=slot0[1]); time.sleep(0.5)
    st = battle_state()
    if st["targeting"] == 0:
        check("slot tap enters targeting", True)
        check("ability card shows while targeting", has_text("Tap a highlighted tile"))
        h.shot("targeting")
        h.call("tap", x=slot0[0], y=slot0[1]); time.sleep(0.4)
        check("re-tapping the slot cancels", battle_state()["targeting"] == -1)
    else:
        check("slot tap enters targeting (or auto-casts)", st["results"]["ActionsTaken"] >= 2, str(st["targeting"]))

    # ── Generic mana orbs: filled vs empty slots read at a glance ─────────
    h.call("givemana", n=0)  # forces an orb resync
    bank = battle_state()["bank"]
    have = int(bank.split("/")[0])
    filled = h.call("style", name="orb0")["tint"]
    empty = h.call("style", name=f"orb{min(have, 11)}")["tint"] if have < 12 else filled
    check("orb line shows the bank count in one orb color", have >= 1 and (have >= 12 or filled != empty), f"{bank} {filled} vs {empty}")

    # ── Buff pips with timer ring (US-155/156) ───────────────────────────
    b = battle_state()
    target = next(u for u in b["units"] if u["team"] == "Enemy")
    h.call("buff", unit=target["Id"], id="burning")
    st = h.call("style", name=f"buffs-{target['Id']}")
    check("buff shows as a pip on the unit", st.get("children", 0) >= 1, str(st))

    # ── Long-press tooltip ────────────────────────────────────────────────
    wait_idle(200)
    h.call("press", x=slot0[0], y=slot0[1], ms=700)
    time.sleep(0.2)
    check("long-press shows the ability card", any(("Instant" in x) or ("Cast time" in x) or ("Skill" in x) for x in texts()))
    h.call("release"); time.sleep(0.3)

    # ── Pause menu ────────────────────────────────────────────────────────
    h.call("click", name="menu"); time.sleep(0.5)
    t = texts()
    check("pause menu has Resume / Auto battle / Retreat", all(any(k in x for x in t) for k in ("Resume", "Auto battle", "Retreat")))
    h.call("clicktext", text="Resume"); time.sleep(0.3)

    # ── Idle hint (first battles) ─────────────────────────────────────────
    wait_idle(200)
    time.sleep(6.0)
    banner = h.call("banner")["text"] or ""
    check("idle hint appears", banner.startswith("Hint") or banner.startswith("Tip"), banner)
    h.shot("hint")

    # ── Settings toggles don't break the battle ───────────────────────────
    for key in ("colorblind", "reducemotion"):
        h.call("setting", key=key, value=1)
    h.call("speed", x=3)
    h.call("autoplay", on=1)
    for _ in range(60):
        time.sleep(1.0)
        s = h.call("state")
        if s["screen"] != "battle":
            break
    check("battle finishes with colorblind + reduce motion + ×3 speed", h.call("state")["screen"] == "postbattle")
    check("results music", h.call("music")["track"] in ("HeroicAge", "Heartbreaking"))
    h.shot("results")
    h.call("setting", key="colorblind", value=0)
    h.call("setting", key="reducemotion", value=0)

    # ── Letterbox ─────────────────────────────────────────────────────────
    h.call("resize", w=1280, h=720); time.sleep(1.2)
    sb = h.call("stagebounds")
    aspect = sb["w"] / sb["h"]
    centered = abs((sb["x"] + sb["w"] / 2) - sb["screen"][0] / 2) < 4
    check("wide window letterboxes (aspect kept, centered)", abs(aspect - 1170 / 2532) < 0.01 and centered, json.dumps(sb))
    h.shot("letterbox-wide")
    h.call("resize", w=585, h=1266); time.sleep(1.0)

    # ── Boss styling (US-196): purple frame on the 2×2 Cyclops ───────────
    h.call("back"); time.sleep(0.4)
    h.call("clicktext", text="Continue"); time.sleep(1.0)
    h.call("battle", stage="cave-02"); time.sleep(2.0)
    b = battle_state()
    boss = next((u for u in b["units"] if u["cls"].startswith("cyclops")), None)
    if boss is None:
        for _ in range(30):  # the cyclops may arrive in a later wave: autoplay until it appears
            h.call("autoplay", on=1); time.sleep(1.0)
            b = battle_state() or {}
            boss = next((u for u in b.get("units", []) if u["cls"].startswith("cyclops")), None)
            if boss or not b: break
        h.call("autoplay", on=0)
    if boss:
        fr = h.call("child_style", name=f"unit-{boss['Id']}", index=1)
        kl = h.call("child_style", name=f"unit-{boss['Id']}", index=1, child=1)
        check("unit frame uses the menu portrait frame (white border)", fr.get("border", "").upper().startswith("#ECEEFF"), str(fr))
        check("2×2 boss gets the purple boss keyline", kl.get("border", "").upper().startswith("#DB66FF"), str(kl))
        h.shot("boss")
    else:
        check("2×2 boss appears in Frostmaw Caverns II", False)

    # ── Build settings (US-329) ───────────────────────────────────────────
    ps = open(os.path.join(h.ROOT, "ProjectSettings", "ProjectSettings.asset"), encoding="utf-8").read()
    ver = open(os.path.join(h.ROOT, "version.txt"), encoding="utf-8").read().strip()
    check("portrait orientation, no splash, version from version.txt",
          "defaultScreenOrientation: 0" in ps and "m_ShowUnitySplashScreen: 0" in ps and f"bundleVersion: {ver}" in ps)

    logs = h.call("logs")
    errs = [l for l in logs if l.startswith(("Error", "Exception")) and "first mouse position" not in l]
    check("no errors or exceptions logged", not errs, " | ".join(e[:160] for e in errs[:5]))

    # ── Safe area (US-287): simulate a 120 px notch + 80 px home bar on a 585×1266 window ─────
    try:
        h.call("quit", timeout=3)
    except Exception:
        pass
    time.sleep(1.5)
    h.launch("uicheck-safearea", fresh=True, extra=("-safeArea", "120,80"))
    time.sleep(1.0)
    el = h.call("element", name="screens")
    # 120 screen px at 585 px wide = 240 design px; the stage fills the window exactly.
    check("safe-area insets push content below the notch and above the home bar",
          abs(el["y"] - 240) < 6 and abs((2532 - (el["y"] + el["h"])) - 160) < 6, str(el))
    h.shot("safearea-title")

    passed = sum(1 for r in results if r["ok"])
    report = {"passed": passed, "failed": len(results) - passed, "results": results}
    with open(os.path.join(art, "report.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)
    print(f"\n{passed}/{len(results)} UI checks passed → {os.path.join(art, 'report.json')}")
    try:
        h.call("quit", timeout=3)
    except Exception:
        pass
    sys.exit(0 if passed == len(results) else 1)


if __name__ == "__main__":
    main()
