---
status: draft — pitch for owner review
date: 2026-10-18
purpose: RFC for three net-new systems the owner described (overworld, dialogue/branching, mid-battle
  recruitment) that currently contradict BIBLE.md §27 "Story crawl (no dialog)" and §28 "No Overworld".
  Nothing here is built or amended yet — this is the "say so and ask" required by GG-LAW-7 before any
  of it lands in code or in the bible. Supersedes nothing in docs/story-draft-spider-town.md; merges
  with it per owner direction (2026-10-18).
---

# Overworld, dialogue and battle recruitment — design pitch

## 0. What's already true, so this isn't a rewrite

The loop the owner described — pick heroes, gear them, fight a 5-wave stage, come home with loot,
spend it at Camp on upgrades/repair/rest, go back out — **is the existing game.** `§22 Macro Loop` and
`§22.5 Expeditions` already do this: 3 battles per theme in sequence, mana and consumables deliberately
scarce across them, wounds carrying, the caravan's shops always open. Nothing below touches that core
loop. What follows is three additions **around** it:

1. **An overworld** — a navigable "descend from the walking town" layer between Camp and a stage,
   replacing or dressing up the flat Adventure list.
2. **A dialogue/conversation system** — portrait boxes, full scripted conversations, branching choices,
   used for story beats and for recruitment.
3. **Mid-battle recruitment** — Fire Emblem's "walk next to them, talk, meet their condition, they
   join" on the actual grid.

Items 1 and 2 directly reverse `§28` and `§27`. They are deliberate reversals the owner is asking for,
not oversights — but per `GG-LAW-7` they need to be written down and agreed before `BattleEngine`,
`ContentLoader` or the screen router change shape around them.

## 0.1 Modes (owner, 2026-10-19)

- **Campaign** — the full game: the macro loop above plus the story cutscenes/dialogue (§3) and
  exploration (§2) this pitch proposes. Today's New Game/Continue is its mechanical core.
- **Crucible** (ticket GG-T95) — a "waiting for the bus" endless survival battle reached from Camp
  inside a saved campaign: escalating waves until wiped or retreating, and the XP and loot it earns
  are kept by the campaign save. Independent of this pitch; buildable now.
- **Test Mode** (ticket GG-T93) — bot-only `-testmode`: plays the mechanical loop (battle → XP/mats →
  camp: repair, rest, buy, learn, equip → battle) with no story or exploration, and enemy level
  follows party level so the loop gets harder as it goes. Independent of this pitch; buildable now.

## 1. Narrative merge: biomechanical plague + the Longmother

