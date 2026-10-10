using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Meta;
using Xunit;
using Xunit.Abstractions;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>Exercises every entry of the real content catalogs through the engine (no ability is "data only").</summary>
    public class CatalogTests
    {
        private readonly ITestOutputHelper output;
        public CatalogTests(ITestOutputHelper output) { this.output = output; }

        public static IEnumerable<object[]> HeroAbilities() =>
            Real.Abilities.Values.Where(Gaiden.Meta.Abilities.IsHeroUsable).Select(a => new object[] { a.Id });

        [Theory]
        [MemberData(nameof(HeroAbilities))]
        public void Every_hero_ability_resolves_against_a_live_board(string abilityId)
        {
            var c = Real;
            var a = c.Ability(abilityId);
            var hero = c.Classes.Values.First(x => x.Team == Team.Hero);
            var enemy = c.Classes.Values.First(x => x.Team == Team.Enemy && x.Footprint.W == 1 && x.SnakeSegments == 0 && x.ChargeAbility == null && x.Trap == null);
            var stage = new StageDef { Id = "cat", IsTest = true, Waves = { new WaveDef { Enemies = { new SpawnDef { Class = enemy.Id, Level = 1, X = 2, Y = 2 }, new SpawnDef { Class = enemy.Id, Level = 1, X = 3, Y = 2 } } } } };
            var setup = new BattleSetup { Stage = stage, Seed = 11, UnlockedSlots = 5 };
            setup.Party.Add(new HeroSpec { ClassId = hero.Id, Level = 5, Start = new Coord(2, 3), Bar = new List<string> { a.Id } });
            setup.Party.Add(new HeroSpec { ClassId = hero.Id == "cleric" ? "paladin" : "cleric", Level = 5, Start = new Coord(4, 4) });
            var s = BattleEngine.Create(c, setup, new List<BattleEvent>());
            s.Bank.Add(12); // wilds pay any colored cost
            var h = s.Units.First(u => u.IsHero);
            h.Hp = h.MaxHp / 2; // so heals have something to do
            var status = BattleEngine.GetSlotStatus(s, h.Id, 0);
            Assert.Equal(SlotStatus.Ready, status);
            Coord target = h.Pos;
            if (a.Mode != TargetMode.Auto)
            {
                var targets = BattleEngine.ValidTargets(s, h.Id, 0);
                Assert.True(targets.Count > 0, $"{a.Id}: no valid targets");
                target = targets[0];
            }
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = h.Id, Slot = 0, Target = target });
            Assert.DoesNotContain(ev, e => e is AbilityRefusedEvent);
            bool resolvedNow = ev.Any(e => e is AbilityResolvedEvent r && r.AbilityId == a.Id) || ev.Any(e => e is TeleportedEvent);
            bool queued = ev.Any(e => e is CastQueuedEvent q && q.AbilityId == a.Id);
            Assert.True(resolvedNow || queued, $"{a.Id} neither resolved nor queued: {string.Join(" | ", ev)}");
            for (int i = 0; i < 6 && queued && !resolvedNow && !s.IsOver; i++)
                resolvedNow = BattleEngine.Apply(s, new ShieldCommand()).Any(e => e is AbilityResolvedEvent r && r.AbilityId == a.Id);
            Assert.True(resolvedNow || s.IsOver, $"{a.Id}: cast never resolved");
            AssertInvariants(s);
        }

        public static IEnumerable<object[]> ChargeAbilities() =>
            Real.Abilities.Values.Where(a => a.Id.StartsWith("charge_")).Select(a => new object[] { a.Id });

        [Theory]
        [MemberData(nameof(ChargeAbilities))]
        public void Every_enemy_charge_telegraphs_and_resolves(string abilityId)
        {
            var c = Real;
            var caster = c.Classes.Values.FirstOrDefault(x => x.ChargeAbility == abilityId && x.Footprint.W == 1);
            if (caster == null) { output.WriteLine($"{abilityId}: no 1x1 caster uses it"); return; }
            var stage = new StageDef { Id = "chg", IsTest = true, Waves = { new WaveDef { Enemies = { new SpawnDef { Class = caster.Id, Level = 1, X = 0, Y = 0 } } } } };
            var setup = new BattleSetup { Stage = stage, Seed = 5, UnlockedSlots = 2 };
            setup.Party.Add(new HeroSpec { ClassId = "paladin", Level = 20, Start = new Coord(5, 7) });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Level = 20, Start = new Coord(0, 5) });
            var s = BattleEngine.Create(c, setup, new List<BattleEvent>());
            // A healing charge only goes out when an ally needs it: wound the caster.
            if (c.Abilities[abilityId].BaseHeal > 0) { var me = s.Units.First(u => !u.IsHero); me.Hp = me.MaxHp / 3; }
            var all = new List<BattleEvent>();
            for (int i = 0; i < 12 && !s.IsOver; i++) all.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
            Assert.Contains(all, e => e is CastQueuedEvent q && q.IsCharge);
            Assert.Contains(all, e => e is AbilityResolvedEvent r && r.AbilityId == abilityId);
            AssertInvariants(s);
        }

        [Fact]
        public void Every_consumable_has_a_working_battle_effect_or_is_camp_only()
        {
            var c = Real;
            var problems = new List<string>();
            foreach (var item in c.Items.Values.Where(i => i.Type == ItemType.Consumable))
            {
                if (item.OutOfBattleOnly) { Assert.True(Camp.UsableAtCamp(item), $"{item.Id}: camp-only item unusable at camp"); continue; }
                var stage = new StageDef { Id = "itm", IsTest = true, Waves = { new WaveDef { Enemies = { new SpawnDef { Class = "slime00", Level = 1, X = 2, Y = 2 } } } } };
                var setup = new BattleSetup { Stage = stage, Seed = 2, UnlockedSlots = 5 };
                setup.Party.Add(new HeroSpec { ClassId = "paladin", Level = 3, Start = new Coord(2, 3), Bar = new List<string> { "item:" + item.Id }, ItemCounts = { [item.Id] = 2 } });
                setup.Party.Add(new HeroSpec { ClassId = "cleric", Level = 3, Start = new Coord(4, 4) });
                setup.Party.Add(new HeroSpec { ClassId = "barbarian", Level = 3, Start = new Coord(0, 7) });
                var s = BattleEngine.Create(c, setup, new List<BattleEvent>());
                var h = s.Units.First(u => u.IsHero);
                h.Hp = 5;
                var buffDef = c.TryBuff(item.CuresBuff) ?? c.TryBuff("poisoned");
                h.Buffs.Add(new BuffInstance { Def = buffDef, Remaining = 3 });
                if (item.Revives) { var dead = s.Units.First(u => u.Def.Id == "barbarian"); dead.Hp = 0; dead.OnBoard = false; }
                var a = BattleEngine.SlotAbility(s, h, 0);
                if (a == null) { problems.Add($"{item.Id}: no in-battle effect"); continue; }
                var targets = a.Mode == TargetMode.Auto ? new List<Coord> { h.Pos } : BattleEngine.ValidTargets(s, h.Id, 0);
                if (targets.Count == 0) { problems.Add($"{item.Id}: no target"); continue; }
                var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = h.Id, Slot = 0, Target = targets.Contains(h.Pos) ? h.Pos : targets[0] });
                if (!ev.Any(e => e is ItemUsedEvent)) { problems.Add($"{item.Id}: refused {string.Join(" | ", ev.OfType<AbilityRefusedEvent>())}"); continue; }
                bool effect = ev.Any(e => e is HealedEvent || e is DamagedEvent || e is RevivedEvent || e is BattleEndedEvent || e is OrbMintedEvent || (e is BuffRemovedEvent r && (r.Reason == "cured" || r.Reason == "cleansed")));
                if (!effect) problems.Add($"{item.Id}: used but nothing happened");
                AssertInvariants(s);
            }
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void Smoke_bomb_escapes_and_keeps_wounds()
        {
            var c = Real;
            var save = Progression.NewGame(c, "Esc");
            save.AddItem("smoke_bomb", 1);
            save.Hero("paladin").Bar[0] = "item:smoke_bomb";
            var setup = BattleFactory.Build(c, save, c.CampaignStages()[0].Id, 1);
            var s = BattleEngine.Create(c, setup, new List<BattleEvent>());
            var pal = s.Units.First(u => u.Def.Id == "paladin");
            pal.Hp = 20;
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = pal.Id, Slot = 0, Target = pal.Pos });
            Assert.Equal(BattlePhase.Escaped, s.Phase);
            int gold = save.Gold;
            var rep = BattleFactory.Commit(c, save, s);
            Assert.True(rep.Escaped);
            Assert.Equal(gold, save.Gold);
            Assert.Equal(20, save.Hero("paladin").Hp);
            Assert.Equal(0, save.Count("smoke_bomb"));
        }

        [Fact]
        public void Camp_potion_heals_a_wounded_hero_and_tent_heals_everyone()
        {
            var c = Real;
            var save = Progression.NewGame(c, "Camp");
            var h = save.Hero("paladin");
            h.Hp = 10;
            save.AddItem("healing_potion_basic", 1);
            int before = save.Count("healing_potion_basic");
            Assert.True(Camp.Use(c, save, "healing_potion_basic", "paladin").Ok);
            Assert.True(h.Hp > 10 || h.Hp == 0);
            Assert.Equal(before - 1, save.Count("healing_potion_basic"));
            save.Hero("cleric").Hp = 3;
            save.AddItem("tent", 1);
            Assert.True(Camp.Use(c, save, "tent", null).Ok);
            Assert.All(save.Roster, x => Assert.Equal(0, x.Hp));
        }

        [Fact]
        public void Every_recipe_is_craftable_with_its_inputs()
        {
            var c = Real;
            foreach (var r in c.Recipes.Values)
            {
                var s = new SaveData { Gold = r.Gold };
                foreach (var ic in r.Inputs) s.AddItem(ic.Item, ic.Count);
                if (r.BaseItem != null) s.AddItem(r.BaseItem, 1);
                var res = Crafting.Craft(c, s, r);
                Assert.True(res.Ok, $"{r.Id}: {res.Message}");
                Assert.True(s.Count(r.Result) >= r.ResultCount);
            }
        }

        [Fact]
        public void Every_class_can_be_instantiated_and_has_art_reference()
        {
            foreach (var cls in Real.Classes.Values)
            {
                Assert.False(string.IsNullOrEmpty(cls.Name), cls.Id);
                var st = Gaiden.Core.Rules.Formulas.StatsAtLevel(cls, 10);
                Assert.True(Gaiden.Core.Rules.Formulas.MaxHp(st, 10) > 0, cls.Id);
                Assert.True(System.IO.File.Exists(System.IO.Path.Combine(RepoRoot, "Assets", "Gaiden", "Resources", "Thumbs", cls.Id + ".png")), $"missing thumb for {cls.Id}");
            }
        }
    }
}
