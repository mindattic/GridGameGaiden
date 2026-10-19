"""
Self-play harness: launches the development player with the automation endpoint and drives it.

    python tools/harness.py smoke            # title → new game → hub → battle (autoplay) → results
    python tools/harness.py sweep            # screenshot + layout-audit every menu screen
    python tools/harness.py clips N          # generate N random-stage/random-wave battle clips
    python tools/harness.py boardaudit N     # GG-T97: N autoplayed battles; fail if two tokens are ever drawn on one tile
    python tools/harness.py pause            # GG-T126: pause mid-chain freezes playback (two identical frames), resume finishes it
    python tools/harness.py durability       # GG-T39: last-strike weapon badge + drag-preview shatter warning
    python tools/harness.py crucible [WAVES [RUN]]  # GG-T95: camp -> Crucible -> Auto -> leave after WAVES (99 = fight to a wipe) -> results -> camp
    python tools/harness.py stress HOURS     # unattended soak test (GG-T21); default 4h
    python tools/harness.py testmode N       # GG-T93: -testmode self-play, N full battle→results→camp cycles
    python tools/harness.py shot NAME        # screenshot the running player
    python tools/harness.py call PATH [k=v]  # raw endpoint call, e.g. call state

Screenshots land in artifacts/<run>/. Design coordinates are 1170x2532 (top-left origin).
"""
import hashlib
import json
import os
import subprocess
import sys
import time
import urllib.parse
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXE = os.path.join(ROOT, "Build", "Windows", "GridGameGaiden.exe")
# GG-T21: this used to default every worktree to the same fixed port (7787), so two agents running
# harness.py in sibling GridGameGaiden worktrees at once would collide — the second one's launch()
# sees the first's player already listening and calls quit on it, silently ending an unrelated
# agent's run mid-battle with no crash/exception trail (confirmed live: a 5h stress run here died
# after ~5 minutes with a clean Application.Quit() shutdown sequence in Player.log that this script
# never requested). Deriving the default from the worktree's own path keeps every worktree's harness
# on its own port automatically; GAIDEN_PORT still overrides it for anyone who wants a fixed value.
_DEFAULT_PORT = 20000 + (int(hashlib.sha1(ROOT.encode()).hexdigest(), 16) % 20000)
PORT = int(os.environ.get("GAIDEN_PORT", str(_DEFAULT_PORT)))
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


STARTING_PARTY = ("paladin", "barbarian", "cleric", "redmage", "engineer")  # Meta.Progression.StartingRoster


def _wait_screen(screen, timeout=6.0):
    end = time.time() + timeout
    while time.time() < end:
        try:
            if call("state")["screen"] == screen:
                return True
        except Exception:
            pass
        time.sleep(0.25)
    return False


def pick_party(ids=STARTING_PARTY, timeout=10.0):
    """Drives the New Game party-select screen (CharacterSelectScreen, id "charselect") through the real
    UI: tap a hero's tile to preview it, tap it again to lock it in, five times. Picks the fixed starting
    five by default so every run gets the same squad the old fixed-roster New Game gave. Taps can be
    dropped while the player window isn't focused (GG-T121), so each lock-in is verified (a locked tile
    dims to 0.25 opacity) and retried. Returns True once the screen has left for the camp hub."""
    if not _wait_screen("charselect", timeout):
        return False
    time.sleep(0.5)  # let the screen's fade-in finish before the first tap
    for cid in ids:
        name = f"tile-{cid}"
        for attempt in range(8):
            if call("state")["screen"] != "charselect" or call("style", name=name).get("opacity", 1) < 0.5:
                break
            if attempt in (3, 6):  # GG-T16/GG-T121 recovery: recreate the virtual mouse, wait out a focus steal
                if not call("pick", x=10, y=10).get("focused", True):
                    print(f"pick_party: player window unfocused while locking in {cid} (GG-T121), retrying")
                call("resetinput"); time.sleep(1.0)
            call("scrollto", name=name); time.sleep(0.25)  # most of the grid starts below the fold
            call("click", name=name); time.sleep(0.35)
            if call("style", name=name).get("opacity", 1) < 0.5:
                break  # a stale highlight from a dropped tap means the first tap already locked it in
            call("click", name=name); time.sleep(0.45)
    return _wait_screen("hub", timeout)


