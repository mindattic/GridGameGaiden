using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gaiden.Core.Content
{
    /// <summary>
    /// Builds a <see cref="ContentDb"/> from the JSON documents. The text source is injected
    /// (Unity: Resources TextAssets; tests: files on disk) so the loader stays engine-free.
    /// </summary>
    public static class ContentLoader
    {
        public static readonly string[] Files =
        {
            "classes", "abilities", "buffs", "items", "recipes", "droptables",
            "stages", "campaign", "traps", "bounties", "summon", "enchants",
        };

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Ignore,
            // Replace (not append to) collections that have initializers, e.g. HeroSave.Bar's five empty slots.
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        /// <param name="readText">Returns the JSON text for a file name (no extension), or null if absent.</param>
        public static ContentDb Load(Func<string, string> readText)
        {
            var db = new ContentDb();
            AddAll(db.Classes, ReadList<ClassDef>(readText, "classes"), c => c.Id);
            AddAll(db.Abilities, ReadList<AbilityDef>(readText, "abilities"), c => c.Id);
            AddAll(db.Buffs, ReadList<BuffDef>(readText, "buffs"), c => c.Id);
            AddAll(db.Items, ReadList<ItemDef>(readText, "items"), c => c.Id);
            AddAll(db.Recipes, ReadList<RecipeDef>(readText, "recipes"), c => c.Id);
            AddAll(db.DropTables, ReadList<DropTableDef>(readText, "droptables"), c => c.Id);
            var stages = ReadList<StageDef>(readText, "stages");
            AddAll(db.Stages, stages, c => c.Id);
            db.StageOrder.AddRange(stages);
            AddAll(db.Traps, ReadList<TrapDef>(readText, "traps"), c => c.Id);
            AddAll(db.Bounties, ReadList<BountyDef>(readText, "bounties"), c => c.Id);
            AddAll(db.Enchants, ReadList<EnchantDef>(readText, "enchants"), c => c.Id);
            db.Campaign = ReadOne<CampaignDef>(readText, "campaign") ?? new CampaignDef();
            db.Summon = ReadOne<SummonDef>(readText, "summon") ?? new SummonDef();
            AddAll(db.Caravan, ReadList<CaravanUpgradeDef>(readText, "caravan"), x => x.Id);
            return db;
        }

        private static List<T> ReadList<T>(Func<string, string> readText, string name)
        {
            var text = readText(name);
            if (string.IsNullOrWhiteSpace(text)) return new List<T>();
            try { return JsonConvert.DeserializeObject<List<T>>(text, Settings) ?? new List<T>(); }
            catch (Exception e) { throw new FormatException($"Content file '{name}.json' is invalid: {e.Message}", e); }
        }

        private static T ReadOne<T>(Func<string, string> readText, string name) where T : class
        {
            var text = readText(name);
            if (string.IsNullOrWhiteSpace(text)) return null;
            try { return JsonConvert.DeserializeObject<T>(text, Settings); }
            catch (Exception e) { throw new FormatException($"Content file '{name}.json' is invalid: {e.Message}", e); }
        }

        private static void AddAll<T>(Dictionary<string, T> map, List<T> items, Func<T, string> key)
        {
            foreach (var item in items)
            {
                var k = key(item);
                if (string.IsNullOrEmpty(k)) throw new FormatException($"{typeof(T).Name} with empty Id");
                if (map.ContainsKey(k)) throw new FormatException($"Duplicate {typeof(T).Name} id '{k}'");
                map[k] = item;
            }
        }

        /// <summary>Cross-reference check; returns human-readable problems (empty = valid).</summary>
        public static List<string> Validate(ContentDb db)
        {
            var p = new List<string>();
            void NeedClass(string id, string where) { if (id != null && !db.Classes.ContainsKey(id)) p.Add($"{where}: unknown class '{id}'"); }
            void NeedItem(string id, string where) { if (id != null && !db.Items.ContainsKey(id)) p.Add($"{where}: unknown item '{id}'"); }
            void NeedAbility(string id, string where) { if (id != null && !db.Abilities.ContainsKey(id)) p.Add($"{where}: unknown ability '{id}'"); }
            void NeedBuff(string id, string where) { if (id != null && !db.Buffs.ContainsKey(id)) p.Add($"{where}: unknown buff '{id}'"); }

            foreach (var c in db.Classes.Values)
            {
                if (c.DropTable != null && !db.DropTables.ContainsKey(c.DropTable)) p.Add($"class {c.Id}: unknown drop table '{c.DropTable}'");
                foreach (var a in c.Abilities) NeedAbility(a, $"class {c.Id} abilities");
                foreach (var s in c.DefaultLoadout)
                {
                    if (s.StartsWith("item:")) NeedItem(s.Substring(5), $"class {c.Id} loadout");
                    else NeedAbility(s, $"class {c.Id} loadout");
                }
                NeedAbility(c.ChargeAbility, $"class {c.Id} charge");
                if (c.Trap != null && !db.Traps.ContainsKey(c.Trap)) p.Add($"class {c.Id}: unknown trap '{c.Trap}'");
                foreach (var ph in c.BossPhases) NeedAbility(ph.OnEnterAbility, $"class {c.Id} boss phase");
            }
            foreach (var a in db.Abilities.Values) NeedBuff(a.ApplyBuff, $"ability {a.Id}");
            foreach (var b in db.Buffs.Values) NeedBuff(b.OnExpireApply, $"buff {b.Id}");
            foreach (var i in db.Items.Values) NeedAbility(i.OnUseAbility, $"item {i.Id}");
            foreach (var r in db.Recipes.Values)
            {
                NeedItem(r.Result, $"recipe {r.Id}");
                NeedItem(r.BaseItem, $"recipe {r.Id} base");
                foreach (var ic in r.Inputs) NeedItem(ic.Item, $"recipe {r.Id}");
            }
            foreach (var d in db.DropTables.Values)
                foreach (var e in d.Entries) NeedItem(e.Item, $"droptable {d.Id}");
            foreach (var s in db.Stages.Values)
                foreach (var w in s.Waves)
                    foreach (var e in w.Enemies) NeedClass(e.Class, $"stage {s.Id}");
            foreach (var t in db.Campaign.Themes)
                foreach (var s in t.Stages)
                    if (!db.Stages.ContainsKey(s)) p.Add($"theme {t.Id}: unknown stage '{s}'");
            foreach (var t in db.Traps.Values) NeedBuff(t.ApplyBuff, $"trap {t.Id}");
            foreach (var b in db.Bounties.Values) { NeedClass(b.TargetClass, $"bounty {b.Id}"); NeedItem(b.RewardItem, $"bounty {b.Id}"); }
            foreach (var c in db.Summon.Pool) NeedClass(c, "summon pool");
            foreach (var e in db.Enchants.Values) NeedItem(e.Essence, $"enchant {e.Id}");
            return p;
        }
    }
}