**Landed 2026-10-19 (GG-T120):** this merge is now the shipped story. Every `campaign.json` theme's
`Crawl` carries it (the five main themes plus the five postgame themes, which the merge's "she can't
recross woken ground, so her route curls" framing now explains), and the NPC/hero join scenes plus a
new `*_party_talk` beat per theme in `dialogues.json` thread the magic-vs-machine and
built-not-grown/buried threads through actual dialogue. See `docs/BIBLE.md` §27.1 for the summary and
pointers. The mechanical depth question in §6.3 below is still open — this pass is narrative/flavor
only, no new stat system.

Per owner direction, this **merges with** `docs/story-draft-spider-town.md` rather than replacing it.
Keep from that draft: the town is built on something that walks (placeholder "the Longmother"), it
moves because standing still is fatal, the party are vanguard crews dropping down at each step to clear
what the footfall disturbed and salvage what it turned up. Pincers-as-copied-from-a-brood and
gear-grown-not-mined both still work unchanged.

What the new premise swaps in: **the thing souring the ground is not a passive blight, it's a
biomechanical plague** — monsters infected with it are tougher, stranger, half-grown-half-built. Its
second-order effect is the hook the owner wants threaded through everything: **classical magic is
becoming unreliable wherever the plague has spread**, because magic and the plague interfere with each
other, while **machines don't** — electricity doesn't care about the same interference an Orb of Sight
does, and an orb can fizzle mid-cast in exactly the cave the party is standing in. That's why the town
is leaning harder on machine fuel and salvage every theme: not flavor text, a mechanical and economic
pressure. Concretely, this gives three load-bearing threads:

- **Why the party descends at all** — the existing "vanguard crews clear + salvage" framing holds, now
  salvaging fuel/materials for a town whose magical infrastructure is failing, not just forged gear.
- **Why machines are creeping into a magic game** — reframes `summon.json`'s "Summon Circle" and future
  equipment/consumables as a spectrum from pure-arcane (unreliable, powerful, plague-adjacent zones
  punish it) to mechanical (reliable, duller, available everywhere) rather than one undifferentiated
  item pool. This is a strong content hook but **not a prerequisite** for the overworld/dialogue/
  recruitment systems below — it can land later as its own content pass (new tag on `ItemDef`/
  `AbilityDef`, a zone-level "plague saturation" multiplier on spell fizzle chance) without blocking them.
- **Why recruitable NPCs exist mid-battle** — survivors, deserters, or vanguard crews from other
  caravans the party finds already fighting the plague's tide; this is the natural diegetic reason a
  stray unit stands on the grid asking to be convinced rather than just another enemy.

Placeholder naming carries over unresolved from the Longmother draft (town name, the plague's name,
whether it has a singular "author" the way the Demon Lord does) — not re-litigated here, see Open
Questions.

## 2. The overworld — undecided, being prototyped in GridGameCaravan

**Status (owner, 2026-10-19): still up for debate.** The traversal layer is being prototyped in a
separate repo, `D:\Projects\MindAttic\GridGameCaravan` ("traversal lab"), before anything is folded
into Gaiden. Its premise already matches §1: towns walk on giant legs because the surface is overrun,
and ground teams disembark to fight and haul fuel/ore home. Every Caravan demo plays the same
`Lab.Core` loop — scan → choose a site → walk the town and/or send the ground team by bike → battle
(placeholder) → haul resources home — so the five candidates compare on feel, time-to-battle and taps
per decision:

| Caravan demo | Idea |
|---|---|
| Route Map | Fogged node graph (FTL / Slay the Spire); walk the town along roads or send the bike |
| Horizon Strip | Side view; the walker strides across parallax landscapes |
| **Isometric Expanse** | 2.5D tap-to-walk over hilly iso terrain — **the isometric walking city the owner is currently considering** |
| 3D Low-Poly Walker | Procedural terrain and an IK-legged walker |
| Radar Sectors | Real-time hex fog grid |

This pitch does **not** pick one. The earlier node-graph proposal is just the Route Map option and is
withdrawn as a recommendation. What holds whichever demo wins:

- **Battles stay the game; traversal is a quick beat between them** (Caravan's own rule). The overworld
  ends in a `BattleFactory.Build` call exactly as Adventure does today; `BattleEngine`/`BattleRunner` don't
  change.
- **The unlock rules stay underneath.** `Progression`, `HighestClearedStage`/`IsStageUnlocked` and
  expedition sequencing (`§22.5`) remain the source of truth; the overworld presents them, it doesn't
  replace them. Owner call needed if the winning demo implies free choice of site order, which loosens
  linear unlock.
- **Porting follows Gaiden's laws.** Caravan's `Lab.Core` (world, town, routing, hex) is already
  engine-free and code-built, so it should port into `Gaiden.Meta` (`GG-LAW-9`), with sites/routes as
  validated content JSON (`GG-LAW-10`). The walker rig (`WalkerRig2D`, Painter2D) moves to `Gaiden.App`.
  An isometric or 3D view is new rendering work for Gaiden, which is all UI Toolkit screens today;
  Isometric Expanse would be Gaiden's first non-menu, non-grid world view.
- **Site types beyond Battle** carry over from the owner's ask: a cache (loot, no fight), an event
  (dialogue scene, §3), a fuel/scavenge hunt (short combat).

### 2.1 Terrain gates — what the town can't walk (owner, 2026-10-19)

The walking town is the story gate. At first it **can't cross water**; later it **can't cross lava**;
each barrier is lifted by an upgrade to the town itself (fording legs, heat plating, and so on), built
from what the ground teams salvage. That's the diegetic reason the party moves and explores: the town
needs a specific material or machine part to get past the next barrier, so the expedition ahead is
"go find it," not an arbitrary unlock. It ties three existing threads together:

- **Story.** Each barrier is a chapter break. The plague-and-machines premise (§1) explains why the fix
  is engineering rather than a spell: a ward that lets the town wade a river might fizzle halfway
  across, but a pump and sealed joints won't.
- **Economy.** Town upgrades become the big gold-and-materials sink above Camp's per-hero spending,
  so the loot loop (and Crucible farming, GG-T95) has a long-term goal.
- **Progression.** Gates can map onto the existing theme order (e.g. a river before Mireholt Fens,
  lava before a volcanic theme) so `HighestClearedStage` gating keeps working underneath; the barrier
  is the presentation of the unlock, plus a material check. It's the same idea as Metroidvania ability
  gates, applied to the hub.

This holds for any Caravan demo: a node graph shows it as impassable edges, Isometric Expanse as
impassable terrain the town paths around. Caravan's `Lab.Core` already models town routing, so that's where
impassable terrain would be prototyped first.

Until Caravan settles, §3 and §4 are designed not to depend on the overworld at all.

## 3. Dialogue / conversation system

The ask: portraits, overlapping dialogue boxes, full conversations, branching choices, used for story
events and for recruitment (§4).

- **New content:** `dialogues.json` — `DialogueDef`s: an id, a list of nodes, each node a speaker
  (portrait id, reuses `Art.Portrait` already used by `CharacterSelectScreen`/`LevelUpScreen`), line
  text, and either an auto-advance to the next node or a list of `Choice`s (label, target node id,
  optional `Condition` to show it, optional `Effect` list). Validated by `ContentLoader.Validate` like
  every other content file (`GG-LAW-10`) — dangling node ids, unreachable choices, and missing
  portraits all fail load, same severity as a dangling `ClassDef.Id` reference today.
- **Conditions/effects are the existing save-and-party vocabulary**, not a new scripting language:
  `HasItem`, `GoldAtLeast`, `FlagSet` (a new small string-set on `SaveData` for story beats, the same
  idea as `SeenCrawls`), `PartyHas(classId)`; effects are `GrantGold`, `GrantItem`, `SetFlag`,
  `RecruitHero(classId)` (§4), `UnlockOverworldNode`. This keeps the engine a data interpreter, not a
  bespoke VM — consistent with `§4.4`/`GG-LAW-10`.
- **Engine placement:** a `DialogueRunner` belongs in `Gaiden.Meta` (it reads/writes `SaveData`, same
  tier as `Shop`/`Crafting`/`Inn`), not `Gaiden.Core` (it never touches `BattleState`) and not `App`
  (so it's testable under `dotnet test` without Unity, matching every other meta service).
- **Presentation:** a `DialogueScreen`/overlay — portrait boxes anchored left/right (or stacked for a
  3+-speaker scene), the owner's "overlapping boxes" read as the classic visual-novel layering (back
  speaker dimmed, current speaker full-bright, box slides in). Same UI Toolkit + `Theme.uss` approach as
  every other screen, no new rendering tech.
- **Where it's triggered:** an Overworld Event node (§2), a one-time trigger on entering a theme
  (replacing or supplementing the existing `StoryCrawlScreen`, which can stay as the quick/skippable
  option for minor beats while full dialogue handles named-character scenes), and the in-battle
  recruitment prompt (§4) reusing the same renderer in a lighter single-exchange mode.

This is the piece that most directly reverses `§27`. Scope risk is real — branching dialogue content
is a lot of writing, not just engineering — so the recommendation is to ship the **engine** generically
(works for any `DialogueDef`) and let content land incrementally per theme, the same way campaign crawl
text already does.

## 4. Mid-battle recruitment (Fire Emblem "talk")

- **Content:** a `RecruitableDef` (new file or a section of `classes.json`) marks a spawn as a
  recruitable neutral unit for a given stage: starting team (`Team.Neutral`, a new team value), its
  `DialogueDef` id, and a list of `RecruitCondition`s that must all be true before the convince dialogue
  offers to succeed — e.g. `HealedBy(anyHero)`, `ParticipatedInPincerWith(anyHero)`, `PayGold(500)` (paid
  at the moment of recruiting, from the run's banked coins). This directly matches the owner's examples
  ("Heal me", "Participate in a pincer with me", "give me 500 g").
- **Command/event:** a new `TalkCommand` (hero id, target neutral unit id) — legal only when the hero is
  cardinally adjacent to the neutral unit (mirrors `Unit.IsAdjacentTo`, already footprint-aware for
  bosses). `BattleEngine.Apply` resolves it like any other command: no clock cost if conditions aren't
  met yet (mirrors the existing no-op-drag-is-free rule), a `RecruitOfferedEvent`/`RecruitJoinedEvent`
  when they are. On success the unit's `Team` flips to `Team.Hero` for the rest of the battle and it
  follows the same post-battle `BattleFactory.Commit` path new recruits already take via the Summon
  Circle (added to the roster, not just a one-battle guest) — **unless** the owner wants "joins for this
  fight only," which is an open question (§6).
- **AI stance:** a neutral unit before recruitment doesn't fight either team (or fights the enemy team
  passively depending on scene framing) — needs its own `EnemyPlanner`/bot-soak carve-out so it doesn't
  break the "both teams alive while the battle runs" invariant the soak suite already asserts
  (`§6 Verified state`); straightforward since neutrals are a new `Team` value, not a new relationship
  matrix.
- This system does **not** depend on the overworld or the full dialogue engine engineering-wise (it can
  use the lighter single-exchange dialogue mode from §3), only on the recruitment conditions and the
  new `Team.Neutral` plumbing through `BoardRulesTests`-style invariants.

## 5. What would need to change in the bible if the owner approves this

Not done yet — listed so approval has a concrete checklist:

- `§3` — strike "No dialog system, no world map."
- `§27` — rewrite "Story crawl (no dialog)" to describe the dialogue engine, keeping `StoryCrawlScreen`
  as the lightweight path for minor beats.
- `§28` — rewrite "No Overworld" once GridGameCaravan picks a traversal demo; until then it stays.
- `§7 Active frontier` — the "V2 direction... branching story" line currently defers this to a future
  build window; approving this pitch means pulling it into the current one.
- New laws/sections as needed for `Team.Neutral`, `TalkCommand`, `DialogueDef` validation rules.

## 6. Open questions for the owner

1. **Recruitment permanence** — does a convinced unit join the permanent roster (like a Summon Circle
   pull) or only that one battle?
2. **Overworld** — which GridGameCaravan demo wins (Isometric Expanse is the current front-runner), and
   does it keep linear stage unlock or allow free choice of site order?
3. **Magic-vs-machine mechanical depth** — is the "spells fizzle more in plague-heavy zones, machines
   don't" idea meant to be a real stat system (a zone fizzle-chance multiplier, new equipment tags) this
   pass, or pure flavor for now with the mechanic following later?
4. **Plague/town naming** — still open from the Longmother draft; now also needs a name for the plague
   itself if it's going to be referenced in dialogue choices and item flavor, not just crawl text.
5. **Terrain gates (§2.1)** — which barriers, in what order (water, then lava, then...?), and does each
   need a material check on top of clearing the previous theme?
6. **Sequencing** — suggested build order, cheapest/least-risky first: (a) recruitment mechanic
   (§4, no overworld/dialogue dependency, smallest surface), (b) dialogue engine generically (§3), (c)
   overworld (§2) once GridGameCaravan picks a demo, (d) magic-vs-machine content pass
   (§1) threaded in afterward. Confirm or reorder.
