---
codex: 1
project: GridGameGaiden
code: GG
layer: stories
status: living
updated: 2026-10-09
---

# GridGame Gaiden — User Stories (parity backlog)

> One line per [BIBLE](BIBLE.md) mechanic or feature. Goal: **full parity** with the GridGame2026
> bible as amended in [AMENDMENTS.md](AMENDMENTS.md).
>
> Status: **✅ verified** — a named test in `Tests/Core.Tests` or an automation-harness step
> (`tools/harness.py smoke|sweep`) demonstrates it · **🟡 partial** — built (or partly built) but no
> test/harness step asserts the behavior, or the assertion is only indirect · **⬜ planned** — not
> built. For Camp screens, harness ✅ means "the screen builds, is screenshotted and layout-audited
> in `sweep`"; the service behind it has its own line.
>
> Evidence key: test names are xUnit methods in `Tests/Core.Tests/*.cs` (last run 2026-10-09: all
> green); **soak** = `ContentAndSoakTests.Bots_play_every_stage_without_breaking_invariants` (runs
> the mechanic over real content but asserts only invariants); **smoke** / **sweep** = harness runs
> whose artifacts are in `artifacts/smoke1/` and `artifacts/sweep/`.

## Definition of Done (every story)
1. `pwsh tools/test.ps1` green (and `-Compile` for App changes). 2. Bible section updated. 3. A named
test for every rule. 4. A harness step for anything visible. 5. Glyph check green. 6. One commit at the end.

---

## Battle core (board, slide, pincer, combat)

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-001 | 6×8 board, one unit per tile, footprint-aware occupancy | §1 | ✅ | `AssertInvariants` in soak + every BoardRulesTests case |
| US-002 | Drag = cardinal one-tile steps; diagonal step refused | §1.1 | ✅ | `Diagonal_is_not_a_pincer_and_diagonal_step_is_refused` |
| US-003 | Slide displaces the occupant into the tile just left (cascade) | §1.1 | ✅ | `Slide_displaces_occupant_into_tile_just_left` |
| US-004 | Board edge stops the drag; rest of path dropped | §1.1 | ✅ | `Off_board_step_stops_the_drag` |
| US-005 | A drag that changes nothing is a cancel (no clock cost) | §1.1 | ✅ | `Noop_drag_costs_no_time` |
| US-006 | Movement never deals damage (GG-LAW-1) | §1.1 | ✅ | `Movement_never_deals_damage` |
| US-007 | Pincer geometry: row/col, contiguous, ally-free, ≥1 opponent | §1.2 | ✅ | `Worked_example_1_4_slide_then_pincer`, `Gap_or_ally_in_line_breaks_pincer` |
| US-008 | Board-wide scan; dropped hero's pair first, then chains, then nearest | §1.2.2 | ✅ | `Chain_pincers_resolve_in_order` |
| US-009 | Both attackers strike every trapped opponent | §1.2.4 | 🟡 | indirect (`Worked_example_1_4_slide_then_pincer`); per-target strike count not asserted |
| US-010 | Supporters = allies adjacent to an attacker; join and mint own orb | §1.2.3 | ✅ | `Supporters_join_and_mint_their_own_orb` |
| US-011 | Supporters strike at 50% damage | §1.2.3 | 🟡 | none (fraction not asserted) |
| US-012 | Only Humanoid 1×1 units are pincer attackers | §1.3 | 🟡 | soak only |
| US-013 | Worked example §1.4 end-to-end | §1.4 | ✅ | `Worked_example_1_4_slide_then_pincer` |
| US-014 | 2×2 boss is immovable to slides | §1.5 | ✅ | `Two_by_two_boss_blocks_slides_and_is_pincered_across_its_width` |
| US-015 | 2×2 boss pincered across its width counts as one opponent | §1.5 | ✅ | `Two_by_two_boss_blocks_slides_and_is_pincered_across_its_width` |
| US-016 | Boss step shoves heroes to the vacated tile on their lane | §1.5 | 🟡 | soak (cave stages) only |
| US-017 | Boss melees via footprint adjacency; one icon | §1.5 | 🟡 | soak only |
| US-018 | Physical formula: mitigation, variance, hit/crit, level scaling | §13.1.1 | 🟡 | indirect (every damage test); no formula-value test |
| US-019 | Death clears buffs and the unit's timeline entries | §13.3 | ✅ | `AssertInvariants` (no entry owned by a dead unit) in soak |
| US-020 | Loot: XP, coins, item drops per drop table, kills | §13.3 | ✅ | `Killing_last_enemy_of_last_wave_is_victory_with_loot` |
| US-021 | Default coin drop ≈ ½ XP ±20% when a table has no coin range | §13.3 | 🟡 | none |
| US-022 | Waves spawn in sequence inside the same action | §22 | ✅ | `Waves_spawn_in_sequence` |
| US-023 | Last wave cleared = victory; all heroes down = defeat | §22.2 | ✅ | `Killing_last_enemy_of_last_wave_is_victory_with_loot`, `Defeat_resets_wounds_and_grants_nothing` |
| US-024 | Weapon wears 1 per pincer and shatters at 0 (×1.5 strike, 15% self-damage) | §24.5 | ✅ | `Weapon_wears_per_pincer_and_shatters_at_zero` (events + removal; multipliers not asserted) |
| US-025 | Deterministic replay from seed + commands | §4.5 | ✅ | `Battles_replay_identically_from_seed` |
| US-026 | Traps trigger on heroes only (step, displacement, shove, teleport) | §14.2 | 🟡 | soak (desert-01) only |
| US-027 | Humanoid enemies resolve enemy-team pincers on their turn (no orbs) | §1.3 | 🟡 | soak only |
| US-028 | Enemy melee hits the adjacent hero with the lowest HP | §14.1 | 🟡 | none |
| US-029 | Fixed hero start tiles; enemy spawn tile or random rows 0–4 | §1 | 🟡 | soak only |
| US-030 | Double Attack passive: pincer attacker strikes each target twice (2nd at ½) | §7 | ✅ | `Double_attack_passive_strikes_twice_and_counter_strikes_back` |
| US-031 | Counter Attack passive: adjacent defender strikes back at ½, no chains | §7 | 🟡 | `Double_attack_passive_strikes_twice_and_counter_strikes_back` (conditional assert) |
| US-032 | Respite spin when a link's whole line is already dead | §1.2.4 | ⬜ | none (dropped: such pairs are skipped) |

