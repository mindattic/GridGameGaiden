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
- **Charge cancel orb color = first cost letter, else damage-type color.** *Why:* charges carry no cost.
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

