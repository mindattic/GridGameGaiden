using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;
using Gaiden.Meta;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>Roles that need each other (§25.13): taunt, caster power, empowered strikes and battle pickups (§13.3.1).</summary>
    public class SynergyTests
    {
        private static ContentDb WithRoleKit()
        {
            var db = Mini();
            db.Buffs["taunting"] = new BuffDef { Id = "taunting", Name = "Taunting", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 2, IncomingDamageMultiplier = 0.75f, Taunts = true };
            db.Buffs["empowered"] = new BuffDef { Id = "empowered", Name = "Empowered", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 2, OutgoingDamageMultiplier = 1.3f };
            db.Abilities["taunt"] = new AbilityDef { Id = "taunt", Name = "Taunt", Kind = AbilityKind.Skill, CooldownTurns = 3, Shape = TargetShape.Self, Mode = TargetMode.Auto, Filter = TargetFilter.AllyOnly, ApplyBuff = "taunting" };
            db.Abilities["war_cry"] = new AbilityDef { Id = "war_cry", Name = "War Cry", Kind = AbilityKind.Skill, CooldownTurns = 4, Shape = TargetShape.AllAllies, Mode = TargetMode.Auto, Filter = TargetFilter.AllyOnly, ApplyBuff = "empowered" };
            return db;
        }

        [Fact]
        public void A_taunting_hero_draws_every_enemy_that_can_choose()
        {
            var db = WithRoleKit();
            db.Classes["goblin"].Targeting = Targeting.Weakest;
            var (s, _) = Battle(db, Stage(("goblin", 2, 2)), ("knight", 2, 7), ("mage", 3, 3));
            var gob = s.Units.Single(u => u.Def.Id == "goblin");
            var knight = Hero(s, "knight");
            var mage = Hero(s, "mage");
            mage.Hp = 3; // the weakest, and right next to it
            Assert.Same(mage, EnemyPlanner.PickTarget(s, gob));

            knight.Bar[0] = new AbilitySlot { AbilityId = "taunt" };
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0 });
            Assert.Contains(ev, e => e is BuffAppliedEvent b && b.BuffId == "taunting" && b.UnitId == knight.Id);
            Assert.True(knight.IsTaunting);
            Assert.Same(knight, EnemyPlanner.PickTarget(s, gob));
            Assert.Equal(3, knight.Cooldowns["taunt"] + 1); // started at 3, ticked once at control return
        }

        [Fact]
        public void Taunt_only_overrides_among_heroes_in_reach()
        {
            var db = WithRoleKit();
            var (s, _) = Battle(db, Stage(("goblin", 2, 2)), ("knight", 5, 7), ("mage", 2, 3));
            var gob = s.Units.Single(u => u.Def.Id == "goblin");
            Hero(s, "knight").Buffs.Add(new BuffInstance { Def = db.Buffs["taunting"], Remaining = 2 });
            // In melee only the mage is adjacent: the taunter can't be hit from here, so the mage still takes the blow.
            Assert.Same(Hero(s, "mage"), EnemyPlanner.PickFrom(s, gob, new List<Unit> { Hero(s, "mage") }));
            Assert.Same(Hero(s, "knight"), EnemyPlanner.PickTarget(s, gob));
        }

        [Fact]
        public void Caster_power_makes_a_mage_out_hit_a_fighter_and_a_sage_out_heal()
        {
            var c = Real;
            var fire = c.Abilities["fireball"];
            var mage = new Unit { Def = c.Classes["redmage"], Team = Team.Hero, LevelStats = c.Classes["redmage"].BaseStats.Clone() };
            var fighter = new Unit { Def = c.Classes["fighter"], Team = Team.Hero, LevelStats = c.Classes["fighter"].BaseStats.Clone() };
            Assert.True(Formulas.CasterPower(fire, mage) > 1.2f);
            Assert.True(Formulas.CasterPower(fire, fighter) < 1f);
            var cleave = c.Abilities["cleave"];
            Assert.True(Formulas.CasterPower(cleave, fighter) > Formulas.CasterPower(cleave, mage), "physical skills scale with STR");
            var sage = new Unit { Def = c.Classes["sage"], Team = Team.Hero, LevelStats = c.Classes["sage"].BaseStats.Clone() };
            Assert.True(Formulas.HealPower(sage) > Formulas.HealPower(fighter));
            var enemy = new Unit { Def = c.Classes["hag00"], Team = Team.Enemy, LevelStats = c.Classes["hag00"].BaseStats.Clone() };
            Assert.Equal(1f, Formulas.CasterPower(fire, enemy)); // enemy kits keep their tuned numbers
        }

        [Fact]
        public void War_cry_empowers_every_ally_and_their_pincers_hit_harder()
        {
            var db = WithRoleKit();
            int Pincer(bool empowered)
            {
                var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4), ("mage", 5, 7));
                if (empowered)
                {
                    var m = Hero(s, "mage");
                    m.Bar[0] = new AbilitySlot { AbilityId = "war_cry" };
                    var ev0 = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0 });
                    Assert.Equal(3, ev0.OfType<BuffAppliedEvent>().Count(b => b.BuffId == "empowered"));
                }
                var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
                return ev.OfType<DamagedEvent>().Where(d => d.SourceId == Hero(s, "knight").Id && d.Hit != HitKind.Miss).Sum(d => d.Amount);
            }
            int plain = Pincer(false), loud = Pincer(true);
            Assert.True(plain > 0);
            Assert.True(loud > plain, $"empowered {loud} should beat {plain}");
        }

        private static ContentDb WithPickups(float heal = 0.5f, int orbs = 0)
        {
            var db = WithRoleKit();
            db.Pickups["pk"] = new PickupDef { Id = "pk", Name = "Tonic", Weight = 1, HealFraction = heal, Orbs = orbs };
            return db;
        }

        [Fact]
        public void Stepping_on_a_pickup_collects_it_and_heals()
        {
            var db = WithPickups();
            var (s, _) = Battle(db, Stage(("tank", 0, 0)), ("knight", 3, 6), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            k.Hp = 5;
            s.Pickups.Add(new Pickup { Id = 1, PickupId = "pk", At = new Coord(3, 5), ActionsLeft = 4 });
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((3, 5)) });
            Assert.Contains(ev, e => e is PickupCollectedEvent pc && pc.UnitId == k.Id);
            Assert.Contains(ev, e => e is HealedEvent h && h.TargetId == k.Id);
            Assert.Empty(s.Pickups);
        }

        [Fact]
        public void A_round_trip_drag_that_only_grabs_a_pickup_still_counts_as_a_move()
        {
            var db = WithPickups(0f, 2);
            var (s, _) = Battle(db, Stage(("tank", 0, 0)), ("knight", 3, 6), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            s.Pickups.Add(new Pickup { Id = 1, PickupId = "pk", At = new Coord(3, 5), ActionsLeft = 4 });
            int before = s.Bank.Count;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((3, 5), (3, 6)) });
            Assert.DoesNotContain(ev, e => e is AbilityRefusedEvent);
            Assert.True(s.Bank.Count >= before + 2);
        }

        [Fact]
        public void Pickups_fade_after_their_lifetime()
        {
            var db = WithPickups();
            var (s, _) = Battle(db, Stage(("tank", 0, 0)), ("knight", 3, 6), ("cleric", 5, 7));
            s.Pickups.Add(new Pickup { Id = 1, PickupId = "pk", At = new Coord(0, 5), ActionsLeft = 2 });
            var k = Hero(s, "knight");
            BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((2, 6)) });
            Assert.Single(s.Pickups);
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((3, 6)) });
            Assert.Contains(ev, e => e is PickupExpiredEvent);
            Assert.Empty(s.Pickups);
        }

        [Fact]
        public void Fallen_enemies_sometimes_leave_pickups()
        {
            var db = WithPickups();
            int drops = 0;
            for (int seed = 1; seed <= 80; seed++)
            {
                var setup = new BattleSetup { Stage = Stage(("goblin", 2, 3), ("tank", 0, 0)), Seed = seed, UnlockedSlots = 2 };
                setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(1, 3) });
                setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(3, 4) });
                var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
                var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
                if (ev.Any(e => e is PickupDroppedEvent)) { drops++; Assert.Single(s.Pickups); AssertInvariants(s); }
            }
            Assert.InRange(drops, 6, 40); // 20% base
        }

        [Fact]
        public void Every_hero_has_a_role_and_kits_match_it()
        {
            var c = Real;
            Assert.Empty(ContentLoader.Validate(c));
            foreach (var h in c.Classes.Values.Where(x => x.Team == Team.Hero))
            {
                var bar = h.DefaultLoadout.Where(x => !x.StartsWith("item:")).ToList();
                switch (h.Role)
                {
                    case "Tank":
                        Assert.Contains("taunt", bar);
                        // A lone tank (party wiped around it) can't pincer by itself — it needs some non-pincer
                        // damage source in its own bar or it can soft-lock against a weak enemy neither side can
                        // finish (found live 2026-10-13: a paladin with every bar slot non-damage/on
                        // cooldown/unaffordable stalled ~40 simulated minutes against a 7-HP straggler).
                        Assert.True(bar.Any(x => c.Abilities[x].BaseDamage > 0), h.Id);
                        break;
                    case "Healer": Assert.Contains("heal", bar); break;
                    case "Mage": Assert.True(bar.Count(x => c.Abilities[x].Kind == AbilityKind.Spell && c.Abilities[x].BaseDamage > 0) >= 2, h.Id); break;
                    case "Striker": Assert.Contains("cleave", bar); break;
                    case "Support": Assert.True(bar.Contains("quicken") || bar.Contains("regen"), h.Id); break;
                    case "Ranged": Assert.True(bar.All(x => !c.Abilities[x].AdjacentOnly), h.Id); break;
                }
                foreach (var a in bar) Assert.Contains(a, h.Abilities);
            }
        }
    
        [Fact]
        public void The_bar_can_be_reordered_within_unlocked_slots()
        {
            var c = Real; var save = Progression.NewGame(c, "Org");
            var h = save.Hero("paladin");
            var first = h.Bar[0]; var second = h.Bar[1];
            Assert.True(Abilities.Swap(save, h, 0, 1).Ok);
            Assert.Equal(second, h.Bar[0]);
            Assert.Equal(first, h.Bar[1]);
            Assert.False(Abilities.Swap(save, h, 1, Progression.MaxBarSlots).Ok, "there is no sixth slot");
        }
    
        // ── Mana charge (§3.1.9) ──

        [Fact]
        public void More_heroes_in_a_pincer_earn_less_mana_per_hp_taken()
        {
            var db = Mini();
            db.Classes["tank"].BaseStats.Vitality = 30;
            (int amount, float share) Run(bool withSupporter)
            {
                var heroes = new List<(string, int, int)> { ("knight", 1, 3), ("cleric", 3, 4) };
                if (withSupporter) heroes.Add(("mage", 1, 4));
                var (s, _) = Battle(db, Stage(("tank", 2, 3)), heroes.ToArray());
                var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
                var tank = s.Units.Single(u => u.Def.Id == "tank");
                return (ev.OfType<ManaChargedEvent>().Single(c => c.Reason == "pincer").Amount, (tank.MaxHp - tank.Hp) / (float)tank.MaxHp);
            }
            var lean = Run(false); var crowd = Run(true);
            Assert.True(crowd.share >= lean.share, "the supporter adds damage…");
            Assert.True(crowd.amount / crowd.share < lean.amount / lean.share, "…but each point of HP taken charges less");
        }

        [Fact]
        public void Charge_rolls_over_into_orbs_and_keeps_the_remainder()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("goblin", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            s.ManaCharge = BattleConfig.ManaChargePerOrb - 1;
            int before = s.Bank.Count;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            var c = ev.OfType<ManaChargedEvent>().First(x => x.Reason == "pincer");
            Assert.Contains(ev, e => e is OrbMintedEvent o && o.Reason == "pincer");
            Assert.True(s.Bank.Count >= before + 1);
            Assert.InRange(s.ManaCharge, 0, BattleConfig.ManaChargePerOrb - 1);
            Assert.Equal((BattleConfig.ManaChargePerOrb - 1 + c.Amount) % BattleConfig.ManaChargePerOrb, c.Total);
        }

        [Fact]
        public void A_killing_pincer_earns_mana_by_hp_taken_not_per_hero()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("goblin", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            Assert.Contains(ev, e => e is DiedEvent);
            var charge = ev.OfType<ManaChargedEvent>().Single(c => c.Reason == "pincer");
            Assert.Equal((int)System.Math.Round(BattleConfig.PincerChargePerHp), charge.Amount); // the whole foe: share 1, two heroes ×1
            Assert.Equal(charge.Amount / BattleConfig.ManaChargePerOrb, ev.OfType<OrbMintedEvent>().Count(o => o.Reason == "pincer")); // one per 100, not one per hero
        }

        [Fact] // owner direction 2026-10-11: "more combos more xp and mana get generated" — chain depth already paid mana, now XP too
        public void A_kill_deeper_in_a_pincer_chain_earns_bonus_xp()
        {
            var db = Mini();
            // One cleric drag forms two pincers in sequence (knight+cleric, then cleric+mage): chainIndex 0 then 1.
            var (s, _) = Battle(db, Stage(("goblin", 1, 0), ("goblin", 3, 0)), ("knight", 0, 0), ("cleric", 2, 1), ("mage", 4, 0));
            var goblins = s.Units.Where(u => u.Def.Id == "goblin").ToList();
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((2, 0)) });
            var pincers = ev.OfType<PincerFormedEvent>().OrderBy(p => p.ChainIndex).ToList();
            Assert.Equal(2, pincers.Count);
            var loots = ev.OfType<LootEvent>().ToList();
            Assert.Equal(2, loots.Count);
            foreach (var p in pincers)
            {
                var deadId = p.Opponents.Single();
                var goblin = goblins.Single(g => g.Id == deadId);
                var loot = loots.Single(l => l.FromUnitId == deadId);
                int baseXp = Formulas.XpReward(goblin);
                int expected = (int)System.Math.Round(baseXp * (1f + BattleConfig.ChargeChainBonus * p.ChainIndex));
                Assert.Equal(expected, loot.Xp);
            }
            Assert.True(loots.Single(l => l.FromUnitId == pincers[1].Opponents.Single()).Xp >
                        loots.Single(l => l.FromUnitId == pincers[0].Opponents.Single()).Xp, "deeper in the chain should pay out more XP");
        }

        [Fact]
        public void The_first_battle_has_five_waves_of_rising_difficulty()
        {
            var c = Real;
            var first = c.CampaignStages()[0];
            Assert.Equal(5, first.Waves.Count);
            int Weight(WaveDef w) => w.Enemies.Sum(e => { var d = c.Classes[e.Class]; return (int)(d.BaseStats.Vitality + d.BaseStats.Strength) * e.Level + (d.ChargeAbility != null ? 5 : 0) + (d.Trap != null ? 5 : 0); });
            for (int i = 1; i < first.Waves.Count; i++)
                Assert.True(Weight(first.Waves[i]) > Weight(first.Waves[i - 1]), $"wave {i + 1} should be harder than wave {i}");
        }
    
        // ── Enemy brains (§14.6) ──

        [Fact]
        public void Every_enemy_family_plays_differently()
        {
            var c = Real;
            var sig = new Dictionary<string, string>();
            foreach (var d in c.Classes.Values.Where(x => x.Team == Team.Enemy && !x.HasTag("Remains") && !x.HasTag("Egg")))
            {
                string fam = System.Text.RegularExpressions.Regex.Replace(d.Id, @"\d+$", "");
                string key = string.Join("|", d.Brain, EnemyPlanner.TargetingOf(new Unit { Def = d, Team = Team.Enemy, LevelStats = d.BaseStats.Clone() }), d.MoveRange, d.AttackRange,
                    d.ChargeAbility, d.Trap, d.Trail, d.OnHitBuff, d.DeathBurst, string.Join(",", d.Abilities.OrderBy(x => x)));
                if (sig.TryGetValue(key, out var other) && other != fam) Assert.Fail($"{fam} plays exactly like {other}: {key}");
                sig[key] = fam;
            }
            Assert.True(c.Classes.Values.Where(x => x.Team == Team.Enemy).Select(x => x.Brain).Distinct().Count() >= 18);
        }

        private static ContentDb BrainDb(Brain brain, int move = 1)
        {
            var db = Mini();
            db.Classes["wolf"].Brain = brain;
            db.Classes["wolf"].MoveRange = move;
            db.Classes["wolf"].BaseStats.Vitality = 40;
            return db;
        }

        [Fact]
        public void An_ambusher_waits_until_a_hero_comes_close()
        {
            var (s, _) = Battle(BrainDb(Brain.Ambusher), Stage(("wolf", 2, 0)), ("knight", 2, 6), ("cleric", 5, 7));
            var w = s.Units.Single(u => u.Def.Id == "wolf");
            Assert.Equal(w.Pos, EnemyPlanner.PlanStep(s, w));
            Hero(s, "knight").Pos = new Coord(2, 2);
            Assert.NotEqual(w.Pos, EnemyPlanner.PlanStep(s, w));
        }

        [Fact]
        public void A_sentinel_never_moves_and_a_sluggish_foe_moves_every_other_turn()
        {
            var (s, _) = Battle(BrainDb(Brain.Sentinel), Stage(("wolf", 2, 0)), ("knight", 2, 3), ("cleric", 5, 7));
            var w = s.Units.Single(u => u.Def.Id == "wolf");
            Assert.Equal(w.Pos, EnemyPlanner.PlanStep(s, w));
            var (s2, _) = Battle(BrainDb(Brain.Sluggish), Stage(("wolf", 2, 0)), ("knight", 2, 3), ("cleric", 5, 7));
            var w2 = s2.Units.Single(u => u.Def.Id == "wolf");
            w2.TurnsTaken = 1; Assert.NotEqual(w2.Pos, EnemyPlanner.PlanStep(s2, w2));
            w2.TurnsTaken = 2; Assert.Equal(w2.Pos, EnemyPlanner.PlanStep(s2, w2));
        }

        [Fact]
        public void A_hopper_leaps_two_tiles_over_a_unit()
        {
            var (s, _) = Battle(BrainDb(Brain.Hopper), Stage(("wolf", 2, 0), ("tank", 2, 1)), ("knight", 2, 4), ("cleric", 5, 7));
            var w = s.Units.Single(u => u.Def.Id == "wolf");
            Assert.Equal(new Coord(2, 2), EnemyPlanner.PlanStep(s, w));
        }

        [Fact]
        public void A_bomber_dives_where_it_touches_the_most_heroes()
        {
            var (s, _) = Battle(BrainDb(Brain.Bomber), Stage(("wolf", 2, 2)), ("knight", 1, 3), ("cleric", 3, 3), ("mage", 0, 0));
            var w = s.Units.Single(u => u.Def.Id == "wolf");
            Assert.Equal(new Coord(2, 3), EnemyPlanner.PlanStep(s, w)); // between knight and cleric: touches two
        }

        [Fact]
        public void A_skirmisher_strikes_then_darts_away()
        {
            var db = BrainDb(Brain.Skirmisher);
            db.Classes["wolf"].ChargeAbility = null;
            var (s, _) = Battle(db, Stage(("wolf", 2, 2)), ("knight", 2, 3), ("cleric", 5, 7));
            var w = s.Units.Single(u => u.Def.Id == "wolf");
            s.TurnEntryOf(w.Id).Ip = BattleConfig.BarLength - 1;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.Contains(ev, e => e is AttackEvent a && a.AttackerId == w.Id);
            Assert.True(w.DistanceTo(Hero(s, "knight")) >= 2, "it backed off after the strike");
        }

        [Fact]
        public void A_burrower_surfaces_beside_its_target()
        {
            var db = BrainDb(Brain.Burrower);
            db.Classes["wolf"].ChargeAbility = null;
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 4, 6), ("cleric", 5, 7));
            var w = s.Units.Single(u => u.Def.Id == "wolf");
            w.TurnsTaken = 2; // its next turn is the third: it burrows
            s.TurnEntryOf(w.Id).Ip = BattleConfig.BarLength - 1;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.Contains(ev, e => e is TeleportedEvent t && t.UnitId == w.Id);
            Assert.True(s.Units.Where(u => u.IsHero).Any(h => w.IsAdjacentTo(h)));
        }

        [Fact]
        public void A_guardian_stays_by_its_ward()
        {
            var db = BrainDb(Brain.Guardian);
            var (s, _) = Battle(db, Stage(("wolf", 2, 1), ("witch", 0, 0)), ("knight", 2, 6), ("cleric", 5, 7));
            var w = s.Units.Single(u => u.Def.Id == "wolf");
            var step = EnemyPlanner.PlanStep(s, w);
            Assert.True(step.Manhattan(new Coord(0, 0)) <= w.Pos.Manhattan(new Coord(0, 0)), "it closes on the witch, not the heroes");
        }
    
        // ── Armor plates (§13.5) and corpses (§14.7) ──

        private static ContentDb Plated(int plate = 40)
        {
            var db = Mini();
            db.Classes["goblin"].ArmorPlate = plate;
            db.Classes["goblin"].BaseStats.Vitality = 60;
            db.Classes["goblin"].DropTable = null;
            return db;
        }

        [Fact]
        public void A_plate_takes_the_blow_from_its_facing_side_and_the_flank_hits_flesh()
        {
            var (s, _) = Battle(Plated(), Stage(("goblin", 2, 3), ("tank", 0, 0)), ("knight", 2, 4), ("cleric", 2, 0));
            var gob = s.Units.Single(u => u.Def.Id == "goblin");
            Assert.Equal(new Coord(0, 1), gob.Facing); // it faces the closest hero, below it
            int hp = gob.Hp;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((2, 1), (2, 2)) });
            Assert.Contains(ev, e => e is PlateHitEvent ph && ph.AttackerId == Hero(s, "knight").Id);
            Assert.Contains(ev, e => e is DamagedEvent d && d.TargetId == gob.Id && d.SourceId == Hero(s, "cleric").Id);
            Assert.DoesNotContain(ev, e => e is DamagedEvent d && d.TargetId == gob.Id && d.SourceId == Hero(s, "knight").Id);
            Assert.True(gob.PlateHp < gob.PlateMax);
        }

        [Fact]
        public void A_pincer_across_its_line_of_sight_skips_the_plate_and_a_broken_plate_stops_blocking()
        {
            var (s, _) = Battle(Plated(), Stage(("goblin", 2, 3), ("tank", 0, 0)), ("knight", 2, 4), ("cleric", 3, 3), ("mage", 0, 3));
            var gob = s.Units.Single(u => u.Def.Id == "goblin");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "mage").Id, Path = P((1, 3)) }); // mage (1,3) · goblin · cleric (3,3): from the sides
            Assert.DoesNotContain(ev, e => e is PlateHitEvent);
            Assert.Contains(ev, e => e is DamagedEvent d && d.TargetId == gob.Id && d.Amount > 0);

            var (s2, _) = Battle(Plated(1), Stage(("goblin", 2, 3), ("tank", 0, 0)), ("knight", 2, 4), ("cleric", 2, 0));
            var g2 = s2.Units.Single(u => u.Def.Id == "goblin");
            var ev2 = BattleEngine.Apply(s2, new MoveCommand { HeroId = Hero(s2, "cleric").Id, Path = P((2, 1), (2, 2)) });
            Assert.Contains(ev2, e => e is PlateBrokenEvent);
            Assert.False(g2.HasPlate);
        }

        [Fact]
        public void Spells_go_around_the_plate()
        {
            var db = Plated();
            var (s, _) = Battle(db, Stage(("goblin", 2, 3), ("tank", 0, 0)), ("mage", 2, 5), ("knight", 5, 7));
            var gob = s.Units.Single(u => u.Def.Id == "goblin");
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            s.Bank.Add(3);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = gob.Pos });
            Assert.DoesNotContain(ev, e => e is PlateHitEvent);
            Assert.Contains(ev, e => e is DamagedEvent d && d.TargetId == gob.Id && d.Amount > 0);
        }

        private static ContentDb WithCorpses()
        {
            var db = Mini();
            db.Classes["corpse"] = new ClassDef { Id = "corpse", Name = "Corpse", Team = Team.Enemy, Tags = { "Remains", "Corpse" }, BaseStats = new Stats { Vitality = 6 }, MoveRange = 0, Brain = Brain.Sentinel };
            db.Classes["goblin"].Remains = "corpse";
            db.Classes["goblin"].DropTable = null;
            return db;
        }

        [Fact]
        public void A_fallen_foe_leaves_a_corpse_that_blocks_the_way_but_not_the_win()
        {
            var db = WithCorpses();
            var (s, _) = Battle(db, Stage(("goblin", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            Assert.Contains(ev, e => e is RemainsLeftEvent);
            var corpse = s.Units.Single(u => u.Alive && u.IsCorpse);
            Assert.Equal(new Coord(2, 3), corpse.Pos);
            Assert.Equal(BattlePhase.Victory, s.Phase); // only a body left: the wave is clear
            Assert.DoesNotContain(s.Timeline, e => e.OwnerId == corpse.Id);
        }

        [Fact]
        public void A_corpse_blocks_movement_and_rots_away()
        {
            var db = WithCorpses();
            var (s, _) = Battle(db, StageWaves(new[] { ("goblin", 2, 3) }, new[] { ("tank", 0, 0) }), ("knight", 1, 3), ("cleric", 3, 4));
            BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            var corpse = s.Units.Single(u => u.Alive && u.IsCorpse);
            var k = Hero(s, "knight");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((2, 3)) });
            Assert.Contains(ev, e => e is StepBlockedEvent);
            for (int i = 0; i < BattleConfig.CorpseDecayActions + 1 && corpse.Alive; i++)
                BattleEngine.Apply(s, new ShieldCommand());
            Assert.False(corpse.Alive, "it rotted away");
        }
    }
}