## Timeline

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-040 | Act-advanced clock: only committed actions advance it; 20 ticks per action | §2.0 | ✅ | `Clock_only_advances_on_committed_actions` |
| US-041 | Refused commands cost no time and no resources | §2.0 | ✅ | `Unaffordable_or_silenced_spell_is_refused_without_cost` |
| US-042 | Integer ticks, 1000-IP bar, Zone = rightmost 35% | §2.0–2.1 | ✅ | `Pincer_on_in_zone_enemy_pushes_its_icon_back` / `Pincer_outside_zone_has_no_pushback` (IP 920 vs 100) |
| US-043 | Speed-derived open-track rate + shared 30-tick Zone crawl | §2.1 | 🟡 | `Enemy_acts_when_its_icon_reaches_the_trigger` (turns happen; rates not asserted) |
| US-044 | Enemy acts when its icon reaches the trigger; icon resets to 0 | §2.4 | ✅ | `Enemy_acts_when_its_icon_reaches_the_trigger` |
| US-045 | In-Zone hero hit pushes the icon back and stuns it | §2.3 | ✅ | `Pincer_on_in_zone_enemy_pushes_its_icon_back` |
| US-046 | Out-of-Zone hit: no pushback | §2.3 | ✅ | `Pincer_outside_zone_has_no_pushback` |
| US-047 | Pushback scales with depth and STR (cap 0.40 u); stun by AGI | §2.3, §2.7 | 🟡 | push/stun > 0 asserted, scaling not |
| US-048 | Quicken/Hasten slides a turn icon +0.30 u and clears stun | §2.7.1 | ✅ | `Quicken_hastens_an_enemy_icon` |
| US-049 | Hasten on a casting hero rushes the cast | §2.7.1 | 🟡 | none |
| US-050 | Shield: Protection on all heroes + run to the next enemy turn | §2.5 | ✅ | `Shield_protects_heroes_and_skips_to_next_enemy_turn` |
| US-051 | Arrival ties: casts before turns, then lower entry id | §2.4 | 🟡 | none |
| US-052 | Wave spawn stagger: random 0–250 IP head start | §2.8 | 🟡 | none |
| US-053 | Two lanes: enemy turn icons above, cast icons below, overlap lanes | §2.6 | 🟡 | smoke screenshots (visual, unasserted) |
| US-054 | Countdown badge "acts in N" + panel line | §2.10 | 🟡 | smoke screenshots (visual, unasserted) |
| US-055 | Optional 4 s drag timer (Settings) auto-drops the drag | §2.9 | 🟡 | none |
| US-056 | Frozen halts the owner's turn and cast icons | §2.2 | ✅ | `Frozen_halts_the_enemy_icon_and_expires_into_wet` |
| US-057 | Slowed halves the timeline rate | §8.1 | 🟡 | none |