def finish_dialogue(timeout=40.0):
    """Plays through a DialogueScreen (§27.1, GG-T117) the way a hurried player would: Skip › to the next
    choice, take the last choice offered (by content convention the way out, so a conversation that loops
    back to its menu still ends), repeat until the screen leaves. Taps can drop on an unfocused window
    (GG-T121), so it just keeps tapping until the screen changes or the timeout runs out."""
    end = time.time() + timeout
    while time.time() < end:
        if call("state")["screen"] != "dialogue":
            return True
        choices = sorted(call("names", prefix="choice-"))
        if choices:
            call("click", name=choices[-1])
        else:
            call("click", name="dlg-skip")
        time.sleep(0.7)
    return False


def through_story(files=None, wait=1.5):
    """After launching a stage: skips the theme's story crawl (§27) and plays any story-beat dialogue
    (§27.1) so the caller lands on the battle."""
    if call("state")["screen"] == "crawl":
        if files is not None:
            files.append(shot("04-crawl"))
        call("clicktext", text="Skip"); time.sleep(wait)
    if call("state")["screen"] == "dialogue":
        if files is not None:
            time.sleep(0.8); files.append(shot("04-dialogue"))
        finish_dialogue()
        time.sleep(wait)


def smoke(run="smoke"):
    launch(run, fresh=True)  # fresh: an occupied Slot 1 would stop at the overwrite confirm instead
    _wait_screen("title")
    time.sleep(0.6)  # the title's fade-in swallows a tap sent the moment the server answers
    files = [shot("01-title")]
    _tap_until(lambda: call("clicktext", text="New Game"), lambda: any("Slot 1" in t for t in call("texts")), tries=4)
    _tap_until(lambda: call("clicktext", text="Slot 1"), lambda: call("state")["screen"] == "charselect", tries=4, wait=1.0)
    files.append(shot("02-charselect"))
    pick_party()
    files.append(shot("02-hub"))
    call("clicktext", text="Adventure"); time.sleep(0.8)
    files.append(shot("03-stages"))
    call("clicktext", text="Green Valley I"); time.sleep(1.5)
    through_story(files)
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
                   "alchemist", "talk", "summon", "bounties", "crucible", "bestiary", "settings", "credits"]:
        call("goto", screen=screen)
        time.sleep(0.9)
        shot(f"sweep-{screen}")
        report[screen] = call("layout").get("problems")
        call("back")
        time.sleep(0.6)
    print(json.dumps(report, indent=1))
    logs = call("logs")
    print("errors:", [l for l in logs if l.startswith(("Error", "Exception"))])


def _play_battle(tag, timeout=480):
    """Turns on autoplay and waits for the current battle to leave the battle screen (win or lose).
    Returns ("ended", state) | ("stalled", state) | ("timeout", state)."""
    call("autoplay", on=1)
    t0 = time.time(); last_clock = -1; stuck = 0; shot_taken = False
    while time.time() - t0 < timeout:
        time.sleep(1.0)
        st = call("state")
        if st["screen"] != "battle":
            return "ended", st
        b = st["battle"]
        if not shot_taken and b["clock"] > 60:
            shot(tag); shot_taken = True
        stuck = stuck + 1 if b["clock"] == last_clock and not b["busy"] else 0
        last_clock = b["clock"]
        if stuck > 12:
            return "stalled", b
    return "timeout", call("state")


def _skip_levelups(st):
    for _ in range(8):  # one "Skip" per hero that leveled up (LevelUpScreen.Advance), capped at MaxPartySize
        if st["screen"] != "levelup":
            break
        call("clicktext", text="Skip"); time.sleep(0.7)
        st = call("state")
    return st


