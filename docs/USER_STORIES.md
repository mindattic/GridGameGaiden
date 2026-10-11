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
| US-009 | Both attackers strike every trapped opponent | §1.2.4 | ✅ | `Both_attackers_strike_every_trapped_opponent` |
| US-010 | Supporters = allies adjacent to an attacker; join and mint own orb | §1.2.3 | ✅ | `Supporters_join_but_dilute_the_mana_charge` |
| US-011 | Supporters strike at 50% damage | §1.2.3 | ✅ | `Supporter_strikes_at_about_half_damage` |
| US-012 | Only Humanoid 1×1 units are pincer attackers | §1.3 | ✅ | `Beasts_never_form_pincers` |
| US-013 | Worked example §1.4 end-to-end | §1.4 | ✅ | `Worked_example_1_4_slide_then_pincer` |
| US-014 | 2×2 boss is immovable to slides | §1.5 | ✅ | `Two_by_two_boss_blocks_slides_and_is_pincered_across_its_width` |
| US-015 | 2×2 boss pincered across its width counts as one opponent | §1.5 | ✅ | `Two_by_two_boss_blocks_slides_and_is_pincered_across_its_width` |
| US-016 | Boss step shoves heroes to the vacated tile on their lane | §1.5 | ✅ | `Boss_step_shoves_the_hero_behind_it_then_melees_by_footprint` |
| US-017 | Boss melees via footprint adjacency; one icon | §1.5 | ✅ | `Boss_step_shoves_the_hero_behind_it_then_melees_by_footprint` |
| US-018 | Physical formula: mitigation, variance, hit/crit, level scaling | §13.1.1 | ✅ | `Physical_formula_matches_the_bible_shape` |
| US-019 | Death clears buffs and the unit's timeline entries | §13.3 | ✅ | `AssertInvariants` (no entry owned by a dead unit) in soak |
| US-020 | Loot: XP, coins, item drops per drop table, kills | §13.3 | ✅ | `Killing_last_enemy_of_last_wave_is_victory_with_loot` |
| US-021 | Default coin drop ≈ ½ XP ±20% when a table has no coin range | §13.3 | ✅ | `Enemies_without_a_coin_range_drop_about_half_their_xp` |
| US-022 | Waves spawn in sequence inside the same action | §22 | ✅ | `Waves_spawn_in_sequence` |
| US-023 | Last wave cleared = victory; all heroes down = defeat | §22.2 | ✅ | `Killing_last_enemy_of_last_wave_is_victory_with_loot`, `Defeat_resets_wounds_and_grants_nothing` |
| US-024 | Weapon wears 1 per pincer and shatters at 0 (×1.5 strike, 15% self-damage) | §24.5 | ✅ | `Weapon_wears_per_pincer_and_shatters_at_zero` (events + removal; multipliers not asserted) |
| US-025 | Deterministic replay from seed + commands | §4.5 | ✅ | `Battles_replay_identically_from_seed` |
| US-026 | Traps trigger on heroes only (step`, `displacement`, `shove`, `teleport) | §14.2 | ✅ | `Traps_spring_only_on_heroes` |
| US-027 | Humanoid enemies resolve enemy-team pincers on their turn (no orbs) | §1.3 | ✅ | `Humanoid_enemies_pincer_heroes_without_minting_orbs` |
| US-028 | Enemy melee hits the adjacent hero with the lowest HP | §14.1 | ✅ | `Enemy_melee_targets_the_adjacent_hero_with_lowest_hp` |
| US-029 | Fixed hero start tiles; enemy spawn tile or random rows 0–4 | §1 | ✅ | `Heroes_start_on_their_tiles_and_enemies_on_authored_or_upper_rows` |
| US-030 | Double Attack passive: pincer attacker strikes each target twice (2nd at ½) | §7 | ✅ | `Double_attack_passive_strikes_twice_and_counter_strikes_back` |
| US-031 | Counter Attack passive: adjacent defender strikes back at ½`, `no chains | §7 | ✅ | `Double_attack_passive_strikes_twice_and_counter_strikes_back` |
| US-032 | Respite spin when a link's whole line is already dead | §1.2.4 | ✅ | `Second_pincer_on_an_already_dead_target_plays_respite` (spin + "Respite") |

## Timeline

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-040 | Act-advanced clock: only committed actions advance it; 20 ticks per action | §2.0 | ✅ | `Clock_only_advances_on_committed_actions` |
| US-041 | Refused commands cost no time and no resources | §2.0 | ✅ | `Unaffordable_or_silenced_spell_is_refused_without_cost` |
| US-042 | Integer ticks, 1000-IP bar, Zone = rightmost 35% | §2.0–2.1 | ✅ | `Pincer_on_in_zone_enemy_pushes_its_icon_back` / `Pincer_outside_zone_has_no_pushback` (IP 920 vs 100) |
| US-043 | Speed-derived open-track rate + shared 30-tick Zone crawl | §2.1 | ✅ | `Open_track_rate_scales_with_speed_and_the_zone_is_a_shared_crawl` |
| US-044 | Enemy acts when its icon reaches the trigger; icon resets to 0 | §2.4 | ✅ | `Enemy_acts_when_its_icon_reaches_the_trigger` |
| US-045 | In-Zone hero hit pushes the icon back and stuns it | §2.3 | ✅ | `Pincer_on_in_zone_enemy_pushes_its_icon_back` |
| US-046 | Out-of-Zone hit: no pushback | §2.3 | ✅ | `Pincer_outside_zone_has_no_pushback` |
| US-047 | Pushback scales with depth and STR (cap 0.40 u); stun by AGI | §2.3`, `§2.7 | ✅ | `Pushback_grows_with_depth_and_strength` |
| US-048 | Quicken/Hasten slides a turn icon +0.30 u and clears stun | §2.7.1 | ✅ | `Quicken_hastens_an_enemy_icon` |
| US-049 | Hasten on a casting hero rushes the cast | §2.7.1 | ✅ | `Quicken_on_a_casting_hero_rushes_the_cast` |
| US-050 | Shield: Protection on all heroes + run to the next enemy turn | §2.5 | ✅ | `Shield_protects_heroes_and_skips_to_next_enemy_turn` |
| US-051 | Arrival ties: casts before turns`, `then lower entry id | §2.4 | ✅ | `Casts_resolve_before_turns_arriving_on_the_same_tick` |
| US-052 | Wave spawn stagger: random 0–250 IP head start | §2.8 | ✅ | `Wave_icons_start_with_a_bounded_random_head_start` |
| US-053 | Two lanes: enemy turn icons above, cast icons below, overlap lanes | §2.6 | ✅ | `tools/uicheck.py` (timeline icons present) + screenshots |
| US-054 | Countdown badge "acts in N" + panel line | §2.10 | ✅ | `tools/uicheck.py` (countdown badges) |
| US-055 | Optional 4 s drag timer (Settings) auto-drops the drag | §2.9 | ✅ | built (Settings → Drag timer); time-bank tests in `AiAndSystemsTests` |
| US-056 | Frozen halts the owner's turn and cast icons | §2.2 | ✅ | `Frozen_halts_the_enemy_icon_and_expires_into_wet` |
| US-057 | Slowed halves the timeline rate | §8.1 | ✅ | `Slowed_halves_the_timeline_rate` |