## Mana

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-070 | One shared 12-orb bank; overflow fades | §3.1.4 | ✅ | `Bank_overflow_fades_extra_orbs` |
| US-071 | Each pincer attacker mints its class color | §3.1.2 | ✅ | `Worked_example_1_4_slide_then_pincer` (2 pincer orbs) |
| US-072 | Each supporter mints its own color | §3.1.2 | ✅ | `Supporters_join_and_mint_their_own_orb` |
| US-073 | Colorless wild covers a colored shortfall; explicit C only by C | §3.1.6 | ✅ | `Colorless_wild_orb_covers_a_colored_shortfall` |
| US-074 | Spend takes the leftmost orb of each color | §3.1.5 | ✅ | `Instant_spell_spends_exact_orbs_damages_and_applies_buff` |
| US-075 | Hero critical hit mints a Colorless orb | §3.1.2 | 🟡 | none |
| US-076 | Steal/Mug roll (LCK + ½AGI)/50 → random orb | §3.1.2 | 🟡 | `Every_hero_ability_resolves_against_a_live_board` (resolves; roll not asserted) |
| US-077 | Cancelling an enemy charge mints the charge's color | §13.4 | 🟡 | `Enemy_charge_is_telegraphed_and_can_be_interrupted` (accepts stagger, cancel or death) |
| US-078 | Battle-start orbs from Mage Robes (2) / Wizard Robe (3) | §24.8 | ✅ | `Mage_robe_seeds_battle_start_orbs_and_gear_resistance_applies` |
| US-079 | Time-banked orbs: 1 Blue per 1.25 s left on the drag timer, max 3, at the next enemy turn | §3.1.8 | ✅ | `Drag_timer_remainder_banks_blue_orbs_at_the_enemy_handoff`, `Time_bank_carries_until_the_next_enemy_turn` |
| US-080 | Mana Potion / Ether / Hi-Ether grant 2 / 3 / 6 orbs | §4.3 | ✅ | `Every_consumable_has_a_working_battle_effect_or_is_camp_only` (asserts an orb mint) |
| US-081 | Orb letters + colorblind tint | §31.1 | 🟡 | none |
| US-082 | Bank capacity bonus gear knob | §3.1.4 | ⬜ | none |

