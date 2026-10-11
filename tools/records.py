"""
Battle-record analysis (bible §22.6). Reads the JSON battle records the game and the bot soak write and prints the
balance picture: where battles are lost, what each wave costs, what each role contributes, how mana flows.

    python tools/records.py [dir-or-files…]        # default: artifacts/records
    python tools/records.py --source bot:greedy    # only one kind of player

Record schema v1 (one document per battle): schema, source, stage, waves, seed, party[], opening{state,events},
turns[{index, wave, before{state}, command, accepted, events[], after{…}}], outcome{phase, waveReached, actions, …}.
"""
import collections
import glob
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load(paths, source=None):
    files = []
    for p in paths:
        files += sorted(glob.glob(os.path.join(p, "*.json"))) if os.path.isdir(p) else [p]
    recs = []
    for f in files:
        with open(f, encoding="utf-8") as fh:
            r = json.load(fh)
        if source and r.get("source") != source:
            continue
        recs.append(r)
    return recs


def analyse(recs):
    by_source = collections.defaultdict(list)
    for r in recs:
        by_source[r["source"]].append(r)
    for source, rs in by_source.items():
        print(f"\n=== {source}: {len(rs)} battles · stage {rs[0]['stage']} · {rs[0]['waves']} waves ===")
        wins = sum(1 for r in rs if r["outcome"]["phase"] == "Victory")
        reached = collections.Counter(r["outcome"]["waveReached"] for r in rs if r["outcome"]["phase"] != "Victory")
        print(f"victories {wins}/{len(rs)} · avg actions {sum(r['outcome']['actions'] for r in rs) / len(rs):.0f} · lost at waves {dict(sorted(reached.items()))}")

        wave = collections.defaultdict(lambda: collections.Counter())
        roles = collections.defaultdict(lambda: collections.Counter())
        brains = collections.Counter()
        abilities = collections.Counter()
        for r in rs:
            role_of = {h["id"]: h["role"] for h in r["party"]}
            for t in r["turns"]:
                w = (t.get("wave") or 0) + 1
                units = {u["id"]: u for u in t["before"]["units"]}
                c = wave[w]
                c["turns"] += 1
                if t["command"]["type"] == "use":
                    hero = units.get(t["command"]["hero"])
                    slot = hero["bar"][t["command"]["slot"]] if hero and t["command"]["slot"] < len(hero.get("bar", [])) else None
                    name = (slot or {}).get("ability") or (slot or {}).get("item") or "?"
                    abilities[(role_of.get(t["command"]["hero"], "?"), name)] += 1
                    if (slot or {}).get("item"):
                        c["potions"] += 1
                if t["command"]["type"] == "guard":
                    c["guards"] += 1
                for e in t["events"]:
                    ty = e["type"]
                    if ty == "Damaged":
                        tgt, src = units.get(e["TargetId"]), units.get(e["SourceId"])
                        if tgt and tgt["team"] == "Hero":
                            c["heroDmgTaken"] += e["Amount"]
                            if src and src["team"] == "Enemy":
                                brains[src.get("brain", "?")] += e["Amount"]
                        elif src and src["team"] == "Hero":
                            roles[role_of.get(src["id"], "?")]["damage"] += e["Amount"]
                    elif ty == "Healed":
                        src = units.get(e["SourceId"])
                        if src and src["team"] == "Hero":
                            roles[role_of.get(src["id"], "?")]["healing"] += e["Amount"]
                    elif ty == "Died":
                        c["heroDeaths" if e["Team"] == "Hero" else "kills"] += 1
                    elif ty == "ManaCharged":
                        c["charge"] += e["Amount"]
                    elif ty == "OrbMinted" and e.get("Landed"):
                        c["orbs"] += 1
                    elif ty == "OrbsSpent":
                        c["orbsSpent"] += e["Count"]
                    elif ty == "PincerFormed" and e["Team"] == "Hero":
                        c["pincers"] += 1
                        c["chains"] += 1 if e["ChainIndex"] > 0 else 0
                    elif ty == "PickupCollected":
                        c["pickups"] += 1
                    elif ty == "BuffApplied" and e["BuffId"] == "taunting":
                        c["taunts"] += 1
        n = len(rs)
        print("wave  turns  dmgTaken  deaths  potions  pincers chains  charge orbs spent  pickups taunts guards")
        for w in sorted(wave):
            c = wave[w]
            print(f"{w:4} {c['turns'] / n:6.1f} {c['heroDmgTaken'] / n:9.0f} {c['heroDeaths'] / n:7.2f} {c['potions'] / n:8.2f} "
                  f"{c['pincers'] / n:8.1f} {c['chains'] / n:6.1f} {c['charge'] / n:7.0f} {c['orbs'] / n:4.1f} {c['orbsSpent'] / n:5.1f} "
                  f"{c['pickups'] / n:8.2f} {c['taunts'] / n:6.2f} {c['guards'] / n:6.1f}")
        print("role contribution per battle:", {k: {kk: round(vv / n) for kk, vv in v.items()} for k, v in roles.items()})
        print("damage taken by enemy brain per battle:", {k: round(v / n) for k, v in brains.most_common()})
        print("most used:", [(f"{r}:{a}", round(k / n, 1)) for (r, a), k in abilities.most_common(12)])


def main():
    args = sys.argv[1:]
    source = None
    if "--source" in args:
        i = args.index("--source"); source = args[i + 1]; args = args[:i] + args[i + 2:]
    paths = args or [os.path.join(ROOT, "artifacts", "records")]
    recs = load(paths, source)
    if not recs:
        print("no records found in", paths)
        return
    analyse(recs)


if __name__ == "__main__":
    main()