def campaign(run="campaign", stages=6, speed=3, extra=(), max_loss_retries=3):
    """Autoplays consecutive stages through the real UI (Next stage / Retry), checking for stalls and errors.
    Pass extra=("-capture", "video", "screen", "5") to record the run (capture.json must have "enabled": true).
    The raw tape grows fast at HighQuality — ~500MB+/minute of real time observed on one run — so a
    continuous capture of more than a handful of stages means a very large .mkv before it's finalized
    into the much smaller .mp4; capture.json's battle-start/battle-end rules (separate per-battle clips)
    are the safer default for anything longer than a short highlight.

    A lost battle inside a multi-battle expedition restarts it from battle 1 by design (GG-T17): that's
    not a harness bug to route around, so on defeat this clicks the real "Retry" button and replays, up
    to max_loss_retries times, before giving up and recording DIFFICULTY_WALL — distinguishing a real
    balance cliff for blind autoplay (loses every attempt) from one-off automation flakiness (wins on retry)."""
    launch(run, extra=extra)
    call("newgame", slot=0)
    time.sleep(1)
    call("speed", x=speed)
    call("clicktext", text="Adventure"); time.sleep(0.8)
    call("clicktext", text="Green Valley I"); time.sleep(1.2)
    through_story(wait=1.2)
    results = []
    for n in range(stages):
        outcome, st = _play_battle(f"stage{n}-mid")
        if outcome != "ended":
            results.append({"stage": n, "result": outcome.upper(), "state": st})
            break

        time.sleep(1.2)
        st = _skip_levelups(call("state"))

        # Win or lose, PostBattleScreen always renders a "Continue" button; only a loss also renders
        # "Retry". Probing for it both tells win from loss and, on a loss, is the retry tap itself.
        gave_up = False
        for attempt in range(1, max_loss_retries + 1):
            r = call("clicktext", text="Retry")
            if isinstance(r, dict) and r.get("error"):
                break  # no "Retry" button visible: this was a win
            print(f"stage{n}: lost, retrying ({attempt}/{max_loss_retries})")
            time.sleep(1.5)
            for _ in range(20):
                if call("state")["screen"] == "battle":
                    break
                time.sleep(0.2)
            outcome, st = _play_battle(f"stage{n}-retry{attempt}-mid")
            if outcome != "ended":
                results.append({"stage": n, "result": outcome.upper(), "state": st, "after_retries": attempt})
                gave_up = True
                break
            time.sleep(1.2)
            st = _skip_levelups(call("state"))
        else:
            results.append({"stage": n, "result": "DIFFICULTY_WALL", "retries": max_loss_retries})
            print(f"stage{n}: lost {max_loss_retries} times running — real difficulty cliff, not automation flakiness, stopping")
            gave_up = True
        if gave_up:
            break

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
        through_story(wait=1.2)
    logs = call("logs")
    print(json.dumps(results, indent=1))
    print("errors:", [l[:300] for l in logs if l.startswith(("Error", "Exception")) and "first mouse" not in l])


def battle_clips(run="clips", count=10, speed=3, timeout=240, capture=True, relaunch=True):
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
    if relaunch:
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


def board_audit(run="boardaudit", count=12, speed=4, timeout=300):
    """GG-T97: plays `count` random real battles on autoplay (randombattle, no capture) and fails unless the drawn
    board stayed honest the whole time: after every beat no two tokens on one tile, after every action every token
    on exactly its unit's State tile (before the SyncAll safety net could hide a miss), and the engine's last-resort
    tile-conflict net never fired. Reads BoardAudit through the boardaudit endpoint. Returns True when clean."""
    launch(run)
    call("boardaudit", reset=1)
    clips = battle_clips(run=run, count=count, speed=speed, timeout=timeout, capture=False, relaunch=False)
    audit = call("boardaudit")["audit"]
    print(json.dumps({"battles": len(clips), "audit": audit}, indent=1))
    bad = audit["beat_overlaps"] + audit["settle_drift"] + audit["synced"] + audit["tile_conflicts"]
    if audit["actions"] == 0:
        print("FAIL: no actions were audited")
        return False
    if bad:
        print(f"FAIL: {bad} board audit problem(s)")
        return False
    print(f"OK: {audit['actions']} actions / {audit['beats']} beats, every token on its own tile")
    return True