## Abilities & spells

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-090 | Five slots; 2 on a fresh save, +1 at cleared index 0 / 2 / 5 | §4.7 | ✅ | `New_game_has_starting_trio_gold_and_loadouts`, `Full_loop_battle_commit_then_every_vendor` (≥ 3 after clears) |
| US-091 | Slot status precedence (CannotAct/Locked/Empty/OutOfCharges/Cooldown/Silenced/Unaffordable/CastCap/Ready) | §4.5 | 🟡 | Unaffordable, CastCap, Cooldown asserted; others not |
| US-092 | Shape × mode × filter resolver; every hero ability resolves on a live board | §5, GG-LAW-5 | ✅ | `Every_hero_ability_resolves_against_a_live_board` |
| US-093 | Spell orbs spent at cast start; no refund on interrupt | §4.4 | ✅ | `Cast_time_spell_rides_timeline_then_resolves` (bank 0 after queue) |
| US-094 | Cast-time spells ride the timeline and resolve on arrival (WIS+INT scaling) | §2.6, §13.1.4 | ✅ | `Cast_time_spell_rides_timeline_then_resolves` (scaling not asserted) |
| US-095 | Cast cap: 4 hero casts in flight | §2.6 | ✅ | `Fifth_hero_cast_is_refused_by_cast_cap` |
| US-096 | Cast stagger: WIS shrug, STR/WIS delay, cancel at ≥ cast time | §13.4 | 🟡 | `Enemy_charge_is_telegraphed_and_can_be_interrupted` (weak) |
| US-097 | Clutch (LCK/200, cap 20%) resolves a hero cast on the spot | §13.4 | ✅ | `Clutch_instantly_resolves_a_hero_cast_when_rolled` |
| US-098 | Skills are free and lock for CooldownTurns (ticked per action) | §4.1.1 | ✅ | `Teleport_relocates_and_fires_pincer_then_goes_on_cooldown` (set; ticking not asserted) |
| US-099 | Item stacks draw from inventory; uses deducted at commit (all outcomes) | §4.3 | ✅ | `Potion_item_heals_and_consumes_a_charge`, `Smoke_bomb_escapes_and_keeps_wounds` |
| US-100 | Consumable effects: heal, damage + bonus-vs-tag, orbs, cures, revive, escape | §4.3 | ✅ | `Every_consumable_has_a_working_battle_effect_or_is_camp_only` |
| US-101 | Sleep Dart casts Sleep without mana | §24.8 | ✅ | `Every_consumable_has_a_working_battle_effect_or_is_camp_only` |
| US-102 | Phoenix Down revives the first fallen hero; refused if none | §4.3 | 🟡 | catalog test (revive path); refusal not asserted |
| US-103 | Smoke Bomb escapes: no rewards, wounds kept | §22.2 | ✅ | `Smoke_bomb_escapes_and_keeps_wounds` |
| US-104 | Teleport: empty tile, relocate, pincer scan, cooldown 3 | §5.3 | ✅ | `Teleport_relocates_and_fires_pincer_then_goes_on_cooldown` |
| US-105 | Fireball 18 Fire + Burning | §7 | ✅ | `Instant_spell_spends_exact_orbs_damages_and_applies_buff` (Mini content) |
| US-106 | Full §7 catalog rows (Heal … CrossHit, Steal, Mug) resolve | §7 | ✅ | `Every_hero_ability_resolves_against_a_live_board` |
| US-107 | Scan reveals HP/stats and marks the class Scanned | §6, §15.3 | 🟡 | resolves only |
| US-108 | Silence blocks hero spell slots | §8.1 | 🟡 | status path exists; the "silenced" half of the test is not exercised |
| US-109 | Silence stops enemy charges | §7 | ✅ | `Silenced_caster_does_not_start_a_charge` |
| US-110 | Mug deals one 10-damage hit | §7 | ✅ | `Mug_steals_and_hits_exactly_once_per_target` |
| US-111 | Targeting: valid anchors marked, AoE preview, release commits, re-tap/Back cancels | §5.2 | 🟡 | none |
| US-112 | Ability card in the actor panel while targeting | §4.5 | 🟡 | none |
| US-113 | Long-press ability tooltips + radial cooldown sweep | §4.5 | ⬜ | none |
| US-114 | Distinct Homing / Spiral / Twist / Strike projectile curves | §6.1 | ⬜ | none |
| US-115 | Spell damage unified with the hit/crit roll | §13.1.2 | ⬜ | none |
| US-116 | Ability-bar weapon swap | §24.5 | ⬜ | none |
| US-117 | Distinct kits + colors for the 55 default-kit hero classes | §23.2.2 | ⬜ | none |