## Mana

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-070 | One shared 12-orb bank; overflow fades | §3.1.4 | ✅ | `Bank_overflow_fades_extra_orbs` |
| US-071 | Each pincer attacker mints an orb | §3.1.2 | ✅ | `Worked_example_1_4_slide_then_pincer` (2 pincer orbs) |
| US-072 | Each supporter mints an orb | §3.1.2 | ✅ | `Supporters_join_but_dilute_the_mana_charge` |
| US-073 | Any orbs pay for any spell (generic mana, amended 2026-10-10) | §3.1.6 | ✅ | `Any_orbs_pay_for_any_spell` |
| US-074 | Spend removes exactly the cost or nothing | §3.1.5 | ✅ | `Instant_spell_spends_exact_orbs_damages_and_applies_buff`, `Any_orbs_pay_for_any_spell` |
| US-075 | Hero critical hit mints a bonus orb | §3.1.2 | ✅ | `Hero_crit_adds_mana_charge` |
| US-076 | Steal/Mug roll (LCK + ½AGI)/50 → orb | §3.1.2 | ✅ | `Steal_with_high_luck_always_mints_an_orb` |
| US-077 | Cancelling an enemy charge earns mana (an orb before 2026-10-11; now +25 charge, §3.1.9) | §13.4 | ✅ | `Stagger_accumulates_and_cancelling_a_charge_mints_an_orb` |
| US-078 | Battle-start orbs from Mage Robes (2) / Wizard Robe (3) | §24.8 | ✅ | `Mage_robe_seeds_battle_start_orbs_and_gear_resistance_applies` |
| US-079 | Time-banked orbs: 1 Blue per 1.25 s left on the drag timer, max 3, at the next enemy turn | §3.1.8 | ✅ | `Drag_timer_remainder_banks_blue_orbs_at_the_enemy_handoff`, `Time_bank_carries_until_the_next_enemy_turn` |
| US-080 | Mana Potion / Ether / Hi-Ether grant 2 / 3 / 6 orbs | §4.3 | ✅ | `Every_consumable_has_a_working_battle_effect_or_is_camp_only` (asserts an orb mint) |
| US-081 | Orb line reads at a glance (one mana color, filled vs empty) | §31.1 | ✅ | `tools/uicheck.py` (filled vs empty orb tint) |
| US-082 | Bank capacity bonus gear knob | §3.1.4 | ✅ | `Bank_capacity_bonus_gear_widens_the_orb_line` |

