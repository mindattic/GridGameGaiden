"""Plays a session on an Android device/emulator through the dev-build automation endpoint (adb forward tcp:7777)."""
import json, sys, time, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import harness as h
res = []
def check(n, ok, d=""): res.append(ok); print(("PASS " if ok else "FAIL ") + n + ("" if ok else " — " + d))
st = h.call("state"); check("app boots to the title on Android", st["screen"] == "title", str(st))
check("UI tree is laid out at device resolution", h.call("stagebounds")["w"] > 0)
h.call("clicktext", text="New Game"); time.sleep(2)
h.call("clicktext", text="Slot 1"); time.sleep(3)
check("touch input reaches the menu (New Game → Slot 1 → camp)", h.call("state")["screen"] == "hub")
h.call("clicktext", text="Adventure"); time.sleep(2)
h.call("clicktext", text="Green Valley I"); time.sleep(3)
if h.call("state")["screen"] == "crawl": h.call("clicktext", text="Skip"); time.sleep(3)
check("a battle starts", h.call("state")["screen"] == "battle")
h.call("click", name="tutorial-skip"); time.sleep(1)
h.call("speed", x=3); h.call("autoplay", on=1)
for _ in range(240):
    time.sleep(1.5)
    s = h.call("state")
    if s["screen"] != "battle": break
check("the battle plays to its results on Android", h.call("state")["screen"] == "postbattle", h.call("state")["screen"])
h.call("clicktext", text="Continue"); time.sleep(3)
save = h.call("state").get("save", {})
check("the result is saved (gold earned, stage cleared)", save.get("HighestClearedStage", -1) >= 0 and save.get("Gold", 0) > 200, json.dumps(save)[:200])
errs = [l for l in h.call("logs") if l.startswith(("Error", "Exception"))]
check("no errors or exceptions logged", not errs, " | ".join(e[:150] for e in errs[:4]))
print(f"{sum(res)}/{len(res)} Android checks passed"); sys.exit(0 if all(res) else 1)