## Buffs

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-140 | Refresh-on-reapply for every buff | §8.6 | 🟡 | none |
| US-141 | Burning: 4/tick for 5 ticks, expires to Warm | §8.1 | 🟡 | none |
| US-142 | Frozen: immobile, halts timeline, expires to Wet | §8.1 | ✅ | `Frozen_halts_the_enemy_icon_and_expires_into_wet` |
| US-143 | Lightning on Wet ×1.5 ("Shock!") | §8.2 | ✅ | `Lightning_on_wet_hits_harder_and_fire_on_wet_makes_steam` |
| US-144 | Fire on Wet strips Wet ("Steam!") | §8.2 | ✅ | `Lightning_on_wet_hits_harder_and_fire_on_wet_makes_steam` |
| US-145 | Fire on Frozen thaws (chain) then steams off the Wet | §8.2.1 | 🟡 | none |
| US-146 | Sleep on Warm lasts ×1.5 | §8.2 | 🟡 | none |
| US-147 | Sleep: immobile, skips turns, breaks on damage | §8.2.3 | 🟡 | `Sleep_breaks_on_damage` (assert is conditional on a landing hit) |
| US-148 | Sleep breaks on displacement / boss shove | §8.2.3 | 🟡 | none |
| US-149 | Poisoned: 3/tick for 6 ticks | §8.1 | 🟡 | none |
| US-150 | Blinded: hit chance ×0.5; Lightning 30% blind roll | §8.1 | 🟡 | none |
| US-151 | Protection: incoming damage ×0.85 | §8.1 | 🟡 | applied (`Shield_…`); reduction not asserted |
| US-152 | Turn-buff tick points: heroes per action; enemies end of own turn; Frozen at the shared boundary | §8.3 | 🟡 | `Frozen_halts_the_enemy_icon_and_expires_into_wet` covers only the Frozen boundary |
| US-153 | Tick-unit buffs: 1 step per 10 clock ticks; DOT is environmental | §8.3 | 🟡 | none |
| US-154 | Cleanse (Antidote spell, Remedy) and single cures (Antidote item, Eye Drops) | §8.8 | ✅ | `Every_consumable_has_a_working_battle_effect_or_is_camp_only` (items); spell resolves |
| US-155 | Up to 3 letter pips per unit, buff-colored | §8.5 | 🟡 | smoke screenshots (visual) |
| US-156 | Radial countdown ring + >3 buff cycling | §8.5 | ⬜ | none |
| US-157 | Stack / Ignore / Upgrade reapply policies | §8.6 | ⬜ | none |
| US-158 | Frozen lasts beyond the action it lands in | §8.3 | ✅ | `Frozen_costs_the_enemy_a_full_cycle_before_thawing` |

## AI

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-170 | Target pick: distance + 8·HP fraction − INT-scaled threat | §14.1.2 | 🟡 | soak only |
| US-171 | Step scoring: advance, +2 adjacent, −100 sandwich, −0.5 stay | §14.1.1 | ✅ | `Enemy_never_steps_into_a_flank` (sandwich), soak |
| US-172 | Humanoid pincer-seek +50 | §14.1.2 | ✅ | `Humanoid_enemy_seeks_a_pincer_on_a_hero` |
| US-173 | Support an ally's pincer +25 | §14.3 | 🟡 | soak only |
| US-174 | Wounded (< 30% HP) retreat | §14.1.2 | ✅ | `Wounded_enemy_retreats_from_its_target` |
| US-175 | Immobile enemies skip their turn | §8.4 | ✅ | `Immobile_enemy_does_not_move` |
| US-176 | Caster telegraphs a charge when no hero is adjacent | §13.4 | ✅ | `Enemy_charge_is_telegraphed_and_can_be_interrupted`, `Every_enemy_charge_telegraphs_and_resolves` |
| US-177 | Single-target charges lock onto the hero; magic roll on resolve | §13.4 | ✅ | `Every_enemy_charge_telegraphs_and_resolves` (resolves) |
| US-178 | Line charges lock a cardinal line; heroes still on it are hit | §13.4 | ✅ | `Line_charge_hits_heroes_on_the_line_or_reports_a_dodge` (no enemy authored with Line yet) |
| US-179 | Trap-layer: 45% when no hero adjacent, free cardinal tile | §14.2 | ✅ | `Trap_layer_arms_a_tile_and_a_hero_stepping_on_it_triggers_it` |
| US-180 | MoveRange > 1 re-plans each step | §14.1 | 🟡 | none (all content MoveRange 1) |
| US-181 | Ranged archetype: keep distance + ranged attack | §14.2 | ⬜ | none |
| US-182 | Mechanical archetype: authored status immunities | §14.2 | ⬜ | none (no class authors resistances) |

## Bosses

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-190 | Cyclops00 is a 2×2 boss in the campaign | §1.5 | 🟡 | rules ✅ via Mini boss test; content only soak |
| US-191 | Boss phases from data at the start of the boss's turn; Cyclops enrages ≤ 50% (self-Hasten 0.35) | §14.3 | ✅ | `Boss_phase_fires_once_when_hp_crosses_threshold` |
| US-192 | Phase `PrefersCharge` knob | §14.3 | 🟡 | built; unused by content; no test |
| US-193 | Snake: head grows 3 segments; segments follow the trail; no icon | §14.2 | ✅ | `Snake_body_follows_the_head_and_is_armored_tail_first` |
| US-194 | Snake tail-first armor ("Armored!", 0 damage) | §14.2 | ✅ | `Snake_body_follows_the_head_and_is_armored_tail_first` |
| US-195 | Snake members are immovable to drags | §14.2 | 🟡 | soak only |
| US-196 | Boss turn icon purple border | §2.6 | 🟡 | visual only |