def _frame_diff(a, b):
    """Fraction of pixels that differ between two screenshots (0.0 = identical frames)."""
    from PIL import Image, ImageChops
    ia, ib = Image.open(a).convert("RGB"), Image.open(b).convert("RGB")
    if ia.size != ib.size:
        return 1.0
    diff = ImageChops.difference(ia, ib).convert("L").point(lambda v: 255 if v > 8 else 0)
    return (diff.histogram()[255]) / float(ia.size[0] * ia.size[1])


def pause_check(run="pause", speed=1, timeout=240):
    """GG-T126: Pause must pause. Autoplays random battles until playback is mid-pincer (outline strokes drawn,
    chain >= 1; a CHAIN x2 is preferred for the first 90 s), taps the real "II" menu button, then screenshots twice
    ~2 s apart: no playback beat may start (beats / beatsWhilePaused), the outline, clock and frame must be
    identical, and the pause veil must be the topmost Overlay layer. Then taps Resume and checks the chain plays
    on to the end. Returns True when every check passes."""
    art = launch(run)
    call("speed", x=speed)
    t0 = time.time(); caught = None
    while time.time() - t0 < timeout and caught is None:
        call("randombattle"); time.sleep(1.0)
        call("autoplay", on=1)
        t1 = time.time()
        while time.time() - t1 < 90:
            st = call("state")
            b = st.get("battle")
            if st["screen"] != "battle" or b is None:
                break
            want = 2 if time.time() - t0 < 90 else 1
            if b["busy"] and b["chain"] >= want and b["lines"] > 0:
                caught = b
                break
            time.sleep(0.03)
    if caught is None:
        print("FAIL: never caught playback mid-pincer")
        return False
    for _ in range(8):  # GG-T121: a synthetic tap can drop on an unfocused window
        call("click", name="menu"); time.sleep(0.3)
        if call("state")["battle"]["paused"]:
            break
    s1 = call("state")["battle"]
    a = shot("paused-a")
    time.sleep(2.0)
    s2 = call("state")["battle"]
    b2 = shot("paused-b")
    diff = _frame_diff(a, b2)
    checks = {
        "paused": s1["paused"] and s2["paused"],
        "paused mid-chain": s1["busy"] and s1["chain"] >= 1 and s1["lines"] > 0,
        "no beat advanced": s1["beats"] == s2["beats"] and s2["beatsWhilePaused"] == 0,
        "outline frozen": s1["lines"] == s2["lines"],
        "clock frozen": s1["clock"] == s2["clock"] and s1["bank"] == s2["bank"],
        "veil on top": s1["pauseOnTop"] and s2["pauseOnTop"],
        "frames identical": diff == 0.0,
    }
    for _ in range(8):
        call("clicktext", text="Resume"); time.sleep(0.3)
        if not call("state")["battle"]["paused"]:
            break
    resumed = call("state")["battle"]
    checks["resumed"] = not resumed["paused"]
    end = time.time() + 30
    after = resumed
    while time.time() < end:
        st = call("state")
        if st["screen"] != "battle":
            after = None
            break
        after = st["battle"]
        # The paused event list has played out once beats moved on and either playback went idle or the bot
        # already committed its next command (the state clock only changes on a new command).
        if after["beats"] > s2["beats"] and (not after["busy"] or after["clock"] != s2["clock"]):
            break
        time.sleep(0.2)
    checks["chain finished after resume"] = after is None or (after["beats"] > s2["beats"] and (not after["busy"] or after["clock"] != s2["clock"]))
    shot("resumed")
    logs = call("logs")
    pause_errors = [l[:300] for l in logs if "[Pause]" in l or "watchdog" in l]
    checks["no pause/watchdog errors"] = not pause_errors
    print(json.dumps({"caught": {k: caught[k] for k in ("chain", "lines", "beats")}, "paused_a": {k: s1[k] for k in ("beats", "lines", "clock", "chain")},
                      "paused_b": {k: s2[k] for k in ("beats", "lines", "clock", "chain")}, "frame_diff": diff,
                      "after": after and {k: after[k] for k in ("beats", "busy", "clock")}, "errors": pause_errors, "checks": checks}, indent=1))
    print(a); print(b2)
    return all(checks.values())


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


