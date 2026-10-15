---
codex: 1
project: GridGameGaiden
code: GG
layer: amendments
status: living
updated: 2026-10-09
---

# GridGame Gaiden — Amendments to the GridGame2026 bible

> Every deliberate deviation of GridGame Gaiden from the GridGame2026 `docs/BIBLE.md`, one bullet
> each, with rationale. The [BIBLE](BIBLE.md) marks each inline as **Amended 2026-10-09:**. Items
> the new code simply has not built yet are not amendments; they stay as spec marked ⬜ in the bible
> and [USER_STORIES](USER_STORIES.md). "Owner" = confirmed by the project owner.

## Architecture & process

- **Pure simulation + event playback** (owner). `Gaiden.Core` is engine-free and deterministic;
  `BattleEngine.Apply(command) → events` is the only way a battle changes; Unity only plays events
  back (`BattleScreen.Play`). *Why:* the old Unity-managers-and-coroutines game could not be tested
  or reasoned about and was declared unplayable; a pure core runs under `dotnet test` in seconds.
- **One empty scene, everything built in code; UI Toolkit + `Theme.uss`; no prefabs, no scene
  builders** (owner). *Why:* removes the builder/`.unity` drift class of bugs entirely.
- **GG-LAW-3 (builders) and GG-LAW-4 (SerializeField/Resources/Instantiate guardrails) retired**
  (owner). *Why:* nothing left to guard; `Resources.Load` is now the sanctioned loader.
- **New laws GG-LAW-9..12**: Core has no UnityEngine; content is JSON validated by
  `ContentLoader.Validate`; glyph coverage (`tools/check_glyphs.py`); "done" requires harness
  evidence (dotnet tests + bot soak + automation screenshots) (owner). *Why:* make every claim checkable.
- **GG-LAW-7: harness evidence replaces `DebugManager.Demo_*` buttons.** *Why:* a script that plays
  the game is a better demo than a button and can run unattended.
- **GG-LAW-8: `GameApp.FitStage` replaces `AspectGuard`.** Any aspect fits the fixed 1170×2532 stage
  with bars; no snap list. *Why:* UI Toolkit scales one panel; the snap table and its camera rects are unnecessary.
- **Content is data** (owner): `Resources/Content/*.json` (classes, abilities, buffs, items, recipes,
  drop tables, stages, campaign, traps, bounties, summon, enchants) replaces the C# `*Library` /
  `*Data` classes. *Why:* content edits without code, validated at load and exercised by catalog tests.
- **Fade 0.2 s** (old 125 ms). *Why:* one router-wide transition that also hides screen rebuilds.
- **No Addressables, no chiptune fallback, no particle VFX yet.** Art/audio load from `Resources`;
  a missing clip is silent; feedback is code-drawn. *Why:* fewer moving parts for the rebuild; VFX stay ⬜.
- **AnnouncementWindow + combat feed → one action banner.** *Why:* the banner is an awaited beat in
  the event player; the feed stays ⬜.
- **Settings are per save slot** (no profile-level settings). *Why:* profiles are gone.

## Timeline & time

- **Act-advanced clock** (owner): the clock only runs when the player commits an action; each action
  = `ActionTicks` 20 ticks = 2.0 s; Shield runs to the next enemy turn; refusals and no-op drags cost
  nothing. Rewrites §0.2 "thinking time is game-clock time" and §2. *Why:* playable on a phone in any
  rhythm; decisions, not reflexes; deterministic replay.
- **Integer ticks (10/s), 1000-IP bar, Zone fixed at the rightmost 35%** (owner). *Why:* exact,
  reproducible math (old: floats, ZoneU 0.25–0.35).