## Meta & Camp services

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-210 | New game: trio, 200 G, 3 of each starting bar item, 2 slots | §22, §25 | ✅ | `New_game_has_starting_trio_gold_and_loadouts` |
| US-211 | Three save slots, JSON, atomic write, version + migration | §15 | ✅ | `Save_round_trips_through_json`; smoke (New Game → Slot 1) |
| US-212 | Battle setup from save (levels, HP, gear, bar, inventory stacks, recommended level) | §22.1 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-213 | Victory commit: gold, XP to every party hero, items, kills, unlocks, bounty | §22.1 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-214 | Defeat / retreat: nothing gained, party restored to full | §22.2 | ✅ | `Defeat_resets_wounds_and_grants_nothing` |
| US-215 | Escape: nothing gained, wounds kept | §22.2 | ✅ | `Smoke_bomb_escapes_and_keeps_wounds` |
| US-216 | Wounds carry on victory; fallen → 1 HP; level-up → full | §22.1 | 🟡 | none |
| US-217 | XP curve 50 + 10·L²; level derived from TotalXp | §15.1 | 🟡 | `Full_loop_…` logs level-ups; curve not asserted |
| US-218 | Linear unlock, open backtrack, replayable stages | §22.3 | ✅ | `Full_loop_battle_commit_then_every_vendor` (IsStageUnlocked, HighestClearedStage ≥ 2) |
| US-219 | Enemy level floored to campaign index + 1 (tests exempt) | §22.3 | 🟡 | soak passes the parameter; not asserted |
| US-220 | Merchant: separate buy/sell carts, stack cap, 50% sell price | §25.1 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-221 | Forge recipes | §24.6 | ✅ | `Every_recipe_is_craftable_with_its_inputs`, `Full_loop_…` |
| US-222 | Upgrade recipes (base item consumed) | §24.6 | ✅ | `Every_recipe_is_craftable_with_its_inputs` |
| US-223 | Alchemy brew recipes | §24.6 | ✅ | `Every_recipe_is_craftable_with_its_inputs` |
| US-224 | Enchant recipes | §24.6 | ✅ | `Every_recipe_is_craftable_with_its_inputs` |
| US-225 | Salvage: 50% of the base forge recipe's inputs | §24.6 | 🟡 | none |
| US-226 | Repair: restore to max − repairs, cost ×1.6 per repair, uneconomical flag | §24.5 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-227 | Inn / Alchemist Heal: 0.5 G per missing HP, whole roster | §25.3.1 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-228 | Use potions on a hero and a Tent on the roster at Camp | §25.3.2 | ✅ | `Camp_potion_heals_a_wounded_hero_and_tent_heals_everyone` |
| US-229 | Equip / unequip, slot fit, stat compare | §24.2 | ✅ | `Full_loop_battle_commit_then_every_vendor` (equip; compare not asserted) |
| US-230 | Durability resets on unequip (documented simplification) | §24.5 | 🟡 | none |
| US-231 | Learn abilities for gold: class list + spell catalog; 120 + 80/orb, skills 300 | §25.6 | ✅ | `Full_loop_battle_commit_then_every_vendor` (learn; price formula not asserted) |
| US-232 | Assign: Known abilities or consumables only, locked refused, duplicates move | §25.6 | ✅ | `Full_loop_battle_commit_then_every_vendor` (assign) |
| US-233 | Party toggle, cap 4, last member kept | §23.4 | ✅ | `Full_loop_battle_commit_then_every_vendor` (cap) |
| US-234 | Summon: pool of 7, price 250 + 250·recruits, no duplicates | §25.10 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-235 | Recruits arrive at roster-average XP and auto-join if room | §25.10 | 🟡 | none |
| US-236 | Bounty: one active, kills on victory, claim gold + item | §22.4 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-237 | Bounty abandon; claimed bounties never repost | §22.4 | 🟡 | none |
| US-238 | Bestiary: Seen / Scanned always, Defeated on victory | §15.3 | 🟡 | `Save_round_trips_through_json` (data only) |
| US-239 | Content loads and every cross-reference resolves | §4.4, GG-LAW-10 | ✅ | `Real_content_loads_and_cross_references_resolve` |
| US-240 | Every class instantiable with a thumb | §12.1 | ✅ | `Every_class_can_be_instantiated_and_has_art_reference` |
| US-241 | Campaign pacing instrument (bots of 3 skills, with/without Camp routine) | §0.3 | ✅ | `BalanceReport.Campaign_pacing_table` (greedy bot finishes the campaign) |
| US-242 | Camp auto-manage routine (repair, rest, forge, equip, learn, slot, restock) | §25 | 🟡 | used by `BalanceReport`; no UI entry, no direct assert |
| US-243 | Armor wears in battle | §24.5 | ⬜ | none |
| US-244 | Relic / Epic+ named passives | §24.1.1 | ⬜ | none |
| US-245 | Inventory cap | §29 | ⬜ | open question |
| US-246 | Per-instance equipment durability | §24.5 | ⬜ | open question |

