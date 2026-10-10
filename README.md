# GridGame Gaiden

A portrait-mobile tactics RPG built on **Terra Battle's pincer combat** with a **Grandia II-style action
timeline**: drag a hero tile by tile, swap anyone in your way, and trap enemies in a straight line
between two heroes to strike. Time only moves when you act, so it plays the same in a 5-minute bus ride
or an all-night session.

Between battles you're at camp: a Final Fantasy-style menu with a merchant, blacksmith (forge, upgrade,
salvage, repair), alchemist (brew, enchant, heal), inn, ability learning and loadouts, a party roster,
a summon circle, bounties and a bestiary.

## How it's built

| Layer | Where | What |
|---|---|---|
| Simulation | `Assets/Gaiden/Core` | Pure C# (no `UnityEngine`). `BattleEngine.Apply(command) → events`. Deterministic, seeded. |
| Meta | `Assets/Gaiden/Meta` | Pure C# save model + hub services (shop, craft, repair, inn, equip, learn, party, summon, bounties). |
| Content | `Assets/Gaiden/Resources/Content/*.json` | Every class, ability, buff, item, recipe, drop table, stage and campaign. Validated at load. |
| App | `Assets/Gaiden/App` | Unity presentation. One empty scene; `GameApp` builds everything in code. UI Toolkit screens + `Theme.uss`. The battle screen plays simulation events back as animations. |
| Automation | `Assets/Gaiden/App/Automation` | Development-build HTTP endpoint for scripted play, input injection, screenshots and layout audits. |

No prefabs, no hand-built scenes, no Inspector wiring.

## Commands

```powershell
pwsh tools/test.ps1              # rules + meta tests, bot soak over every stage, glyph check (seconds)
pwsh tools/test.ps1 -Compile     # + Unity batchmode compile (Editor must be closed)
pwsh tools/test.ps1 -Ui          # + build the player and run the 35-check UI gate
pwsh tools/build.ps1             # Windows development player → Build/Windows
python tools/harness.py smoke    # launch the player and play title → hub → battle → results
python tools/harness.py sweep    # screenshot + layout-audit every menu screen
python tools/import_art.py       # regenerate art/audio from the GridGame2026 reference project
```

Unity **6000.4.3f1**. Design docs: [`docs/BIBLE.md`](docs/BIBLE.md), backlog: [`docs/USER_STORIES.md`](docs/USER_STORIES.md).

## Credits

See [`Assets/Gaiden/Resources/Credits.txt`](Assets/Gaiden/Resources/Credits.txt).
