# Quicksave — GridGameGaiden: synergy/feel goal + follow-ups delivered; 6 local commits (not pushed)
_Printed: 2026-10-11_

## Current task
Owner goal (2026-10-11): make the game synergize and feel professional — roles (healer must heal, mage ranged
spells, tank taunts), powerup drops, gold on kills, configure gear/abilities, believable potions, standardized press
feedback, confirm before acting, and above all staged timing (confirm → caster particles → filling cast bar while
time moves → cast splash → curved projectile → splash → status effect then icon). Mid-session additions: scarcer
mana (participants divide the charge), action-packed cut-ins with supporter pop-ins, an art-review screenshot loop,
lighter board tiles, a full tutorial over a five-wave first battle, a distinct AI for every enemy family.
All of it is built, verified and committed locally: `895aaa7`, `24bb74d`, `58f281d`, `5c84bb5` (20-wave first battle,
JSON battle records, two X-Potion stacks of 5 per hero in unlocked slots, party of five, defeats keep XP), `9e77c5b`
(directional armor plates, Darkest Dungeon corpses). **Not pushed** — ask before pushing.

## Decisions locked
- Laws in `AGENTS.md` stand. Story text stays lorem ipsum. Web deploy: owner said "not yet" (2026-10-11).
- Fens retune: owner said retune; the original spike had already resolved; later Fens II wave 2 was trimmed (no
  lurker02) to keep it fair under scarcer mana. Finale wave 2 lost a bat, reaper L5.
- Calls the owner may overrule (all in `docs/AMENDMENTS.md` 2026-10-11): spells keep unlimited range (mage identity =
  INT damage); old saves keep their bars; the shrinking cast ring stays beside the new filling bar; first battle runs
  20 waves (~290–350 actions for a strong bot); defeats keep XP; enemy strikes ×0.9; spells bypass armor plates.

## State / where we are
- 214 core tests green; `tools/uicheck.py` 70/70 (harness on port 7787 — GridGameCaravan's player uses 7777); harness smoke/campaign clean; BalanceReport: greedy 4/4,
  casual+camp 2/4, finale 18–33 % casual.
- New tooling: `tools/artreview.py [scenario…]` + automation `record` (fixed-step frame recorder) → contact sheets in
  `artifacts/artreview/`. Automation also has `setbar`, `democaster`, `confirm` setting.
- USER_STORIES: 265 total, 262 ✅ (open: US-348 story text, US-305 HD art V2, US-328 phone profiling).

- Battle records: the game writes one JSON per battle (`persistentDataPath/battle-records`, or `<artifacts>/records`);
  soak with `GAIDEN_RECORDS=<dir> dotnet test … --filter Record_soak`, analyse with `python tools/records.py`.

## Next concrete steps
1. Ask the owner whether to push the seven local commits.
2. Keep running `python tools/artreview.py` after visual changes and review the sheets (owner asked for this loop).
3. Possible polish: pincer sweep could use the bust-window card style too; billboard shows two lines for one cast.

## Anchors
- Files: `Assets/Gaiden/App/Battle/BattleScreen.{Fx,CutIns,Tutorial}.cs`, `Core/Rules/EnemyPlanner.cs` (brains),
  `Core/Battle/BattleRunner.cs` (pickups, mana charge), `Resources/Content/pickups.json`, `tools/artreview.py`
- Commands: `powershell -File tools/test.ps1`, `powershell -File tools/build.ps1`, `python tools/uicheck.py`,
  `python tools/harness.py smoke|campaign`, `python tools/artreview.py`