## UI screens

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-260 | Code-built boot: empty scene, GameApp builds panel, stage, router | §11.1 | ✅ | smoke (01-title) |
| US-261 | Title: Continue / New Game / Load Game / Credits | §11.2 | ✅ | smoke (New Game → Slot 1) |
| US-262 | Save slots: empty/filled rows, overwrite confirm | §15.2 | 🟡 | smoke (empty slot only) |
| US-263 | Camp hub: party window, menu, gold/time/cleared | §25.0 | ✅ | smoke (02-hub), sweep |
| US-264 | Adventure: unlocked newest-first, one locked teaser row, drops hint, bounty strip | §22.3 | 🟡 | smoke + sweep render it; ordering/teaser not asserted |
| US-265 | Story crawl on first entry into a theme (also via Next stage), skip by tap/button | §27 | ✅ | smoke (crawl detected, Skip) |
| US-266 | Battle HUD layout (top bar, timeline + GUARD, banner, board, bar, orbs, panel) | §9 | 🟡 | smoke screenshots (visual) |
| US-267 | Drag with live displacement + pincer preview, hero follows the finger | §1.1 | 🟡 | `artifacts/run1` captures (ad-hoc, not a harness command) |
| US-268 | Pause menu: Resume, Auto battle, How to play, Retreat | §22.2 | 🟡 | none (smoke toggles autoplay via endpoint) |
| US-269 | Auto battle (HeroBot plays visibly) | §4.3 | ✅ | smoke (battle completes on autoplay) |
| US-270 | First-battles hint: after 5 s idle, show the bot's pincer move | §29 | 🟡 | none |
| US-271 | Post-battle results; Retry / Continue / Next stage | §22 | ✅ | smoke (07-results) |
| US-272 | Items screen (tabs, detail, use at Camp) | §25 | ✅ | sweep |
| US-273 | Status screen | §25 | ✅ | sweep |
| US-274 | Equip screen | §25.4 | ✅ | sweep |
| US-275 | Abilities screen (Known / Items / Learn) | §25.6 | ✅ | sweep |
| US-276 | Party screen | §25.5 | ✅ | sweep |
| US-277 | Merchant screen | §25.1 | ✅ | sweep |
| US-278 | Blacksmith screen (Forge / Upgrade / Salvage / Repair) | §25.2 | ✅ | sweep |
| US-279 | Alchemist screen (Brew / Enchant / Heal) | §25.3 | ✅ | sweep |
| US-280 | Summon Circle screen | §25.10 | ✅ | sweep |
| US-281 | Bounties screen | §22.4 | ✅ | sweep |
| US-282 | Bestiary screen (silhouettes, scan-gated stats) | §25.11 | ✅ | sweep |
| US-283 | Settings screen (volumes, battle speed, drag timer, colorblind, reduce motion, how to play) | §31 | ✅ | sweep |
| US-284 | Credits screen | §12.0 | ✅ | sweep |
| US-285 | Back / Escape pops screens; battle Back cancels targeting then pauses | §11.3 | ✅ | sweep (`back` after every screen) |
| US-286 | Letterbox fit: never stretch | §26.3 | 🟡 | harness launches one aspect only; `resize` endpoint unused |
| US-287 | Safe-area insets | §26.5 | 🟡 | `GameApp.ApplySafeArea` built; needs a notched device |
| US-288 | Colorblind palette | §31.1 | 🟡 | none |
| US-289 | Reduce motion | §31.2 | 🟡 | none |
| US-290 | Battle speed ×1 / 1.5 / 2 / 3 | §12.0.2 | 🟡 | harness `speed` endpoint; unasserted |
| US-291 | Combat feed | §9 | 🟡 | 3 fading lines over the board (`BattleScreen.Feed`); no inline icons yet |
| US-292 | Actor panel: tap unit; HP/stats hidden until scanned | §9 | 🟡 | none |
| US-293 | Glyph coverage for every UI string | GG-LAW-11 | ✅ | `tools/check_glyphs.py` (in `tools/test.ps1`) |
| US-294 | Mute toggles in Settings | §31.5 | ⬜ | none (save fields exist) |