def testmode(run="testmode", cycles=4, timeout_per_cycle=600):
    """GG-T93: launches the player with -testmode (bot-only: throwaway save, no story/crawl/tutorial) and
    lets it drive itself through `cycles` full launch → battle → results → camp loops. Screenshots each
    cycle's battle, results and camp (the Hub after CampRoutine ran), collects the one-line-per-cycle
    "[TestMode] cycle N | ..." log into artifacts/<run>/testmode.log, and checks the evidence: every cycle
    logged, a battle/results/hub screen seen per cycle, the party level never falling, an ability learned
    in any run of 6+ cycles (GG-T100), and no errors."""
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # camp lines carry ★ (masterwork) glyphs
    art = launch(run, extra=("-testmode",), fresh=True)
    lines, errors, seen = [], [], []
    shots_for = {}  # (cycle, screen) -> file
    last_screen = None
    t0 = time.time()
    done = 0
    while done < cycles and time.time() - t0 < timeout_per_cycle * cycles:
        time.sleep(0.5)
        try:
            st = call("state", timeout=10)
        except Exception as e:
            errors.append(f"state failed: {e}")
            if not alive():
                break
            continue
        tm = st.get("testmode") or {}
        done = tm.get("cycles", 0)
        scr = st["screen"]
        cyc = done + 1
        if scr != last_screen:
            seen.append((cyc, scr))
            last_screen = scr
        key = (cyc, scr)
        if scr in ("postbattle", "levelup") and key not in shots_for:
            shots_for[key] = shot(f"c{cyc:02d}-{scr}")
        if scr == "battle" and key not in shots_for and st.get("battle", {}).get("clock", 0) > 30:
            shots_for[key] = shot(f"c{cyc:02d}-battle")
        logs = call("logs", clear=1)
        lines += [l.split(": ", 1)[1] for l in logs if "[TestMode]" in l]
        errors += [l[:300] for l in logs if l.startswith(("Error", "Exception")) and "first mouse" not in l]
        if done >= 1 and scr == "hub" and (done, "hub") not in shots_for:
            shots_for[(done, "hub")] = shot(f"c{done:02d}-camp")
    cycle_lines = [l for l in lines if l.startswith("[TestMode] cycle") and " launch " not in l]
    with open(os.path.join(art, "testmode.log"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    levels = []
    for l in cycle_lines:
        part = l.split("| party Lv ", 1)[1].split(" ", 1)[0]
        levels.append(tuple(int(x) for x in part.split("->")))
    per_cycle = {n: {s for c, s in seen if c == n} for n in range(1, done + 1)}
    checks = {
        "cycles_logged": len(cycle_lines) >= cycles,
        "battle_results_camp_each_cycle": all({"battle", "postbattle", "hub"} <= per_cycle.get(n, set()) for n in range(1, cycles + 1)),
        "party_level_never_falls": all(b >= a for a, b in levels) and all(levels[i + 1][0] >= levels[i][1] for i in range(len(levels) - 1)),
        "camp_did_something": any(": nothing to do" not in l for l in cycle_lines),
        # GG-T100: "learn abilities" is part of what Test Mode must prove — a run of 6+ cycles that never
        # learned one fails (CampRoutine logs "<Hero> learned <Ability> (<price>g)"; TestLoop.CyclesThatMustLearnAnAbility).
        "learned_an_ability": len(cycle_lines) < 6 or any(" learned " in l for l in cycle_lines),
        "no_errors": not errors,
    }
    try:
        call("quit", timeout=3)
    except Exception:
        pass
    print("\n".join(cycle_lines))
    print(json.dumps({"checks": checks, "errors": errors[:10], "screens": seen[:60]}, indent=1))
    print("\n".join(sorted(shots_for.values())))
    return all(checks.values())

def _tap_until(tap, done, tries=8, wait=0.8):
    """Taps until `done()` holds. A synthetic tap is occasionally dropped (the player window losing focus to
    another agent's window on a shared desktop is the usual suspect), so a flow that must advance retries the
    tap rather than failing the run. Returns how many taps it took (0 = never got there)."""
    for i in range(tries):
        tap(); time.sleep(wait)
        if done():
            return i + 1
    return 0


def crucible(run="crucible", leave_after=3, speed=4, timeout=600):
    """GG-T95: the Crucible end to end through the real UI. Camp -> the Crucible row -> Enter -> the battle on
    Auto until `leave_after` waves are cleared (or the party wipes first) -> pause menu "Leave the Crucible" ->
    level-ups -> results -> Continue -> camp, checking the haul really landed in the save (gold, items, XP),
    the party recovered to full and the camp row now shows the best record. Screenshots in artifacts/<run>/."""
    launch(run, fresh=True)
    call("newgame", slot=0)
    time.sleep(1.5)
    call("cues", clear=1)
    before = call("state")["save"]
    out = {"before": {k: before[k] for k in ("Gold", "inventory", "xp", "crucible", "HighestClearedStage")}, "taps": {}}
    screen = lambda: call("state")["screen"]
    texts = lambda: [t for t in call("texts") if isinstance(t, str)]
    shot("01-camp")
    out["camp_row_before"] = [t for t in texts() if t in ("Crucible", "Endless waves") or t.startswith("Best:")]
    out["taps"]["camp->crucible"] = _tap_until(lambda: call("clicktext", text="Crucible"), lambda: screen() == "crucible")
    shot("02-crucible-screen")
    out["taps"]["enter"] = _tap_until(lambda: call("click", name="crucible-enter"), lambda: screen() == "battle", wait=1.5)
    call("speed", x=speed)
    call("autoplay", on=1)
    t0 = time.time(); ended_by = "timeout"; last_wave = -1
    while time.time() - t0 < timeout:
        time.sleep(1.0)
        st = call("state")
        if st["screen"] != "battle":
            ended_by = "wipe"
            break
        b = st["battle"]
        if b["wave"] != last_wave:
            last_wave = b["wave"]
            time.sleep(0.4)  # let the wave banner land before the shot
            shot(f"03-battle-wave{b['wave'] + 1:02d}")
        if b["wave"] >= leave_after:
            # Leave the way a player would: Auto off, let the current action finish, pause menu, confirm.
            call("autoplay", on=0)
            for _ in range(80):
                if not call("state").get("battle", {}).get("busy", True):
                    break
                time.sleep(0.25)
            out["taps"]["pause"] = _tap_until(lambda: call("click", name="menu"), lambda: any("Leave the Crucible" in t for t in texts()))
            shot("04-pause-menu")
            out["taps"]["leave-row"] = _tap_until(lambda: call("clicktext", text="Leave the Crucible"), lambda: any(t.startswith("Leave the Crucible after") for t in texts()))
            shot("05-leave-confirm")
            out["taps"]["leave-confirm"] = _tap_until(lambda: call("clicktext", text="Leave"), lambda: screen() != "battle", wait=1.5)
            ended_by = "left"
            break
    out["ended_by"] = ended_by
    out["waves_reached"] = last_wave + 1
    time.sleep(1.0)
    st = call("state")
    if st["screen"] == "levelup":
        shot("06-levelup")
    for _ in range(8):  # one "Skip" per hero that leveled up
        if screen() != "levelup":
            break
        _tap_until(lambda: call("clicktext", text="Skip"), lambda: True, tries=1, wait=0.8)
    time.sleep(1.5)
    out["results_screen"] = screen()
    shot("07-results")
    out["results_texts"] = texts()[:40]
    after = call("state")["save"]
    out["taps"]["continue"] = _tap_until(lambda: call("click", name="continue"), lambda: screen() == "hub", wait=1.2)
    out["back_at"] = screen()
    shot("08-camp-after")
    out["after"] = {k: after[k] for k in ("Gold", "inventory", "xp", "hp", "crucible", "HighestClearedStage", "expedition")}
    gained = {k: v - before["inventory"].get(k, 0) for k, v in after["inventory"].items() if v != before["inventory"].get(k, 0)}
    out["checks"] = {
        "gold_gained": after["Gold"] - before["Gold"],
        "items_changed": gained,
        "xp_gained": {k: after["xp"][k] - before["xp"].get(k, 0) for k in after["xp"]},
        "party_full_hp": all(after["hp"][k] == 0 for k in before["party"]),
        "best_recorded": after["crucible"]["Best"],
        "campaign_untouched": after["HighestClearedStage"] == before["HighestClearedStage"] and after["expedition"] is None,
        "camp_row_after": [t for t in texts() if t == "Crucible" or t.startswith("Best:")],
        "crucible_cues": sorted({c for c in call("cues") if c.startswith("crucible")}),
    }
    # A dropped tap (0 in "taps") is almost always the window losing focus to another player on a shared
    # desktop: UI Toolkit ignores pointer input while unfocused, and `pick` reports it.
    out["focused_at_end"] = call("pick", x=0, y=0).get("focused")
    logs = call("logs")
    out["errors"] = [l[:300] for l in logs if l.startswith(("Error", "Exception"))]
    print(json.dumps(out, indent=1))


def _report_death(art, returncode, n, extra_note=""):
    """GG-T21: the player process can vanish mid-battle with no exception in Player.log and no
    Windows "Application Error" event — the only report from that incident was a human skimming
    Task Manager afterward. This captures the OS-level evidence immediately, while it's still fresh:
    CrashDumps (WER only writes these for crashes it catches, which this one evidently wasn't) and
    the Application event log window around the death, so a recurrence leaves an actual trail."""
    report = os.path.join(art, "crash-report.txt")
    lines = [f"process died after {n} clips (returncode={returncode}) at {time.strftime('%Y-%m-%d %H:%M:%S')}", extra_note]
    dumps_dir = os.path.join(os.environ.get("LOCALAPPDATA", ""), "CrashDumps")
    try:
        names = [f for f in os.listdir(dumps_dir) if "GridGameGaiden" in f]
        lines.append(f"CrashDumps matching GridGameGaiden: {names or 'none'}")
    except Exception as e:
        lines.append(f"could not list CrashDumps: {e}")
    try:
        ps = subprocess.run(
            ["powershell", "-NoProfile", "-Command",
             "Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-15)} "
             "-ErrorAction SilentlyContinue | Select-Object TimeCreated,ProviderName,Id,LevelDisplayName,Message "
             "| Format-List | Out-String -Width 200"],
            capture_output=True, text=True, timeout=30)
        lines.append("Application event log, last 15 minutes:")
        lines.append(ps.stdout.strip() or "(no matching events)")
        if ps.stderr.strip():
            lines.append(f"(stderr: {ps.stderr.strip()})")
    except Exception as e:
        lines.append(f"could not query event log: {e}")
    log_path = os.path.join(art, "player.log")
    if os.path.exists(log_path):
        with open(log_path, "r", errors="replace") as f:
            tail = f.readlines()[-40:]
        lines.append("Player.log tail:")
        lines.append("".join(tail))
    text = "\n".join(lines)
    with open(report, "w") as f:
        f.write(text)
    print(text)
    print(f"\nfull report: {report}")


def stress(run="stress", hours=4.0, speed=3, timeout=240):
    """GG-T21 soak test: launches once and keeps generating random battle clips (same approach as
    battle_clips) for `hours` of wall-clock time in one long-lived process, to try to reproduce the
    process disappearing mid-battle during an earlier ~1h unattended run with no diagnostic trail.
    Unlike battle_clips/campaign, this polls the OS process directly (Popen.poll()) on every
    iteration instead of only noticing trouble when an HTTP call fails or times out, since the
    original incident was the whole process vanishing, not a hang. On a process death it writes
    artifacts/<run>/crash-report.txt with whatever OS-level evidence it can still gather."""
    art = os.path.join(ROOT, "artifacts", run)
    saves = os.path.join(art, "saves")
    os.makedirs(saves, exist_ok=True)
    args = [EXE, "-automation", str(PORT), "-artifacts", art, "-saveRoot", saves,
            "-screen-width", "585", "-screen-height", "1266", "-screen-fullscreen", "0",
            "-logFile", os.path.join(art, "player.log")]
    proc = subprocess.Popen(args, cwd=ROOT)
    for _ in range(60):
        if alive():
            break
        time.sleep(0.5)
    else:
        raise RuntimeError("player did not start")

    class Died(Exception):
        pass

    def checked_call(endpoint, **params):
        # Every call() in this loop goes through here so a process death is always caught and
        # reported, instead of the exception from a refused connection escaping uncaught (as it
        # did the first time this ran: the process died between a poll() check and the next call()
        # one line later, and only the calls adjacent to an explicit poll() check were guarded).
        if proc.poll() is not None:
            raise Died()
        try:
            return call(endpoint, **params)
        except Exception as e:
            if proc.poll() is not None:
                raise Died(str(e))
            raise

    checked_call("speed", x=speed)
    deadline = time.time() + hours * 3600
    clips = []
    n = 0
    try:
        while time.time() < deadline:
            info = checked_call("randombattle")
            if not isinstance(info, dict) or not info.get("ok"):
                print(f"clip{n}: randombattle failed: {info}")
                break
            for _ in range(20):
                if checked_call("state")["screen"] == "battle":
                    break
                time.sleep(0.2)
            checked_call("autoplay", on=1)
            t0 = time.time(); last_clock = -1; stuck = 0; result = "TIMEOUT"
            while time.time() - t0 < timeout:
                time.sleep(1.0)
                st = checked_call("state")
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
            n += 1
            if n % 10 == 0:
                elapsed = time.time() - (deadline - hours * 3600)
                print(f"stress: {n} clips done, {elapsed/3600:.2f}h elapsed, process alive")
    except Died as e:
        _report_death(art, proc.returncode, n, extra_note=str(e))
        return clips
    logs = call("logs")
    errors = [l[:300] for l in logs if l.startswith(("Error", "Exception")) and "first mouse" not in l]
    print(json.dumps({"clips_completed": n, "hours": hours, "errors": errors}, indent=1))
    return clips


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
    elif cmd == "boardaudit":
        sys.exit(0 if board_audit(count=int(sys.argv[2]) if len(sys.argv) > 2 else 12) else 1)
    elif cmd == "durability":
        durability()
    elif cmd == "pause":
        sys.exit(0 if pause_check() else 1)
    elif cmd == "crucible":
        crucible(sys.argv[3] if len(sys.argv) > 3 else "crucible", leave_after=int(sys.argv[2]) if len(sys.argv) > 2 else 3)
    elif cmd == "bestiary":
        bestiary_drops(sys.argv[2] if len(sys.argv) > 2 else "bestiary")
    elif cmd == "stress":
        stress(hours=float(sys.argv[2]) if len(sys.argv) > 2 else 4.0)
    elif cmd == "testmode":
        sys.exit(0 if testmode(cycles=int(sys.argv[2]) if len(sys.argv) > 2 else 4) else 1)
    elif cmd == "shot":
        print(shot(sys.argv[2]))
    elif cmd == "call":
        kv = dict(a.split("=", 1) for a in sys.argv[3:])
        print(json.dumps(call(sys.argv[2], **kv), indent=1))