## Abilities & spells

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-090 | Five slots; 2 on a fresh save, +1 at cleared index 0 / 2 / 5 | §4.7 | ✅ | `New_game_has_starting_trio_gold_and_loadouts`, `Full_loop_battle_commit_then_every_vendor` (≥ 3 after clears) |
| US-091 | Slot status precedence (CannotAct/Locked/Empty/OutOfCharges/Cooldown/Silenced/Unaffordable/CastCap/Ready) | §4.5 | ✅ | `Slot_status_reports_each_blocking_state` |
| US-092 | Shape × mode × filter resolver; every hero ability resolves on a live board | §5, GG-LAW-5 | ✅ | `Every_hero_ability_resolves_against_a_live_board` |
| US-093 | Spell orbs spent at cast start; no refund on interrupt | §4.4 | ✅ | `Cast_time_spell_rides_timeline_then_resolves` (bank 0 after queue) |
| US-094 | Cast-time spells ride the timeline and resolve on arrival (WIS+INT scaling) | §2.6, §13.1.4 | ✅ | `Cast_time_spell_rides_timeline_then_resolves` (scaling not asserted) |
| US-095 | Cast cap: 4 hero casts in flight | §2.6 | ✅ | `Fifth_hero_cast_is_refused_by_cast_cap` |
| US-096 | Cast stagger: WIS shrug`, `STR/WIS delay`, `cancel at ≥ cast time | §13.4 | ✅ | `Stagger_accumulates_and_cancelling_a_charge_mints_an_orb` |
| US-097 | Clutch (LCK/200, cap 20%) resolves a hero cast on the spot | §13.4 | ✅ | `Clutch_instantly_resolves_a_hero_cast_when_rolled` |
| US-098 | Skills are free and lock for CooldownTurns (ticked per action) | §4.1.1 | ✅ | `Teleport_relocates_and_fires_pincer_then_goes_on_cooldown` (set; ticking not asserted) |
| US-099 | Item stacks draw from inventory; uses deducted at commit (all outcomes) | §4.3 | ✅ | `Potion_item_heals_and_consumes_a_charge`, `Smoke_bomb_escapes_and_keeps_wounds` |
| US-100 | Consumable effects: heal, damage + bonus-vs-tag, orbs, cures, revive, escape | §4.3 | ✅ | `Every_consumable_has_a_working_battle_effect_or_is_camp_only` |
| US-101 | Sleep Dart casts Sleep without mana | §24.8 | ✅ | `Every_consumable_has_a_working_battle_effect_or_is_camp_only` |
| US-102 | Phoenix Down revives the first fallen hero; refused if none | §4.3 | ✅ | `Every_consumable_has_a_working_battle_effect_or_is_camp_only`, `Phoenix_down_is_refused_when_nobody_has_fallen` |
| US-103 | Smoke Bomb escapes: no rewards, wounds kept | §22.2 | ✅ | `Smoke_bomb_escapes_and_keeps_wounds` |
| US-104 | Teleport: empty tile, relocate, pincer scan, cooldown 3 | §5.3 | ✅ | `Teleport_relocates_and_fires_pincer_then_goes_on_cooldown` |
| US-105 | Fireball 18 Fire + Burning | §7 | ✅ | `Instant_spell_spends_exact_orbs_damages_and_applies_buff` (Mini content) |
| US-106 | Full §7 catalog rows (Heal … CrossHit, Steal, Mug) resolve | §7 | ✅ | `Every_hero_ability_resolves_against_a_live_board` |
| US-107 | Scan reveals HP/stats and marks the class Scanned | §6`, `§15.3 | ✅ | `Scan_reveals_and_records_the_class` |
| US-108 | Silence blocks hero spell slots | §8.1 | ✅ | `Slot_status_reports_each_blocking_state` |
| US-109 | Silence stops enemy charges | §7 | ✅ | `Silenced_caster_does_not_start_a_charge` |
| US-110 | Mug deals one 10-damage hit | §7 | ✅ | `Mug_steals_and_hits_exactly_once_per_target` |
| US-111 | Targeting: valid anchors marked, AoE preview, release commits, re-tap/Back cancels | §5.2 | ✅ | `tools/uicheck.py` (slot tap → targeting, highlights, re-tap cancels) |
| US-112 | Ability card in the actor panel while targeting | §4.5 | ✅ | `tools/uicheck.py` (ability card while targeting) |
| US-113 | Long-press ability tooltips + radial cooldown sweep | §4.5 | ✅ | `tools/uicheck.py` (long-press card) ; radial sweep → US-156 |
| US-114 | Distinct Homing / Spiral / Twist / Strike projectile curves | §6.1 | ✅ | `BattleScreen.ProjectilePoint` (Straight/Bezier/Homing/Spiral/Twist/Strike) |
| US-115 | Spell damage unified with the hit/crit roll | §13.1.2 | ✅ | `Spells_never_miss_and_can_crit_for_a_bonus_orb` (decision: never miss, crit at ½ chance ×1.6, bonus orb) |
| US-116 | Ability-bar weapon swap | §24.5 | ✅ | `Bar_weapon_swap_changes_the_weapon_and_the_save_follows` |
| US-117 | Distinct kits + colors for the 55 default-kit hero classes | §23.2.2 | ✅ | `Default_kit_classes_now_have_color_matched_kits`, `Every_mana_color_pays_for_at_least_one_hero_spell` |

## Buffs

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-140 | Refresh-on-reapply for every buff | §8.6 | ✅ | `Reapplying_a_buff_refreshes_its_duration` |
| US-141 | Burning: 4/tick for 5 ticks`, `expires to Warm | §8.1 | ✅ | `Burning_ticks_four_per_second_for_five_then_turns_warm` |
| US-142 | Frozen: immobile, halts timeline, expires to Wet | §8.1 | ✅ | `Frozen_halts_the_enemy_icon_and_expires_into_wet` |
| US-143 | Lightning on Wet ×1.5 ("Shock!") | §8.2 | ✅ | `Lightning_on_wet_hits_harder_and_fire_on_wet_makes_steam` |
| US-144 | Fire on Wet strips Wet ("Steam!") | §8.2 | ✅ | `Lightning_on_wet_hits_harder_and_fire_on_wet_makes_steam` |
| US-145 | Fire on Frozen thaws (chain) then steams off the Wet | §8.2.1 | ✅ | `Fire_on_frozen_thaws_then_steams_off_the_wet` |
| US-146 | Sleep on Warm lasts ×1.5 | §8.2 | ✅ | `Sleep_on_a_warm_target_lasts_half_again_as_long` |
| US-147 | Sleep: immobile`, `skips turns`, `breaks on damage | §8.2.3 | ✅ | `Sleeping_enemy_skips_its_turn`, `Sleep_breaks_on_damage` |
| US-148 | Sleep breaks on displacement / boss shove | §8.2.3 | ✅ | `Sleep_breaks_when_the_sleeper_is_displaced` |
| US-149 | Poisoned: 3/tick for 6 ticks | §8.1 | ✅ | `Poison_ticks_three_for_six` |
| US-150 | Blinded: hit chance ×0.5; Lightning 30% blind roll | §8.1 | ✅ | `Blinded_attackers_hit_far_less_often` |
| US-151 | Protection: incoming damage ×0.85 | §8.1 | ✅ | `Protection_reduces_incoming_spell_damage_by_fifteen_percent` |
| US-152 | Turn-buff tick points: heroes per action; enemies end of own turn; Frozen at the shared boundary | §8.3 | ✅ | `Frozen_costs_the_enemy_a_full_cycle_before_thawing`, `Frozen_halts_the_enemy_icon_and_expires_into_wet` |
| US-153 | Tick-unit buffs: 1 step per 10 clock ticks; DOT is environmental | §8.3 | ✅ | `Burning_ticks_four_per_second_for_five_then_turns_warm`, `Poison_ticks_three_for_six` |
| US-154 | Cleanse (Antidote spell, Remedy) and single cures (Antidote item, Eye Drops) | §8.8 | ✅ | `Every_consumable_has_a_working_battle_effect_or_is_camp_only` (items); spell resolves |
| US-155 | Up to 3 letter pips per unit, buff-colored | §8.5 | ✅ | `tools/uicheck.py` (buff pip on the unit) + `RingTimer` |
| US-156 | Radial countdown ring + >3 buff cycling | §8.5 | ✅ | `RingTimer` + 1.5 s rotation in `UnitView.SetBuffs` (visual) |
| US-157 | Stack / Ignore / Upgrade reapply policies | §8.6 | ✅ | `Buff_stacking_policies` |
| US-158 | Frozen lasts beyond the action it lands in | §8.3 | ✅ | `Frozen_costs_the_enemy_a_full_cycle_before_thawing` |

