# Quicksave — crash-recovery backfill after the 2026-10-10 computer crash
_Printed: 2026-10-10_

## Current task
There is no in-flight work to resume. The computer crashed, but this repo was already fully
committed and pushed to `origin/main` at the time (`git status` clean, nothing ahead/behind). This
quicksave is a manual backfill of the crash-recovery convention (`quicksave`/`quickload`/the
`do`/`recover` hook), not a resume of lost work — nothing was lost. Treat **Next concrete steps**
below as the reasoned continuation of the project from its last real commit, not as something
already underway.

## Decisions locked this session
- The crash-recovery convention (`.claude\commands\quicksave.md`, `.claude\commands\quickload.md`,
  `.claude\hooks\quickload-on-do.ps1`, `.claude\settings.json` registering the `UserPromptSubmit`
  hook) was added to this repo, mirroring the convention already used elsewhere in MindAttic
  (template: `D:\Projects\MindAttic\ChiMesh\.claude\`). Typing a bare `do`, `do it`, or `recover`
  now restores this file as working memory.
- Project laws from `AGENTS.md` stand and are not up for relitigation: this is a clean rebuild of
  GridGame2026 (Terra Battle pincer combat + Grandia II timeline + FF-style camp); `GridGame2026`
  is the frozen design reference — read it, never edit it. `Assets/Gaiden/Core` and
  `Assets/Gaiden/Meta` are pure C# (`noEngineReferences`); battle state changes only through
  `BattleEngine.Apply(command) -> events`; all content (classes, abilities, items, stages,
  campaign) is JSON under `Assets/Gaiden/Resources/Content/*` validated by `ContentLoader.Validate`;
  no editor work — one empty scene, `GameApp` builds everything in code; "done" means
  `tools/test.ps1` green, `tools/build.ps1` + `tools/harness.py` run with screenshots actually
  looked at; gameplay changes update `docs/BIBLE.md` (+ `docs/AMENDMENTS.md` for deviations) and
  `docs/USER_STORIES.md` statuses — if a request contradicts the bible, say so and ask rather than
  silently deviating.

## State / where we are
- Last several commits (all WebGL/UI/combat-presentation polish on top of a very mature feature
  set): `553bf2e` "Glove cursor points right; webcheck taps follow the new camp/adventure layout
  (WebGL 4/4)", `b4f0adc` "Caravan upgrades: Mana Casks, Medic Wagon, Armory Wagon, Strongbox,
  Scouts", `e83d4b9` "Expeditions ... and guest allies", `760a8fc` "Undead leave solid gravestones
  / bone piles", `7f34054` "Combat presentation: FF VI billboard ... cut-in modes". `origin/main`
  matches local `main` exactly.
- Per `docs/USER_STORIES.md`'s tally (243 stories total): **240 ✅, 1 🟡, 2 ⬜**. This project is
  functionally complete and shipping-adjacent across battle core, timeline, mana, abilities,
  buffs, AI, bosses, meta/camp services, UI screens (all 35), and most of platform/ship. The three
  non-done items are the entire remaining backlog:
  - **US-348** (🟡, "Production roadmap") — Caravan story framing (shops-between-battles,
    Camp -> Caravan, expeditions) is fully built, but the **story text is lorem ipsum by owner
    request (2026-10-10)** — i.e. the owner explicitly deferred writing real narrative beats, this
    is not an oversight.
  - **US-305** (⬜) — HD art pass; explicitly scoped to **V2 per RFC 0002**, never part of the V1
    claim. Current art is the original GridGame illustration set. Not something to pick up now.
  - **US-328** (⬜) — On-device profiling pass; needs a **physical mid-tier phone** (the emulator
    is software-rendered at ~18 fps, not representative). Windows/WebGL already hold >=50 fps.
    Blocked on hardware access, not on more coding.
- `docs/BIBLE.md` §29 "Open Design Questions" lists smaller unresolved design calls that gate
  future stories (not blocking anything currently ✅): permadeath/NG+ nuance (partly answered —
  NG+ exists, no permadeath, per US-332), a scripted tutorial beyond the current hint system,
  Frozen-status duration semantics, whether banked timeline seconds should persist to the next
  enemy turn, whether `ActionTicks` (2.0s/action) should lengthen so 1.5s casts visibly ride the
  timeline, an inventory cap, and per-instance durability reset-on-unequip.

## Open questions / pending
- The owner deliberately left caravan story text as lorem ipsum (US-348) as of 2026-10-10 — do not
  "fix" this by inventing narrative without asking; it reads as an intentional placeholder, not a
  gap to quietly fill.
- US-328 (on-device profiling) is blocked on the owner providing/using a physical mid-tier phone —
  not actionable by an agent alone.
- The BIBLE §29 open design questions have no deadline attached; nothing currently depends on
  resolving them, but they're the right place to look before adding new timeline/economy features.

## Next concrete steps
1. Confirm the project still builds/tests cleanly post-crash: `pwsh tools/test.ps1`, then
   `pwsh tools/test.ps1 -Compile` (Editor must be closed) to make sure nothing in `Library/` got
   corrupted by the crash.
2. If the owner wants to close the one 🟡 item: write real story beats per expedition for
   US-348/§27 (the caravan framing, screens, and data plumbing are already built — this is content
   work, in `campaign.json`/the relevant content files, not code).
3. Leave US-305 (HD art, V2/RFC 0002) and US-328 (needs physical phone) alone until the owner
   either provides hardware (US-328) or opens V2 scope (US-305) — don't start either speculatively.
4. If the owner wants new scope beyond the current backlog, the right place to start a
   conversation is `docs/BIBLE.md` §29's open design questions — resolve one, write the answer into
   its real section, then delete it from §29 per the file's own stated process.
5. Before claiming anything is done: `pwsh tools/build.ps1` then `python tools/harness.py
   smoke|sweep|campaign` and actually look at the screenshots in `artifacts/` — this project's own
   law #4, not optional.

## Anchors
- Files: `AGENTS.md`, `docs/BIBLE.md` (esp. §27 Story crawl, §29 Open design questions),
  `docs/USER_STORIES.md`, `docs/AMENDMENTS.md`, `Assets/Gaiden/Core/Battle/BattleRunner*.cs`,
  `Assets/Gaiden/Resources/Content/*.json`, `Assets/Gaiden/App/Automation/AutomationServer.cs`,
  `tools/test.ps1`, `tools/build.ps1`, `tools/harness.py`, `tools/uicheck.py`, `tools/webcheck.mjs`
- Names / ids: `BattleEngine.Apply`, `ContentLoader.Validate`, `GameApp`, `CampRoutine`, `HeroBot`,
  US-348, US-305, US-328, RFC 0002
- Commands to re-run:
  - `pwsh tools/test.ps1` / `pwsh tools/test.ps1 -Compile` / `pwsh tools/test.ps1 -Ui`
  - `pwsh tools/build.ps1`
  - `python tools/harness.py smoke|sweep|campaign`
  - `python tools/uicheck.py`
  - `pwsh tools/build.ps1 -Target WebGL; node tools/webcheck.mjs`