- **Speed-derived open-track rate (`Speed × 1.6` IP/tick) + shared Zone crawl (30-tick crossing)** (owner).
- **Pushback/stun/hasten math as implemented** (owner): push `min(0.40, 0.18·(0.5+depth)·strMult)`,
  `strMult = max(0.5, 1+(STR−10)/40)`, no 0.55 floor; stun `max(1, round(8/(1+AGI/50)))` ticks;
  Hasten +u·1000 IP and clears stun. *Why:* integer-tick port with a cap instead of a floor.
- **Only enemies have turn icons.** Heroes act on any action. *Why:* the act-advanced clock makes
  hero icons meaningless.
- **Spawn stagger = random 0–250 IP head start** (replaces u=0 + queue delay; `Queued/PushedBack/
  Resolving` icon modes dropped). *Why:* same visual spread with no extra state.
- **Arrival ties: casts before turns, then lower entry id.** *Why:* deterministic ordering.
- **Hasten on a casting hero rushes the cast.** *Why:* heroes have no turn icon to push.
- **Shield never auto-presses.** *Why:* there is no real-time window to expire.
- **Optional Terra Battle drag timer (4 s, Settings, off by default)** (owner). *Why:* restores hand
  pressure for players who want it without making it the default.
- **Time-banked orbs come from drag-timer seconds left: 1 Blue per 1.25 s, max 3**, minted at the
  next enemy turn in the same action (old: seconds before the next enemy trigger, 1 per 3 s) (owner).
  Current consequence: no enemy turn in that action → nothing banked.
- **Countdown badge ("acts in N moves")** on every enemy. *Why:* the act-advanced clock needs a
  Terra Battle–style read of "how many moves do I have".
- **All content spells have a 1.5 s base cast** (WIS+INT scaled), so they usually resolve inside the
  action that casts them. *Why:* keeps casts on the timeline/interruptible without stalling play;
  open question §29.

## Combat rules

- **Supporters: any ally adjacent to an attacker; strike at 50%.** No line-of-sight/axis test. *Why:*
  readable at a glance; 50% keeps supporters from out-damaging the pincer.
- **Pincer order: dropped hero's pair first, then chains, then nearest to the last link.** *Why:* the
  player's own move always resolves first.
- **Respite spin dropped**; dead-line pairs are skipped silently.
- **Humanoid enemies resolve enemy-team pincers on their turn** (no orbs). *Why:* the old bible had
  enemies seek pincers but left resolution implicit.
- **Enemy melee targets the adjacent hero with the lowest current HP.** *Why:* makes wounded heroes
  feel threatened; deterministic.
- **Boss shove sends each hero to the tile the body vacated on its lane** (no cascade; aborts only on
  a non-hero blocker). *Why:* a 2×2 step can never push a hero off-board, so the abort case vanishes.
- **Boss phases trigger at the start of the boss's own turn** and are data (`ClassDef.BossPhases`).
  *Why:* phase changes land as a readable beat, not mid-combo.
- **Charges: fixed 2.5 s (not stat-scaled); single-target charges follow the hero; a charging enemy
  still moves/attacks on later turns.** *Why:* telegraphs read the same every time; dodging is by
  interrupting.
- ~~**Charge cancel orb color = first cost letter, else damage-type color.**~~ (moot since generic mana, 2026-10-10)
- **Fire on a Frozen target thaws it and steams off the resulting Wet in the same hit.** *Why:*
  falls out of running the expire chain before the Wet check; reads as "melted and evaporated".
- **Frozen (timeline-halting) ticks at the shared turn boundary; hero turn buffs and skill cooldowns
  tick once per action.** *Why:* a halted icon never reaches its own turn; heroes have no turns.
- **Traps are triggered only by heroes**, by any way of entering the tile; excluded from teleport,
  revive and spawn. *Why:* enemy-laid traps are a hero hazard.
- **Default coin drop ≈ ½ XP ±20%** when a drop table authors no coins (none do). *Why:* one curve
  instead of hand-tuning 16 tables.
- **Coins are gold**, credited on victory only; `GoldTracker` removed. *Why:* one number to reason about.
- **Every party hero (alive or fallen) receives the battle's full XP total** (not split). *Why:*
  reserves of the active party never fall behind; simple.
