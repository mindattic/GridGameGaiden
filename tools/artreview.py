"""
Art review recorder (owner direction 2026-10-11: "take screenshots and then examine them for artistic integrity and
ways to improve with better effects, sprites or transparency").

    python tools/artreview.py                 # every scenario
    python tools/artreview.py caster poison   # just these

Each scenario stages a moment in a real battle, then records it with the in-game frame recorder (automation "record":
animations step a fixed 1/30 s per rendered frame, so slow PNG writes never skip a beat). Output per scenario:
artifacts/artreview/<scenario>/f###.png plus <scenario>_sheet.png, a numbered contact sheet to review frame by frame.
"""
import os
import subprocess
import sys
import threading
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import harness as h  # noqa: E402

RUN = "artreview"
OUT = os.path.join(h.ROOT, "artifacts", RUN)
SLOT = lambda i: (25 + 108 + 238 * i, 450 + 1440 + 20 + 98)


def st():
    return h.call("state")["battle"]


def units():
    return st()["units"]


def hero(cls):
    return next(u for u in units() if u["team"] == "Hero" and u["cls"] == cls)


def foe():
    return next(u for u in units() if u["team"] == "Enemy")


def tap_unit(u):
    x, y = h.tile(u["x"], u["y"])
    h.call("tap", x=x, y=y)
    time.sleep(0.35)


def record(name, action, frames=72, every=2):
    """Start the recorder, fire the action on the next beat, wait for the frames."""
    result = {}
    t = threading.Thread(target=lambda: result.update(h.call("record", name=name, frames=frames, every=every, timeout=600)))
    t.start()
    time.sleep(0.15)
    action()
    t.join()
    sheet(name)
    return result


def sheet(name):
    d = os.path.join(OUT, name)
    files = sorted(f for f in os.listdir(d) if f.startswith("f") and f.endswith(".png"))
    if not files:
        return
    subprocess.run(["magick", "montage", *[os.path.join(d, f) for f in files], "-tile", "8x", "-geometry", "234x506+4+4",
                    "-label", "%t", "-pointsize", "18", "-background", "#111", "-fill", "white",
                    os.path.join(OUT, f"{name}_sheet.png")], check=False)


def fresh_battle(stage="greenvalley-02"):
    h.call("battle", stage=stage)
    time.sleep(2.5)
    try:
        h.call("click", name="tutorial-skip")
    except Exception:
        pass
    time.sleep(0.4)
    h.call("givemana", n=10)


def aim(hero_cls, slot, entry, target):
    hu = hero(hero_cls)
    h.call("setbar", unit=hu["Id"], slot=slot, entry=entry)
    tap_unit(hu)
    h.call("tap", x=SLOT(slot)[0], y=SLOT(slot)[1])
    time.sleep(0.35)
    if target is not None:
        tap_unit(target)


SCENARIOS = {}


def scenario(fn):
    SCENARIOS[fn.__name__] = fn
    return fn


@scenario
def caster():
    record("caster", lambda: h.call("democaster"), frames=60)


@scenario
def sweep():
    record("sweep", lambda: h.call("democutin"), frames=70)


@scenario
def confirm():
    aim("barbarian", 0, "frost", foe())
    h.shot("confirm_card")


@scenario
def poison():
    aim("barbarian", 0, "poison", foe())
    record("poison", lambda: h.call("click", name="confirm-action"), frames=150, every=3)


@scenario
def fireball():
    aim("barbarian", 0, "fireball", foe())
    record("fireball", lambda: h.call("click", name="confirm-action"), frames=150, every=3)


@scenario
def potion():
    aim("cleric", 1, "item:healing_potion_basic:3", hero("paladin"))
    record("potion", lambda: h.call("click", name="confirm-action"), frames=70)


@scenario
def taunt():
    aim("paladin", 0, "taunt", None)
    record("taunt", lambda: h.call("click", name="confirm-action"), frames=90, every=2)


def main():
    picks = sys.argv[1:] or list(SCENARIOS)
    os.makedirs(OUT, exist_ok=True)
    h.launch(RUN, fresh=True)
    for name in picks:
        h.call("newgame", slot=0, gold=5000)  # resets the router (back in battle opens the pause menu)
        time.sleep(1.5)
        fresh_battle()
        print("recording", name, flush=True)
        SCENARIOS[name]()
    print("sheets in", OUT)


if __name__ == "__main__":
    main()