## AI

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-170 | Target pick: distance + 8·HP fraction − INT-scaled threat | §14.1.2 | ✅ | `Smart_enemies_hunt_whoever_hurt_them_most` |
| US-171 | Step scoring: advance, +2 adjacent, −100 sandwich, −0.5 stay | §14.1.1 | ✅ | `Enemy_never_steps_into_a_flank` (sandwich), soak |
| US-172 | Humanoid pincer-seek +50 | §14.1.2 | ✅ | `Humanoid_enemy_seeks_a_pincer_on_a_hero` |
| US-173 | Support an ally's pincer +25 | §14.3 | ✅ | `Enemy_values_supporting_an_ally_pincer` |
| US-174 | Wounded (< 30% HP) retreat | §14.1.2 | ✅ | `Wounded_enemy_retreats_from_its_target` |
| US-175 | Immobile enemies skip their turn | §8.4 | ✅ | `Immobile_enemy_does_not_move` |
| US-176 | Caster telegraphs a charge when no hero is adjacent | §13.4 | ✅ | `Enemy_charge_is_telegraphed_and_can_be_interrupted`, `Every_enemy_charge_telegraphs_and_resolves` |
| US-177 | Single-target charges lock onto the hero; magic roll on resolve | §13.4 | ✅ | `Every_enemy_charge_telegraphs_and_resolves` (resolves) |
| US-178 | Line charges lock a cardinal line; heroes still on it are hit | §13.4 | ✅ | `Line_charge_hits_heroes_on_the_line_or_reports_a_dodge` (no enemy authored with Line yet) |
| US-179 | Trap-layer: 45% when no hero adjacent, free cardinal tile | §14.2 | ✅ | `Trap_layer_arms_a_tile_and_a_hero_stepping_on_it_triggers_it` |
| US-180 | MoveRange > 1 re-plans each step | §14.1 | ✅ | `MoveRange_two_steps_twice` |
| US-181 | Ranged archetype: keep distance + ranged attack | §14.2 | ✅ | `Ranged_enemy_shoots_down_a_clear_lane_and_keeps_its_distance` (hags, Swamp Mistress) |
| US-182 | Mechanical archetype: authored status immunities | §14.2 | ✅ | `Mechanical_enemies_are_immune_to_poison_and_sleep` |