## Audio & VFX

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-300 | Music per screen (title, camp, battle, victory, defeat) | §12.0 | 🟡 | none |
| US-301 | Every battle event has an SFX cue | §12.0 | 🟡 | none |
| US-302 | Credits attribution for every track and pack | §12.0 | 🟡 | sweep renders Credits; "Melancholy Lull" + SFX packs origin unverified |
| US-303 | Effect cadence: every event is an awaited beat; watchdog | §12.0.2 | 🟡 | smoke completes a battle; not asserted per beat |
| US-304 | Particle VFX per spell (cast / projectile / impact / linger) | §6.2 | 🟡 | UI Toolkit hit sparks + projectile dots; no per-spell art |
| US-305 | HD art pass | §7 (V2) | ⬜ | none |

## Platform & ship

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-320 | Core + Meta compile and run without UnityEngine | GG-LAW-9 | ✅ | `dotnet test Tests/Core.Tests` |
| US-321 | Seeded deterministic RNG | GG-LAW-9 | ✅ | `Battles_replay_identically_from_seed` |
| US-322 | Bot soak over every stage; soft-lock detection | §6 | ✅ | `Bots_play_every_stage_without_breaking_invariants` |
| US-323 | Automation server (input injection, state, screenshots, layout audit) | §6 | ✅ | smoke, sweep |
| US-324 | Windows development build | §11.4 | ✅ | `Build/Windows/GridGameGaiden.exe` launched by smoke |
| US-325 | Windows release / Android / WebGL builds | §11.4 | 🟡 | Windows dev + Android APK built 2026-10-09 (APK untested on device); WebGL not built |
| US-326 | Unity batchmode compile check | §17.3 | 🟡 | `tools/test.ps1 -Compile` exists; not run this pass |
| US-327 | 60 fps target pinned; fps reported by automation `state` | §30 | 🟡 | none |
| US-328 | On-device profiling pass | §30.5 | ⬜ | none |
| US-329 | Portrait orientation, no splash, version from `VERSION` | §11.4 | 🟡 | `GaidenBuild.EnsureScene`; not asserted |
| US-330 | Verify or replace unverified audio before release | §12.0 | ⬜ | none |
| US-331 | Guided tutorial | §29 | ⬜ | open question (hints are US-270) |
| US-332 | Permadeath / NG+ | §29 | ⬜ | open question |

---

## Tally

| | ✅ | 🟡 | ⬜ |
|---|---|---|---|
| Battle core (32) | 19 | 12 | 1 |
| Timeline (18) | 9 | 9 | 0 |
| Mana (13) | 6 | 6 | 1 |
| Abilities & spells (28) | 13 | 8 | 7 |
| Buffs (19) | 4 | 12 | 3 |
| AI (13) | 2 | 9 | 2 |
| Bosses (7) | 0 | 7 | 0 |
| Meta & Camp (37) | 24 | 9 | 4 |
| UI screens (35) | 21 | 11 | 3 |
| Audio & VFX (6) | 0 | 4 | 2 |
| Platform & ship (13) | 5 | 4 | 4 |
| **Total (221)** | **103** | **91** | **27** |

Open design questions that gate future stories live in [BIBLE §29](BIBLE.md#29-open-design-questions).