- **Wounds: survivors keep HP, fallen → 1 HP, a level-up → full HP; defeat/retreat → full; escape
  keeps wounds.** *Why:* level-ups feel like a reward; escape is a cost.
- **Consumed items are deducted at commit on every outcome** (victory, defeat, retreat, escape).
- **Potion = single-ally heal (PickActor)**; damage items hit one enemy with a bonus vs a tag; new
  item knobs: GrantsOrbs (Ether), CuresBuff/CuresAll, Revives (Phoenix Down), Escape (Smoke Bomb),
  OutOfBattleOnly (Tent). *Why:* every consumable in content now does something.
- **Double Attack / Counter Attack are passives** (`AbilityKind.Passive` on enemy classes): second
  pincer strike at ½; adjacent counter at ½, no chains.
- **Equipment durability resets on unequip** (per-instance durability not tracked). *Why:* inventory
  is id → count; known simplification, open question.
- **Weapon BaseDamage adds to physical offense; armor rating unused; armor never wears in battle.**
- **Effective Speed = SPD + gear SPD + ½ gear AGI.**
- **A hero class placed in a stage wave fights as an enemy** (the Vampire Lord is the recruitable
  `vampire` class). *Why:* one class table.
- **Mug currently hits twice** (fixed Mug hit + its row's BaseDamage). Recorded as current truth and
  a defect (US-110), not a design change.
- **Silence currently affects hero slots only** (enemy charges ignore it). Recorded as current truth (US-109).

## Meta & Camp

- **Merged Camp hub** (owner): the FF-style `HubScreen` replaces the 7 vendor scenes, `VendorNavBar`
  and StageSelect-as-home. *Why:* the old §25.9 goal; one menu is faster than scene hops.
- **Inn added** (owner): hub row + Alchemist Heal tab; 0.5 G per missing HP across the whole roster.
- **Learn abilities for gold** (owner) on the Abilities screen: class list + general spell catalog;
  price 120 + 80 per orb, skills 300; the bar accepts only Known abilities. *Why:* a gold sink and
  build expression beyond fixed class presets.
- **Three save slots replace profiles** (owner); fixed party name "Lightbearer"; JSON with
  migrations; dictionaries/sets serialize natively. *Why:* simpler and testable.
- **Stage list shows unlocked stages newest-first and collapses locked ones into one teaser row**
  (owner). *Why:* the frontier is always on top; no wall of locked rows.
- **Stage names are "<Theme> I/II/III"** (owner).
- **Bounties get their own Camp screen** (plus a progress strip on Adventure).
- **Blacksmith Upgrade tab** (the old unused upgrade recipes now have a UI); **Enchant runs through
  `Station: Enchant` recipes**.
- **Summon recruits arrive at the roster's average XP and auto-join the party when there is room.**
  *Why:* a new recruit is usable immediately.
- **Party must keep at least one member.**
- **Camp item use** (potions on a hero, Tent on everyone). *Why:* Tent had no battle use.
- **Retreat (pause menu) and Escape (Smoke Bomb)** as battle exits. *Why:* a way out without
  waiting for a wipe.
- **Story crawl marks the theme seen when it starts**, scrolls at 95 px/s, and also plays when
  entering via "Next stage".
- **Persist after every successful Camp action** (old: commit on vendor exit). *Why:* no lost purchases.
- **First-battles hint** (until the third stage is cleared): after 5 s idle the bot's best pincer
  move is shown. *Why:* answers part of the old tutorial question without a tutorial system.

## Presentation

- **Debuff icons are up to 3 letter pips** (no radial ring, no cycling).
- **Projectiles are a moving dot, straight or arced** (other motions pending).
- **Ability info shows in the actor panel while targeting** (long-press tooltips pending).
- **HUD is absolute design-pixel layout in `BattleScreen`** (old `HudLayout` row constants gone).

## 2026-10-10 — parity batch

- **Black and Green orbs now have uses.** The old catalog had no spell costing B or G, so Black/Green
  heroes minted dead orbs. *Change:* Poison costs (B)(B); new Green spells **Regen** (G — heal 4/s for
  6 s via the new `regen` buff, `BuffDef.HealPerTick`) and **Rejuvenate** (GG — heal 30 + cleanse).
- **Distinct kits for the 55 default-kit hero classes.** Each gets a color identity (from its name/role)
  and a color-matched preset + learnable pool; ninjas carry Teleport. The bible's seven seeded loadouts
  (§23.2) are unchanged.
- **Ranged archetype built** (§14.2): `ClassDef.AttackRange`; hags and the Swamp Mistress shoot the
  lowest-HP hero in a clear straight lane within 3 tiles and keep their distance.
- **Mechanical archetype built**: `ClassDef.ImmuneBuffs` (Mechanical/Construct: poisoned, sleep → "Immune!")
  plus Poison resistance 0.
- **Buff stacking policies** (§8.6): `BuffDef.Stacking` = Refresh (default) / Stack / Ignore / Upgrade.
- **Armor wears in battle**: −1 durability per landing enemy hit; at 0 it breaks and its bonuses vanish
  (no shatter damage — that stays weapon-only). Repair works as for weapons.
- **Relic passives** (§24.1.1 named passives): `PincerDamageBonus`, `RegenPerTurn`, `StartsProtected`,
  `BankCapacityBonus` (the §3.1.4 knob, max +4 slots), authored on Bloodstone, Jade, Sunfire, Phantom, Stars.
- **Ability-bar weapon swap** (old US feature): a bar slot can hold `weapon:<id>` from the bag; using it
  swaps weapons (costs a turn; incoming weapon at factory durability); the save follows the swap.
- **Battle end is checked after every action**, not only after kills/turns (robustness).
- **UI**: mute toggles; distinct projectile curves; long-press ability card; radial buff timers with
  >3-buff rotation; idle hint also suggests spells/GUARD.
- **WebGL build** (§11.4): uncompressed Minimal template; saves flush to IndexedDB via `GaidenSync.jslib`
  (`FS.syncfs`), and `SaveStore` falls back from `File.Replace` where unsupported.
- **Bosses are tagged `Boss`** (cyclops, naga, vampire) so their frame and timeline icon read purple
  (heroes never take boss styling).
- **Respite** restored (§13.1.1): a later link whose whole line already fell spins and shows "Respite".

## 2026-10-10 — design decisions taken (owner may overrule; each is one constant or flag)

- **Spells never miss but can crit** (§13.1.2 backlog closed): crit chance = ½ the caster's physical crit %,
  ×1.6 damage, a hero spell crit mints a bonus orb like a pincer crit. `BattleConfig.SpellCrit*`.
- **Inventory: no global cap** (§29 #19 closed): items stack to `MaxStack`; battle loot beyond it is
  auto-sold at the merchant price and reported on the results screen.
- **Per-instance durability** (§24.5 closed): unequipped worn gear keeps its durability and repair count
  (`SaveData.Worn`); equipping takes a fresh copy first, else the least-worn.
- **Guided first battle** (§29 #6 closed): the first battle of a new save shows a 4-step coach card
  (drag → pincer → spell → timeline) with immediate move hints; skippable; `SaveData.TutorialDone`.
- **No permadeath; New Game+** (§29 #1 closed): after clearing the final stage, camp offers New Game+:
  progress resets, roster/gear/gold kept, enemies +10 levels and coins +50% per cycle.
- **Audio is fully licensed**: every SFX is original (synthesized by `tools/make_sfx.py`); all music is
  Kevin MacLeod CC-BY 4.0 ("Heroic Age" replaces the Pixabay victory track, "Heartbreaking" replaces the
  unsourced defeat track). `tools/check_assets.py` fails the gate if any cue or track is missing.
- **VFX are procedural UI Toolkit effects per element** (fire, ice, lightning, poison, holy, dark, arcane,
  heal) plus status auras, replacing the old prefab VFX library.

## 2026-10-10 — generic mana (owner direction)

- **Owner:** "The colored mana system doesn't add anything, just make them generic mana orbs that can be
  globally spent on actions."
- `ManaBank` is now a capped count; every orb is identical; spell costs are integers (the former letter
  counts, 1–3). Removed: `ManaColor`, `ClassDef.ColorAffinity`, color-matched spending, the Colorless
  wild orb (crits now mint a plain bonus orb), per-source orb colors, orb letters and the colorblind
  orb tint. GG-LAW-6 (one shared, visible, capped pool) still holds. Bible §3.1 rewritten.

## 2026-10-10 — pincer close-up cut-in (owner request)

- **Owner:** GridGame2026 "had a technique for zooming in on the characters so it was like a close up
  shot with some shifting like a hand held camera." Ported from `PortraitManager.SpawnPair2DRoutine` /
  `Portrait2DInstance` (same slide curve, speed ×1.75, four entry patterns, lanes flanking the line) and
  extended with hand-held Perlin sway, a slow portrait push-in, a world dim and a board push-in. Plays on
  the first pincer of an action only, so chains stay quick. Settings → "Pincer close-ups" (default on).

## 2026-10-10 — battle unit frames match the menus (owner request)

- **Owner:** "make the borders around the Heros and Enemies in the battle match the menu system that
  exists outside the battle; same UI for all." Unit frames now use the menu portrait frame (white
  rounded border, window gradient). The old gold/red/purple outer borders became a thin inner keyline
  so team and boss still read at a glance.

## 2026-10-10 — pixel-art enemy timeline icons (owner request)

- **Owner:** "use imagemagick to reduce and enhance the enemy icons so that they stand out and look like
  pixel art versions of the full enemy … so the user can tell from a glance who's the next attacker."
  Turn icons now show the whole enemy as a 36 px, 16-colour sprite with a sticker outline; the next
  attacker's icon gets a gold ring and a slight lift.

## 2026-10-10 — timeline readability, enemy personalities, cast countdowns (owner requests)

- "The enemies don't need turn numbers on them because the timeline is there" → board countdown badges removed.
- "a very fast enemy [can] move past a slow enemy on the windup" / "set up the fast enemies to bypass enemies on
  the line" → enemy speed tiers 8 / 11 / 15 / 18 and an overtake lane on the timeline.
- "smarter enemies will attack the weakest enemy, dumb enemies will attack the closest" → targeting
  personalities (Closest / Weakest / Threat / Squishy / Erratic), per family in classes.json. Replaces the old
  blended score (distance + HP fraction − INT × threat). Bot balance moved slightly easier mid-campaign.
- Timeline icons centred on a thinner line, opaque, masked to the circle; debuff badges on circles + tiles.
- "casting spells requires a casting period which should also be on the timeline" → casts travel 0 → 1 over
  their own cast time on the same line (was: constant reference pace below the line).
- "use the enemy border as a radial countdown … each spell has a different color … cycle" → `CastRing`.
- Enemy icon method recorded in docs/PIXEL_ICONS.md (owner: "save and record the method").

## 2026-10-10 — story text removed (owner)

- **Owner:** "get rid of all this story about the missing sun and the UNDEARTH, its terrible, just put in lorem
  ipsum for now." All five theme crawls are lorem ipsum; the default profile name is "Player". The replacement
  story is the caravan premise (US-348).

## 2026-10-10 — cast time on the caster; spell VFX arc; biome tiles (owner)

- "instead of putting casting time on the timeline have it show on the caster" → cast icons removed from the
  line; the countdown ring moves to the caster's frame; the target keeps a spell chip.
- "always a particle effect … Hero1 'drawing energy' then cast burst, spawns an electrical particle that curves
  to the space above an enemy where it spawns the lightning strike" → `SpellVfx` (gather → burst → curving mote
  → element strike from above the target).
- "each area should use a different tile style" → `tools/make_tiles.py` recolours the original tile per biome.

## 2026-10-10 — overlap beats, one status slot, burn stacks, party of six (owner)

- Timeline circles stay on the centre line; overlapping circles take turns on top (no lane lift).
- One status slot per actor that flashes through buffs/debuffs; the card explains each.
- "fire has multiple stacks … not sure if they should tick when it's the user's turn and they are idle; it kills
  the wait-and-decide component" → burning stacks to 3; DoT ticks only on the act-advanced clock, so idle
  thinking is free (decision: keep the clock act-advanced; recorded here so the owner can overrule).
- "Up to six party members need to be visible" → `MaxPartySize` 4 → 6; Camp party is a 2-column grid.


## 2026-10-11 — synergy, staging, press feel, scarcer mana (owner)

- "it all needs to synergize … a healer should need to heal, a magician should have ranged spells, a tank should be
  able to taunt enemies, powerups should sometimes drop, money should accumulate on kills" → hero **roles** with
  matching kits (§25.13), **Taunt** (§14.1.1), **caster power** — INT/STR/WIS now scale hero spells, skills and heals
  (§13.1.5), **Cleave / War Cry** for strikers, **pickups** (§13.3.1). Gold on kills already existed (coin burst).
  Calls the owner may overrule: spells stay unlimited-range (the mage's identity is INT damage, not reach); existing
  saves keep their bars, only new heroes get the role kits.
- "button presses need feedback like depress, or confirmation screens when deciding to cast a spell … standardized"
  → `Ui.Pressable` on everything tappable (§11.5); select → confirm → act with a confirm card (§4.4.1), switchable
  in Settings.
- "if I clicked poison and an icon just appeared on an enemy it would feel meaningless … particle effects around the
  actor, a loading bar fills (while game is in motion), cast splash, a projectile on a curve, a splash attack, then a
  bubbly green poison effect which coincides with the poison debuff" → staging (§6.2). **Deviation:** the 2026-10-10
  ring that *shrinks* on the caster stays; a charging bar that *fills* is added beside it.
- "mana needs to be a more rare resource … the number of participants in the attack increases the multiplier needed
  to earn a mana bubble" → the mana charge meter (§3.1.9). Tuned with `BalanceReport`: greedy bot clears the
  campaign 4/4; casual+camp bot 2/4 (was 1/4 at the start of the day). To keep Mireholt Fens II fair under scarcer
  mana, its second wave drops the second lurker (hag + naga only). Mana Shard pickup gives 1 orb.
- "portrait slides … action packed … supporters pop in and out" → action cut-ins (§9).
- "the tiles transparency contrast is too high … the background needs to be clearly visible" → lighter, lower-contrast
  tiles; the stage background is dimmed less.
- "take screenshots and then examine them for artistic integrity" → `tools/artreview.py` + the automation `record`
  endpoint (fixed-step frame recorder: `Tween.FixedStep`), producing per-scenario contact sheets. First review pass
  fixed: flat cut-in band → feathered band + bust window + dimmed stage; opaque area slabs → soft bloom; dot
  projectile → glowing orb with comet tail; status text in the status colour; bolder taunt rings; item icon on the
  confirm card; billboard dodges top-row action.

## 2026-10-11 — full tutorial, five-wave first battle (owner)

- "there needs to be a full tutorial with explanations and examples, the first battle needs 5 waves of increasing
  difficulty" → Green Valley I is five waves (levels ramp 1 → 2; test `The_first_battle_has_five_waves_of_rising_difficulty`)
  and the coach card became ten lessons (§9). The first battle is now ~43 actions for a strong bot — above the 15–40
  pacing target, accepted for the teaching battle.

## 2026-10-11 — a brain for every enemy family (owner)

- "come up with ai for every single enemy unit, they are all different in their own unique ways" → 20 brains (§14.6)
  over the 41 families; with targeting and kit no two families play alike (test). The toad's tongue now slows (it
  matched the frog exactly). Smarter foes made the campaign harder, so enemy strikes carry ×0.9
  (`EnemyStrikeMultiplier`) and the finale's second wave drops a bat and its reaper is level 5; result: greedy
  bot 4/4, casual+camp bot 2/4, finale 18–33 % for the casual bot (it was 9 % this morning).

## 2026-10-11 — 20-wave first battle, battle records, potion stacks, party of five (owner)

- "make the first battle 20 rounds with increasing difficulty and record everything … keep the records of all moves
  as JSON" → §22.7, §22.6. Records drove fixes: skirmishers dart one tile (×0.8 glancing strike) instead of two;
  evasive brains make a last stand when they're the only fighters left (no stalls); the bot values Taunt by the
  allies it covers; late waves stay at level 4–5 and grow in numbers instead.
- "give each character 2 stacks of full life potions and unlock the slots … two stacks of 5x" → X-Potion ×5 twice per
  hero; all slots open from the start (§4.7). **Deviation:** progressive slot unlocks retired.
- "add a fourth and fifth hero … a 5 player team seems about right for max" → party cap 6 → 5; Red Mage + Engineer
  join the starting party (§25.14).
- Call the owner may overrule: **a defeat keeps its XP** (§22.2) — without it a learning player could never get past a
  20-wave first battle.
- Harness: `tools/harness.py` now uses port 7787 (`GAIDEN_PORT`); GridGameCaravan's player also listens on 7777.

## 2026-10-11 — armor plates and corpses (owner)

- "some enemies are armored and need to take damage from that side to break it off" → directional plates (§13.5).
  Calls the owner may overrule: spells bypass plates (a mage's answer to armor); the plate turns to face the enemy's
  target each turn.
- "add in the corpses for some things so they get in the way like Darkest Dungeon" → corpses (§14.7). Calls: corpses
  don't hold up a wave and rot after 6 actions (a 20-wave battle would otherwise silt up the 6×8 board).
- Balance after both: greedy bot 4/4 campaign and 6/6 first battle (~348 actions); casual+camp 1/4.
- Art review fixes: plates sit on the tile rim (they covered the HP bar); the billboard dodges upward over the banner
  (it landed on the heroes), and also while a spell charges on a top-row target. UI gate 70/70.

## 2026-10-11 — final art review pass

- Every art-review scenario re-recorded on the final build (caster, sweep, confirm, poison, fireball, potion, taunt,
  armor, corpses) and reviewed. Fix: the first-link pincer sweep now uses the standard cut-in band (slim, tilted,
  feathered, white core) along each portrait's lane, so every cut-in shares one look. UI gate 70/70 (the long-press
  check now recognises the "Charges …s on the timeline" wording).

## 2026-10-15 — Merchant Bargain shelf (owner direction, US-246, GG-T38)

- "a rotating 'Used / Bargain' shelf selling weapons and armor at 35–60% price but with only 10–40% of max
  durability left, clearly labelled with a durability bar before purchase — cheap power now, real risk of
  shattering mid-combat later, a deliberate early-game gamble." A third `ShopScreen` tab (`Meta.Bargains`, §25.1)
  rolls `ShelfSize` = 4 eligible weapons/armor deterministically from `SaveData.BargainRotation`; the shelf is
  stable until the rotation advances (once per finished battle, win or lose) and replays identically from the
  same save. Buying files the rolled durability into the existing per-instance `SaveData.Worn` tracking (§24.5,
  US-246 — already in place from the 2026-10-10 batch), so the piece equips worn rather than factory-fresh.
- Not yet built (tracked under GG-T54): teaching `MetaBot`/`CampRoutine` to shop the shelf and avoid gear close to
  shattering.