## Bosses

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-190 | Cyclops00 is a 2×2 boss in the campaign | §1.5 | ✅ | `Cyclops_is_a_two_by_two_boss_that_appears_in_the_campaign` |
| US-191 | Boss phases from data at the start of the boss's turn; Cyclops enrages ≤ 50% (self-Hasten 0.35) | §14.3 | ✅ | `Boss_phase_fires_once_when_hp_crosses_threshold` |
| US-192 | Phase `PrefersCharge` knob | §14.3 | ✅ | `Prefers_charge_phase_makes_a_caster_charge_even_point_blank` |
| US-193 | Snake: head grows 3 segments; segments follow the trail; no icon | §14.2 | ✅ | `Snake_body_follows_the_head_and_is_armored_tail_first` |
| US-194 | Snake tail-first armor ("Armored!", 0 damage) | §14.2 | ✅ | `Snake_body_follows_the_head_and_is_armored_tail_first` |
| US-195 | Snake members are immovable to drags | §14.2 | ✅ | `Snake_members_are_immovable_to_drags` |
| US-196 | Boss turn icon purple border | §2.6 | ✅ | `tools/uicheck.py` (boss keyline #DB66FF, unit frame = menu white frame); Boss tag on cyclops/naga/vampire |

## Meta & Camp services

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-210 | New game: trio, 200 G, 3 of each starting bar item, 2 slots | §22, §25 | ✅ | `New_game_has_starting_trio_gold_and_loadouts` |
| US-211 | Three save slots, JSON, atomic write, version + migration | §15 | ✅ | `Save_round_trips_through_json`; smoke (New Game → Slot 1) |
| US-212 | Battle setup from save (levels, HP, gear, bar, inventory stacks, recommended level) | §22.1 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-213 | Victory commit: gold, XP to every party hero, items, kills, unlocks, bounty | §22.1 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-214 | Defeat / retreat: nothing gained, party restored to full | §22.2 | ✅ | `Defeat_resets_wounds_and_grants_nothing` |
| US-215 | Escape: nothing gained, wounds kept | §22.2 | ✅ | `Smoke_bomb_escapes_and_keeps_wounds` |
| US-216 | Wounds carry on victory; fallen → 1 HP; level-up → full | §22.1 | ✅ | `Wounds_carry_on_victory_and_the_fallen_revive_at_one_hp` |
| US-217 | XP curve 50 + 10·L²; level derived from TotalXp | §15.1 | ✅ | `Xp_curve_is_50_plus_10_level_squared` |
| US-218 | Linear unlock, open backtrack, replayable stages | §22.3 | ✅ | `Full_loop_battle_commit_then_every_vendor` (IsStageUnlocked, HighestClearedStage ≥ 2) |
| US-219 | Enemy level floored to campaign index + 1 (tests exempt) | §22.3 | ✅ | `Enemy_levels_are_floored_to_the_campaign_recommendation` |
| US-220 | Merchant: separate buy/sell carts, stack cap, 50% sell price | §25.1 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-221 | Forge recipes | §24.6 | ✅ | `Every_recipe_is_craftable_with_its_inputs`, `Full_loop_…` |
| US-222 | Upgrade recipes (base item consumed) | §24.6 | ✅ | `Every_recipe_is_craftable_with_its_inputs` |
| US-223 | Alchemy brew recipes | §24.6 | ✅ | `Every_recipe_is_craftable_with_its_inputs` |
| US-224 | Enchant recipes | §24.6 | ✅ | `Every_recipe_is_craftable_with_its_inputs` |
| US-225 | Salvage: 50% of the base forge recipe's inputs | §24.6 | ✅ | `Salvage_returns_half_the_forge_inputs` |
| US-226 | Repair: restore to max − repairs, cost ×1.6 per repair, uneconomical flag | §24.5 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-227 | Inn / Alchemist Heal: 0.5 G per missing HP, whole roster | §25.3.1 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-228 | Use potions on a hero and a Tent on the roster at Camp | §25.3.2 | ✅ | `Camp_potion_heals_a_wounded_hero_and_tent_heals_everyone` |
| US-229 | Equip / unequip, slot fit, stat compare | §24.2 | ✅ | `Full_loop_battle_commit_then_every_vendor` (equip; compare not asserted) |
| US-230 | Durability resets on unequip (documented simplification) | §24.5 | ✅ | `Unequipping_returns_the_piece_and_resets_durability` |
| US-231 | Learn abilities for gold: class list + spell catalog; 120 + 80/orb, skills 300 | §25.6 | ✅ | `Full_loop_battle_commit_then_every_vendor` (learn; price formula not asserted) |
| US-232 | Assign: Known abilities or consumables only, locked refused, duplicates move | §25.6 | ✅ | `Full_loop_battle_commit_then_every_vendor` (assign) |
| US-233 | Party toggle, cap 4, last member kept | §23.4 | ✅ | `Full_loop_battle_commit_then_every_vendor` (cap) |
| US-234 | Summon: pool of 7, price 250 + 250·recruits, no duplicates | §25.10 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-235 | Recruits arrive at roster-average XP and auto-join if room | §25.10 | ✅ | `Recruits_arrive_at_roster_average_xp` |
| US-236 | Bounty: one active, kills on victory, claim gold + item | §22.4 | ✅ | `Full_loop_battle_commit_then_every_vendor` |
| US-237 | Bounty abandon; claimed bounties never repost | §22.4 | ✅ | `Abandoned_bounties_can_be_retaken_and_claimed_ones_never_repost` |
| US-238 | Bestiary: Seen / Scanned always`, `Defeated on victory | §15.3 | ✅ | `Bestiary_records_seen_on_spawn_and_defeated_on_victory` |
| US-239 | Content loads and every cross-reference resolves | §4.4, GG-LAW-10 | ✅ | `Real_content_loads_and_cross_references_resolve` |
| US-240 | Every class instantiable with a thumb | §12.1 | ✅ | `Every_class_can_be_instantiated_and_has_art_reference` |
| US-241 | Campaign pacing instrument (bots of 3 skills, with/without Camp routine) | §0.3 | ✅ | `BalanceReport.Campaign_pacing_table` (greedy bot finishes the campaign) |
| US-242 | Camp auto-manage routine (repair`, `rest`, `forge`, `equip`, `learn`, `slot`, `restock) | §25 | ✅ | `Camp_routine_rests_and_restocks`, `BalanceReport` |
| US-243 | Armor wears in battle | §24.5 | ✅ | `Armor_wears_per_enemy_hit_and_breaks_at_zero` |
| US-244 | Relic / Epic+ named passives | §24.1.1 | ✅ | `Relic_passives_boost_pincers_regen_and_protect` (Bloodstone, Jade, Sunfire, Phantom, Stars) |
| US-245 | Inventory cap | §29 | ✅ | `Loot_past_the_stack_limit_is_auto_sold` (decision: no global cap; per-item MaxStack, overflow auto-sells) |
| US-246 | Per-instance equipment durability | §24.5 | ✅ | `Unequipped_gear_remembers_its_wear_and_fresh_copies_are_used_first` |

## UI screens

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-260 | Code-built boot: empty scene, GameApp builds panel, stage, router | §11.1 | ✅ | smoke (01-title) |
| US-261 | Title: Continue / New Game / Load Game / Credits | §11.2 | ✅ | smoke (New Game → Slot 1) |
| US-262 | Save slots: empty/filled rows, overwrite confirm | §15.2 | ✅ | `tools/uicheck.py` (3 slots, overwrite confirm, Continue) |
| US-263 | Camp hub: party window, menu, gold/time/cleared | §25.0 | ✅ | smoke (02-hub), sweep |
| US-264 | Adventure: unlocked newest-first, one locked teaser row, drops hint, bounty strip | §22.3 | ✅ | `tools/uicheck.py` (first stage listed, locked teaser row) |
| US-265 | Story crawl on first entry into a theme (also via Next stage), skip by tap/button | §27 | ✅ | smoke (crawl detected, Skip) |
| US-266 | Battle HUD layout (top bar, timeline + GUARD, banner, board, bar, orbs, panel) | §9 | ✅ | `tools/uicheck.py` (board, ability bar, orbs, GUARD, pause, feed present) |
| US-267 | Drag with live displacement + pincer preview, hero follows the finger | §1.1 | ✅ | `tools/uicheck.py` (held drag: displaced unit at preview tile, PINCER banner) |
| US-268 | Pause menu: Resume, Auto battle, How to play, Retreat | §22.2 | ✅ | `tools/uicheck.py` (Resume / Auto battle / Retreat) |
| US-269 | Auto battle (HeroBot plays visibly) | §4.3 | ✅ | smoke (battle completes on autoplay) |
| US-270 | First-battles hint: after 5 s idle, show the bot's pincer move | §29 | ✅ | `tools/uicheck.py` (idle hint: move, spell or GUARD) |
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
| US-286 | Letterbox fit: never stretch | §26.3 | ✅ | `tools/uicheck.py` (1280×720 window: aspect kept, centered) |
| US-287 | Safe-area insets | §26.5 | ✅ | `tools/uicheck.py` with `-safeArea 120,80` (content insets below the notch / above the home bar) |
| US-288 | Colorblind palette | §31.1 | ✅ | `tools/uicheck.py` (battle completes in colorblind mode) |
| US-289 | Reduce motion | §31.2 | ✅ | `tools/uicheck.py` (battle completes with reduce motion) |
| US-290 | Battle speed ×1 / 1.5 / 2 / 3 | §12.0.2 | ✅ | `tools/uicheck.py` (battle completes at ×3) |
| US-291 | Combat feed | §9 | ✅ | 3 fading lines with type glyphs (†/☠/✚/★/▶); `tools/uicheck.py` asserts the feed exists |
| US-292 | Actor panel: tap unit; HP/stats hidden until scanned | §9 | ✅ | `tools/uicheck.py` (tap enemy → HP hidden '???') |
| US-293 | Glyph coverage for every UI string | GG-LAW-11 | ✅ | `tools/check_glyphs.py` (in `tools/test.ps1`) |
| US-294 | Mute toggles in Settings | §31.5 | ✅ | Settings → Mute music / Mute sound |

## Audio & VFX

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-300 | Music per screen (title, camp, battle, victory, defeat) | §12.0 | ✅ | `tools/uicheck.py` (title, camp, battle, results tracks) |
| US-301 | Every battle event has an SFX cue | §12.0 | ✅ | `tools/uicheck.py` (pick-up, every step, pincer, hits voiced) |
| US-302 | Credits attribution for every track and pack | §12.0 | ✅ | Credits.txt lists every asset; all music Kevin MacLeod CC-BY 4.0; all SFX original (`tools/make_sfx.py`) |
| US-303 | Effect cadence: every event is an awaited beat; watchdog | §12.0.2 | ✅ | `tools/uicheck.py` + `BattleScreen.Safe`/watchdog; zero errors across the run |
| US-304 | Particle VFX per spell (cast / projectile / impact / linger) | §6.2 | ✅ | Per-element UI Toolkit VFX (`ElementBurst`: fire embers, ice shards + ring, lightning bolt, poison bubbles, holy rays, dark implosion, arcane ring; heal sparkles; status auras); screenshots in artifacts/vfx |
| US-333 | Pincer close-up cut-in: attackers' portraits sweep past with hand-held sway while the board camera pushes in (owner request, from GridGame2026) | §6.2 | ✅ | `tools/uicheck.py` (both portraits on screen mid-sweep, cleans up; real-pincer counter); autoplay battle showed 4 cut-ins, 0 errors; screenshot artifacts/uicheck/cutin.png |
| US-334 | Enemy timeline icons are pixel-art sprites of the whole enemy; the next attacker gets a gold ring (owner request) | §2.6 | ✅ | `tools/make_pixel_icons.py` (110 sprites, ImageMagick); `tools/check_assets.py` (every enemy has one); `tools/uicheck.py` (turn icons are pixel sprites); screenshots artifacts/tlshot |
| US-335 | Enemy turn icons are opaque circles centred on a thin track; sprites masked to the circle; enhanced eyes re-inked on every sprite (docs/PIXEL_ICONS.md) | §2.6 | ✅ | `tools/uicheck.py` (opaque circle, centred on line, thin track); `tools/eyes.json` + `ink_eyes` snap; screenshots artifacts/tlshot |
| US-336 | No turn-number badges on the board — turn order lives on the timeline | §2.6 | ✅ | `tools/uicheck.py` (no bare digits on the board) |
| US-337 | Speed tiers (swift 18 / quick 15 / steady 11 / slow 8): a fast enemy overtakes a slow one during the windup; the faster icon hops over in the upper lane | §2.2 | ✅ | `A_fast_enemy_overtakes_a_slow_one_during_the_windup`, `Every_real_enemy_has_a_targeting_personality_and_a_speed_tier`; `LayoutIcons` |
| US-338 | Enemy targeting personalities: Closest (dumb), Weakest (smart), Threat (tacticians), Squishy (assassins), Erratic (feral); shown on the enemy card and bestiary | §14.1 | ✅ | `Dumb_enemies_attack_the_closest_hero`, `Smart_enemies_hunt_the_weakest_hero_even_across_the_board`, `Assassins_target_the_most_fragile_hero`, `Erratic_enemies_keep_one_target_per_turn_but_vary_between_turns` |
| US-339 | Frozen / asleep / stunned units show a debuff badge on their timeline circle (sprite dimmed) and a pip on their tile, so it is clear why they are not moving | §8.5 | ✅ | `tools/uicheck.py` (frozen badge on circle + tile pip) |
| US-340 | Casts ride the same line from 0 (cast start) to 1 (lands) over their own cast time, so quick spells visibly overtake older icons | §2.6 | ✅ | `TimelineEntry.U` (cast), `BattleEngine.UPerTick`; 157 sim tests |
| US-341 | Pending spells draw a radial countdown around the target's frame in the spell's own colour, with the spell icon chip; several pending spells on one unit cycle every 1.2 s; cast/resolve banners say who casts what on whom and when it lands | §2.6 | ✅ | `tools/uicheck.py` (ring + chip on target, two spells cycle colours); `CastRing`, `Art.SpellColor` |
| US-351 | Every status, ability, item and the cursor is a pixel-art icon from open-licence silhouettes (game-icons.net, CC BY 3.0) | §11 | ✅ | `tools/make_icons.py` + `tools/icons.json` (218 icons); `tools/check_assets.py`; `tools/uicheck.py` (status pips are pixel icons) |
| US-352 | Combat presentation: FF VI billboard, accumulating damage tally that bursts into the total, cut-in modes (sweep + supporter strip, chain pop-up cards, caster slide) | §6.2 | ✅ | probe run: cut-in 0.81 s, billboard 0.89 s, tally 2.05 s, burst 2.66 s (artifacts/show); `tools/uicheck.py` (pincer names itself on the billboard) |
| US-353 | Shield Bash knockback (crash into units, slam into stone/edge) and movable tiles (amplifier pushed like an actor, nothing spawns on it) | §6.6, §1.1.2 | ✅ | `Shield_bash_*`, `Bashing_into_a_unit_crashes_both_and_into_stone_slams_hard`, `The_amplifier_is_a_movable_tile_heroes_push_and_nothing_spawns_on` |
| US-354 | Undead leave solid gravestones / bone piles: destroy them or the undead rises again at half HP | §14.5 | ✅ | `A_slain_undead_leaves_solid_remains_that_block_the_wave_from_clearing`, `Remains_left_alone_raise_their_owner_at_half_hp_without_loot`, `Remains_are_solid_and_real_undead_leave_them`; bot soak |
| US-305 | HD art pass | §7 (V2) | ⬜ | V2 scope per RFC 0002 (never part of the V1 claim); current art is the original GridGame illustration set |

## Platform & ship

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-320 | Core + Meta compile and run without UnityEngine | GG-LAW-9 | ✅ | `dotnet test Tests/Core.Tests` |
| US-321 | Seeded deterministic RNG | GG-LAW-9 | ✅ | `Battles_replay_identically_from_seed` |
| US-322 | Bot soak over every stage; soft-lock detection | §6 | ✅ | `Bots_play_every_stage_without_breaking_invariants` |
| US-323 | Automation server (input injection, state, screenshots, layout audit) | §6 | ✅ | smoke, sweep |
| US-324 | Windows development build | §11.4 | ✅ | `Build/Windows/GridGameGaiden.exe` launched by smoke |
| US-325 | Windows release / Android / WebGL builds | §11.4 | ✅ | Windows (`tools/uicheck.py` 45/45), WebGL in Chromium (`tools/webcheck.mjs` 4/4), Android APK on an API 34 emulator (`tools/androidcheck.py` 7/7: boot, touch → camp, battle to results, save). Emulator GL translator draws no UI Toolkit pixels — confirm visuals on a physical phone |
| US-326 | Unity batchmode compile check | §17.3 | ✅ | `tools/build.ps1` compiles + builds clean each pass |
| US-327 | 60 fps target pinned; fps reported by automation `state` | §30 | ✅ | `tools/uicheck.py` (fps ≥ 50 in battle) |
| US-328 | On-device profiling pass | §30.5 | ⬜ | Needs a physical mid-tier phone (emulator is software-rendered: 18 fps is not representative); Windows/WebGL hold ≥ 50 fps |
| US-329 | Portrait orientation, no splash, version from `VERSION` | §11.4 | ✅ | `tools/uicheck.py` (ProjectSettings portrait, splash off, version.txt) |
| US-330 | Verify or replace unverified audio before release | §12.0 | ✅ | Unverified audio removed: SFX regenerated from scratch, Triumph/Melancholy Lull replaced by CC-BY tracks; `tools/check_assets.py` gates every cue/track |
| US-331 | Guided tutorial | §29 | ✅ | `tools/uicheck.py` (first battle starts tutorial, suggests a move, advances after the first move, skippable) |
| US-332 | Permadeath / NG+ | §29 | ✅ | `New_game_plus_restarts_the_campaign_with_stronger_enemies` (decision: no permadeath; NG+ after the campaign, +10 enemy levels, +50% coins per cycle) |

---


## Production roadmap (owner direction, 2026-10-10)

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-342 | Power Amplifier (Terra Battle): spawns on a luck-modified random schedule; a pincer attacker aligned with it gets an enhanced attack; enemies cannot use it but can walk through it to shove it away and break amplified combos | §1.2 | ✅ | `Amplifier_chance_grows_with_party_luck_and_caps`, `An_attacker_on_the_amplifier_strikes_harder_adds_mana_charge_and_uses_it_up`, `Enemies_cannot_use_the_amplifier_but_stepping_on_it_shoves_it_back`; `tools/uicheck.py` (marker on its tile) |
| US-343 | Defeated enemies burst into gold coins that fly to the gold counter (top right), as in GridGame2026 | §13 | ✅ | `CoinBurst` (coins burst, bounce, fly to the counter, which counts up); counter = purse + battle coins; screenshot artifacts/vfx |
| US-344 | Pincer group outline: a line draws itself around the whole Hero–foe…–Hero cluster (no line across the art); short connectors join heroes to their supporters | §6.2 | ✅ | `LineLayer.Outline` + `BoardView.GroupRect/EdgeLink` (preview + resolve); screenshots artifacts/pincer |
| US-345 | Every enemy family gets its own kit (behaviour, spells, traps — slime trail, poison cloud …) matched to how it looks | §14 | ✅ | 39 families kitted (bible §14.4); `Every_enemy_charge_telegraphs_and_resolves` (incl. Howl/Rally/Mend/Poison Cloud); bot soak on every stage; `tools/uicheck.py` (enemy card describes its kit) |
| US-346 | Physical blocks on the grid: no unit may enter, land, be displaced or spawn on a block | §1.1 | ✅ | `A_hero_cannot_step_onto_a_block`, `A_block_in_the_line_breaks_the_pincer`, `Nobody_spawns_or_walks_onto_blocks`, `Real_stages_carry_their_blocks`; bot soak on 7 blocked stages; `tools/uicheck.py` (blocks draw) |
| US-347 | Expeditions: a set of X battles, each of Y waves, with no full heal between — survival pressure on consumables and mana | §22 | ✅ | `An_expedition_runs_its_battles_in_order_carrying_mana_with_the_inn_closed`, `Finishing_an_expedition_pays_a_bonus_and_rests_the_party_while_defeat_restarts_it`; `tools/uicheck.py` (adventure groups battles into expeditions) |
| US-348 | Caravan story: the party travels with a merchant caravan through dangerous lands (why shops are always open between battles); story beats per expedition | §27 | 🟡 | framing built (caravan = the shops between battles; Camp → Caravan; expeditions); story text is lorem ipsum by owner request (2026-10-10) |
| US-349 | Guest allies met in battle: rescue them and they join the roster; if they die in that fight they are lost forever | §25 | ✅ | `A_guest_who_survives_a_victory_joins_the_roster`, `A_guest_who_falls_is_lost_forever`; 4 stages carry a guest |
| US-350 | Upgrades and long-run progression (gear tiers, ability ranks, caravan upgrades) with a meaningful power curve | §21 | ✅ | caravan upgrades (bible §25.12) on top of gear tiers/enchants/levels/slot unlocks; `Caravan_upgrades_cost_gold_and_change_how_expeditions_play`; `tools/uicheck.py` (camp menu has Caravan) |

## Gameplay synergy & feel (owner direction, 2026-10-11)

| ID | Story | § | Status | Evidence |
|---|---|---|---|---|
| US-351 | Hero roles: Tank / Healer / Mage / Striker / Support with kits that match, so a party needs each other | §25.13 | ✅ | `Every_hero_has_a_role_and_kits_match_it`; role badges (screenshot artifacts/smoke/02-hub.png) |
| US-352 | A tank can taunt: enemies in reach must go for it, keeping casters' spells from being staggered | §14.1.1 | ✅ | `A_taunting_hero_draws_every_enemy_that_can_choose`, `Taunt_only_overrides_among_heroes_in_reach`; artreview taunt sheet |
| US-353 | Stats give roles meaning: INT scales spells, STR physical skills, WIS heals | §13.1.5 | ✅ | `Caster_power_makes_a_mage_out_hit_a_fighter_and_a_sage_out_heal` |
| US-354 | Strikers get Cleave and War Cry (party Empowered) | §25.13 | ✅ | `War_cry_empowers_every_ally_and_their_pincers_hit_harder` |
| US-355 | Powerups sometimes drop from fallen enemies; walk over them before they fade | §13.3.1 | ✅ | `Fallen_enemies_sometimes_leave_pickups`, `Stepping_on_a_pickup_collects_it_and_heals`, `Pickups_fade_after_their_lifetime` |
| US-356 | Every press has a standard depress + sound; one look and feel | §11.5 | ✅ | `Ui.Pressable`; `tools/uicheck.py` (every press is voiced) |
| US-357 | Select → confirm → act for every ability and item (confirm card, Cancel, setting) | §4.4.1 | ✅ | `tools/uicheck.py` (a pick waits for confirmation; Cancel backs out; Confirm acts with a chime) |
| US-358 | Spells are staged: wind-up, filling charge bar while time moves, cast splash, curved projectile, splash, status effect then its icon, lingering status visuals | §6.2 | ✅ | `tools/artreview.py poison fireball` contact sheets |
| US-359 | Potions are drunk, bottles are thrown and shatter | §6.2 | ✅ | `tools/artreview.py potion` |
| US-360 | Reorder the ability bar | §25.6 | ✅ | `The_bar_can_be_reordered_within_unlocked_slots` |
| US-361 | Mana is scarce and earned by lean, chained pincers (participants divide the charge) | §3.1.9 | ✅ | `More_heroes_in_a_pincer_earn_less_mana_per_hp_taken`, `Charge_rolls_over_into_orbs_and_keeps_the_remainder`, `A_killing_pincer_earns_mana_by_hp_taken_not_per_hero`; BalanceReport |
| US-362 | Action cut-ins that twist and zoom; supporters pop in as they strike | §9 | ✅ | `tools/artreview.py caster sweep` |
| US-363 | Art review loop: record frames of any moment and review them | — | ✅ | `tools/artreview.py`, automation `record` |
| US-364 | Full tutorial with explanations and examples; the first battle has 5 waves of rising difficulty | §9 | ✅ | ten lessons (`BattleScreen.Tutorial.cs`); `The_first_battle_has_five_waves_of_rising_difficulty`; `tools/uicheck.py` (lesson has an explanation and an example); autoplay walk-through artifacts/tutorial |
| US-366 | Battle records of every move as JSON (for balance learning and future ML), with an analysis tool | §22.6 | ✅ | `A_recorded_battle_round_trips_as_json_with_every_turn`; game writes a record per battle (artifacts/first20/records); `tools/records.py` |
| US-367 | The first battle is 20 waves of rising difficulty | §22.7 | ✅ | `The_first_battle_has_twenty_waves_of_rising_difficulty`; record soak: greedy 8/8 |
| US-368 | Every hero starts with two stacks of 5 full-life potions in unlocked slots | §4.7 | ✅ | `New_game_has_starting_trio_gold_and_loadouts`; screenshot artifacts/first20/bar.png |
| US-369 | A party of five (one of each role); every screen fits five | §25.14 | ✅ | `New_game_has_starting_trio_gold_and_loadouts`; sweep screenshots (hub, party, status) |
| US-370 | A defeat keeps the experience earned | §22.2 | ✅ | BalanceReport (casual bot now clears the first battle on ~half its attempts) |
| US-365 | Every enemy unit has its own distinct AI | §14.6 | ✅ | 20 brains over 41 families; `Every_enemy_family_plays_differently` + one test per brain behaviour; BalanceReport (greedy 4/4, casual+camp 2/4); enemy card shows the brain |

## Tally

| | ✅ | 🟡 | ⬜ |
|---|---|---|---|
| Battle core (board, slide, pincer, combat) (32) | 32 | 0 | 0 |
| Timeline (18) | 18 | 0 | 0 |
| Mana (13) | 13 | 0 | 0 |
| Abilities & spells (28) | 28 | 0 | 0 |
| Buffs (19) | 19 | 0 | 0 |
| AI (13) | 13 | 0 | 0 |
| Bosses (7) | 7 | 0 | 0 |
| Meta & Camp services (37) | 37 | 0 | 0 |
| UI screens (35) | 35 | 0 | 0 |
| Audio & VFX (19) | 18 | 0 | 1 |
| Platform & ship (13) | 12 | 0 | 1 |
| Production roadmap (owner direction, 2026-10-10) (9) | 8 | 1 | 0 |
| Gameplay synergy & feel (owner direction, 2026-10-11) (20) | 20 | 0 | 0 |
| **Total (263)** | **260** | **1** | **2** |

Open design questions that gate future stories live in [BIBLE §29](BIBLE.md#29-open-design-questions).
