"""
Self-play harness: launches the development player with the automation endpoint and drives it.

    python tools/harness.py smoke            # title → new game → hub → battle (autoplay) → results
    python tools/harness.py sweep            # screenshot + layout-audit every menu screen
    python tools/harness.py clips N          # generate N random-stage/random-wave battle clips
    python tools/harness.py durability       # GG-T39: last-strike weapon badge + drag-preview shatter warning
    python tools/harness.py shot NAME        # screenshot the running player
    python tools/harness.py call PATH [k=v]  # raw endpoint call, e.g. call state

Screenshots land in artifacts/<run>/. Design coordinates are 1170x2532 (top-left origin).
"""
import json
import os
import subprocess
import sys
import time
import urllib.parse
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXE = os.path.join(ROOT, "Build", "Windows", "GridGameGaiden.exe")
# GAIDEN_PORT keeps this project's player off ports other projects' harnesses use (GridGameCaravan also defaults to 7777).
PORT = int(os.environ.get("GAIDEN_PORT", "7787"))
BASE = f"http://127.0.0.1:{PORT}"

BOARD_LEFT, BOARD_TOP, TILE = 45, 450, 180


def tile(x, y):
    return (BOARD_LEFT + TILE * x + TILE // 2, BOARD_TOP + TILE * y + TILE // 2)


def call(endpoint, timeout=130, **params):
    q = urllib.parse.urlencode({k: v for k, v in params.items() if v is not None})
    url = f"{BASE}/{endpoint}" + (f"?{q}" if q else "")
    with urllib.request.urlopen(url, timeout=timeout) as r:
        body = r.read().decode("utf-8")
    try:
        return json.loads(body)
    except json.JSONDecodeError:
        return body


def alive():
    try:
        call("ping", timeout=2)
        return True
    except Exception:
        return False


def launch(run, width=585, height=1266, extra=(), fresh=False):
    if alive():
        try:
            call("quit", timeout=3)
        except Exception:
            pass
        time.sleep(1.5)
    art = os.path.join(ROOT, "artifacts", run)
    saves = os.path.join(ROOT, "artifacts", run, "saves")
    if fresh and os.path.isdir(saves):
        import shutil
        shutil.rmtree(saves, ignore_errors=True)
    os.makedirs(saves, exist_ok=True)
    args = [EXE, "-automation", str(PORT), "-artifacts", art, "-saveRoot", saves,
            "-screen-width", str(width), "-screen-height", str(height), "-screen-fullscreen", "0",
            "-logFile", os.path.join(art, "player.log"), *extra]
    subprocess.Popen(args, cwd=ROOT)
    for _ in range(60):
        if alive():
            return art
        time.sleep(0.5)
    raise RuntimeError("player did not start")


def shot(name):
    return call("screenshot", name=name)["file"]


def drag_tiles(*tiles, step=0.12):
    pts = ";".join(f"{x},{y}" for x, y in (tile(*t) for t in tiles))
    return call("drag", path=pts, step=step)


def smoke(run="smoke"):
    art = launch(run)
    files = [shot("01-title")]
    call("clicktext", text="New Game"); time.sleep(0.8)
    call("clicktext", text="Slot 1"); time.sleep(1.2)
    files.append(shot("02-hub"))
    call("clicktext", text="Adventure"); time.sleep(0.8)
    files.append(shot("03-stages"))
    call("clicktext", text="Green Valley I"); time.sleep(1.5)
    st = call("state")
    if st["screen"] == "crawl":
        files.append(shot("04-crawl"))
        call("clicktext", text="Skip"); time.sleep(1.5)
    files.append(shot("05-battle"))
    call("autoplay", on=1)
    for i in range(40):
        time.sleep(1.5)
        st = call("state")
        if i % 3 == 0 and st["screen"] == "battle":
            files.append(shot(f"06-battle-{i:02d}"))
        if st["screen"] != "battle":
            break
    time.sleep(1)
    files.append(shot("07-results"))
    logs = call("logs")
    print(json.dumps({"final": call("state").get("screen"), "save": call("state").get("save"), "errors": [l for l in logs if l.startswith(("Error", "Exception"))]}, indent=1))
    print("\n".join(files))


def sweep(run="sweep"):
    launch(run)
    call("newgame", slot=0, gold=5000)
    time.sleep(1)
    report = {}
    for screen in ["hub", "stages", "items", "status", "equip", "abilities", "party", "shop", "blacksmith",
                   "alchemist", "summon", "bounties", "bestiary", "settings", "credits"]:
        call("goto", screen=screen)
        time.sleep(0.9)
        shot(f"sweep-{screen}")
        report[screen] = call("layout").get("problems")
        call("back")
        time.sleep(0.6)
    print(json.dumps(report, indent=1))
    logs = call("logs")
    print("errors:", [l for l in logs if l.startswith(("Error", "Exception"))])


def campaign(run="campaign", stages=6, speed=3, extra=()):
    """Autoplays consecutive stages through the real UI (Next stage / Retry), checking for stalls and errors.
    Pass extra=("-capture", "video", "screen", "5") to record the run (capture.json must have "enabled": true).
    The raw tape grows fast at HighQuality — ~500MB+/minute of real time observed on one run — so a
    continuous capture of more than a handful of stages means a very large .mkv before it's finalized
    into the much smaller .mp4; capture.json's battle-start/battle-end rules (separate per-battle clips)
    are the safer default for anything longer than a short highlight."""
    launch(run, extra=extra)
    call("newgame", slot=0)
    time.sleep(1)
    call("speed", x=speed)
    call("clicktext", text="Adventure"); time.sleep(0.8)
    call("clicktext", text="Green Valley I"); time.sleep(1.2)
    if call("state")["screen"] == "crawl":
        call("clicktext", text="Skip"); time.sleep(1.2)
    results = []
    for n in range(stages):
        call("autoplay", on=1)
        t0 = time.time(); last_clock = -1; stuck = 0; shot_taken = False; stalled = False
        while time.time() - t0 < 480:
            time.sleep(1.0)
            st = call("state")
            if st["screen"] != "battle":
                break
            b = st["battle"]
            if not shot_taken and b["clock"] > 60:
                shot(f"stage{n}-mid"); shot_taken = True
            stuck = stuck + 1 if b["clock"] == last_clock and not b["busy"] else 0
            last_clock = b["clock"]
            if stuck > 12:
                results.append({"stage": n, "result": "STALLED", "state": b})
                stalled = True
                break
        if stalled:
            # Battle never reached postbattle; recording a second entry below would misrepresent the run.
            continue
        time.sleep(1.2)
        st = call("state")
        for _ in range(8):  # one "Skip" per hero that leveled up (LevelUpScreen.Advance), capped at MaxPartySize
            if st["screen"] != "levelup":
                break
            call("clicktext", text="Skip"); time.sleep(0.7)
            st = call("state")
        shot(f"stage{n}-result")
        results.append({"stage": n, "screen": st["screen"], "gold": st.get("save", {}).get("Gold"), "levels": st.get("save", {}).get("levels")})
        if st["screen"] != "postbattle":
            break
        # PostBattleScreen (owner's Expedition rework) names its continuation button "next" whether
        # its text reads "Next battle ›" (mid-expedition) or "Next expedition ›" (crossing into the
        # next one) — there is no literal "Next stage" button. Only a dead-end (last unlocked content)
        # lacks a "next" element, in which case "Continue" is the only way forward (back to the Hub).
        #
        # AutomationServer's click/clicktext could occasionally go silently unresponsive on a
        # long-lived player process (GG-T16): the HTTP call replies ok:true with plausible coordinates,
        # but the tap never registers and the screen never advances. Root-caused and fixed in GG-T20
        # (an orphaned Confirm veil eating every click) — this verify-and-retry is kept as defense in
        # depth rather than ripped out, since it's cheap and a real human-facing path could still leave
        # some other UI element in a dead state we haven't found yet.
        for attempt in range(5):
            if attempt == 3:  # GG-T16 recovery: try recreating the virtual mouse device before giving up
                print(f"stage{n}: resetting automation input device before final retries")
                call("resetinput")
                time.sleep(0.5)
            r = call("click", name="next")
            if isinstance(r, dict) and r.get("error"):
                call("clicktext", text="Continue")
            time.sleep(1.5)
            if call("state")["screen"] != "postbattle":
                break
            print(f"stage{n}: click(next)/Continue didn't advance, retry {attempt + 1}/5")
        else:
            print(f"stage{n}: click retries exhausted, screen still postbattle — stopping run")
            break
        if call("state")["screen"] == "crawl":
            call("clicktext", text="Skip"); time.sleep(1.2)
    logs = call("logs")
    print(json.dumps(results, indent=1))
    print("errors:", [l[:300] for l in logs if l.startswith(("Error", "Exception")) and "first mouse" not in l])


def battle_clips(run="clips", count=10, speed=3, timeout=240, capture=True):
    """Generates many short, independent battle clips via the randombattle endpoint: a random real
    stage + a random wave from it (one enemy family per clip, per the house wave convention), at that
    stage's real recommended level. No UI clicks are involved in the loop at all (ResetTo tears down
    whatever screen a prior clip left behind), so this sidesteps the click-reliability concerns (GG-T16,
    root-caused and fixed as the orphaned-Confirm-veil bug in GG-T20) that campaign() has to guard against.
    capture.json's battle-start/battle-end rules are NOT auto-loaded just by sitting in StreamingAssets —
    MindAttic.Capture only reads a config file when told to with -capture-config (confirmed by reading
    CaptureUnity.Boot()/CaptureOptions.Apply() in the package; the literal -capture flag is NOT required
    for rule-triggered recording, only -capture-config is). So this passes -capture-config pointing at the
    built player's own copy of capture.json, which still needs "enabled": true to actually record — set
    that before calling this, and back to false afterward, then rebuild so the committed state matches."""
    extra = ("-capture-config", os.path.join(os.path.dirname(EXE), "GridGameGaiden_Data",
                                              "StreamingAssets", "capture.json")) if capture else ()
    launch(run, extra=extra)
    call("speed", x=speed)
    clips = []
    for n in range(count):
        info = call("randombattle")
        if not isinstance(info, dict) or not info.get("ok"):
            print(f"clip{n}: randombattle failed: {info}")
            break
        print(f"clip{n}: {info['source']} -> {info['stage']} lvl{info['level']} {info['enemies']}")
        # ResetTo's fade transition (ScreenRouter.Go) takes ~0.2s+ to actually mount BattleScreen;
        # calling autoplay before that lands hits "not in battle" and silently never turns it on.
        for _ in range(20):
            if call("state")["screen"] == "battle":
                break
            time.sleep(0.2)
        r = call("autoplay", on=1)
        if isinstance(r, dict) and r.get("error"):
            print(f"clip{n}: autoplay failed to engage: {r}")
        t0 = time.time(); last_clock = -1; stuck = 0; result = "TIMEOUT"
        while time.time() - t0 < timeout:
            time.sleep(1.0)
            st = call("state")
            if st["screen"] != "battle":
                result = st["screen"]
                break
            b = st["battle"]
            stuck = stuck + 1 if b["clock"] == last_clock and not b["busy"] else 0
            last_clock = b["clock"]
            if stuck > 12:
                result = "STALLED"
                break
        clips.append({**info, "result": result})
    logs = call("logs")
    print(json.dumps(clips, indent=1))
    print("errors:", [l[:300] for l in logs if l.startswith(("Error", "Exception")) and "first mouse" not in l])
    return clips


def durability(run="durability"):
    """GG-T39: forces the bot's suggested hero to its weapon's last-strike durability (1) mid-battle,
    then verifies the battle card shows the cracked-weapon badge and the next drag preview (the same
    pincer-forming move the bot already found) warns the pincer will shatter it. Skips UI navigation
    (newgame/battle endpoints go straight to a real campaign-stage battle) so it isn't blocked by
    unrelated screens. Screenshots land in artifacts/<run>/."""
    launch(run, fresh=True)
    call("newgame", slot=0, gold=500)
    call("devequipall")  # a fresh roster starts bare-handed; equip Iron Swords so there's a weapon to wear down
    info = call("battle")
    for _ in range(20):
        if call("state")["screen"] == "battle":
            break
        time.sleep(0.2)
    shot("01-battle")
    # The first-battle tutorial periodically overwrites the banner with its own "Hint: ..." text while
    # idle, racing the pincer/shatter banner this check reads — skip it the same way uicheck.py does.
    call("click", name="tutorial-skip"); time.sleep(0.4)
    # A fresh battle's opening move rarely pincers (enemies haven't closed in yet) — play the bot's
    # own suggested moves until one does, same as a player just advancing the fight.
    sug = call("suggest")
    tries = 0
    while isinstance(sug, dict) and sug.get("path") and sug.get("pincers", 0) == 0 and tries < 15 and call("state")["screen"] == "battle":
        pts = ";".join(f"{x},{y}" for x, y in ([tile(*sug["from"])] + [tile(x, y) for x, y in sug["path"]]))
        call("drag", path=pts, step=0.1)
        time.sleep(0.6)
        sug = call("suggest")
        tries += 1
    if not (isinstance(sug, dict) and sug.get("path") and sug.get("pincers", 0) > 0):
        print(json.dumps({"stage": info, "error": "bot found no pincer move to test", "tries": tries, "suggest": sug}, indent=1))
        return
    hero_id = sug["hero"]
    forced = call("devweapon", unit=hero_id, durability=1)
    crack = call("element", name=f"weapon-crack-{hero_id}")
    pts = ";".join(f"{x},{y}" for x, y in ([tile(*sug["from"])] + [tile(x, y) for x, y in sug["path"]]))
    call("drag", path=pts, step=0.1, hold=1)
    time.sleep(0.3)
    banner = call("banner")["text"] or ""
    shot("02-drag-shatter-warning")
    call("release"); time.sleep(0.5)
    shot("03-after-drop")
    logs = call("logs")
    print(json.dumps({
        "stage": info, "forced_durability_1": forced,
        "crack_badge_visible": crack.get("visible"), "crack_badge": crack,
        "banner": banner, "banner_warns_shatter": "shatter" in banner.lower(),
        "errors": [l[:300] for l in logs if l.startswith(("Error", "Exception"))],
    }, indent=1))


def bestiary_drops(run="bestiary"):
    """Plays Green Valley I at high speed (its wave 3 is an all-frog family per the house wave
    convention), then opens the Bestiary and taps the now-Seen Frog entry to screenshot its Drops
    line (GG-T43: every enemy family's signature material, listed once Seen — not just Scanned)."""
    launch(run)
    call("newgame", slot=0)
    time.sleep(1)
    call("speed", x=8)
    call("battle", stage="greenvalley-01")
    for _ in range(20):
        if call("state")["screen"] == "battle":
            break
        time.sleep(0.2)
    call("autoplay", on=1)
    t0 = time.time()
    while time.time() - t0 < 300:
        time.sleep(1.0)
        if call("state")["screen"] != "battle":
            break
    else:
        raise RuntimeError("battle did not finish within 300s even at speed x8")
    time.sleep(1.2)
    call("goto", screen="bestiary")
    time.sleep(0.9)
    # frog00 is always among the first two rows of the (alphabetically-sorted) grid and greenvalley-01's
    # wave 3 always fights it, so its thumb is both Seen and on-screen without needing to scroll.
    r = call("click", name="beast-frog00")
    time.sleep(0.6)
    files = [shot("bestiary-frog-drops")]
    print(json.dumps({"click": r, "layout": call("layout").get("problems")}, indent=1))
    print("\n".join(files))


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "smoke"
    if cmd == "smoke":
        smoke(sys.argv[2] if len(sys.argv) > 2 else "smoke")
    elif cmd == "campaign":
        campaign(stages=int(sys.argv[2]) if len(sys.argv) > 2 else 6)
    elif cmd == "sweep":
        sweep(sys.argv[2] if len(sys.argv) > 2 else "sweep")
    elif cmd == "clips":
        battle_clips(count=int(sys.argv[2]) if len(sys.argv) > 2 else 10)
    elif cmd == "durability":
        durability()
    elif cmd == "bestiary":
        bestiary_drops(sys.argv[2] if len(sys.argv) > 2 else "bestiary")
    elif cmd == "shot":
        print(shot(sys.argv[2]))
    elif cmd == "call":
        kv = dict(a.split("=", 1) for a in sys.argv[3:])
        print(json.dumps(call(sys.argv[2], **kv), indent=1))
