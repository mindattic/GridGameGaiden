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
            "stages", "campaign", "traps", "bounties", "summon", "enchants", "caravan", "pickups", "skilltrees",
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
            AddAll(db.Pickups, ReadList<PickupDef>(readText, "pickups"), x => x.Id);
            AddAll(db.SkillTrees, ReadList<SkillTreeDef>(readText, "skilltrees"), x => x.Id);
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
                if (item == null) throw new FormatException($"{typeof(T).Name}: null entry");
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
                    if (s == null) { p.Add($"class {c.Id} loadout: null entry"); continue; }
                    if (s.StartsWith("item:")) NeedItem(s.Substring(5), $"class {c.Id} loadout");
                    else NeedAbility(s, $"class {c.Id} loadout");
                }
                NeedAbility(c.ChargeAbility, $"class {c.Id} charge");
                if (c.Trap != null && !db.Traps.ContainsKey(c.Trap)) p.Add($"class {c.Id}: unknown trap '{c.Trap}'");
                if (c.Trail != null && !db.Traps.ContainsKey(c.Trail)) p.Add($"class {c.Id}: unknown trail '{c.Trail}'");
                NeedBuff(c.OnHitBuff, $"class {c.Id} on-hit buff");
                NeedClass(c.Remains, $"class {c.Id} remains");
                NeedClass(c.LaysEgg, $"class {c.Id} egg");
                foreach (var ph in c.BossPhases) NeedAbility(ph.OnEnterAbility, $"class {c.Id} boss phase");
            }
            foreach (var a in db.Abilities.Values) { NeedBuff(a.ApplyBuff, $"ability {a.Id}"); NeedBuff(a.RemovesBuff, $"ability {a.Id} removes"); }
            foreach (var b in db.Buffs.Values) NeedBuff(b.OnExpireApply, $"buff {b.Id}");
            foreach (var i in db.Items.Values)
            {
                NeedAbility(i.OnUseAbility, $"item {i.Id}");
                NeedBuff(i.CuresBuff, $"item {i.Id} cures");
                NeedBuff(i.PincerApplyBuff, $"item {i.Id} pincer buff");
                if (i.Type == ItemType.RecipeScroll)
                {
                    if (i.TeachesRecipe == null) p.Add($"item {i.Id}: RecipeScroll needs TeachesRecipe");
                    else if (!db.Recipes.ContainsKey(i.TeachesRecipe)) p.Add($"item {i.Id}: unknown recipe '{i.TeachesRecipe}'");
                }
            }
            foreach (var r in db.Recipes.Values)
            {
                NeedItem(r.Result, $"recipe {r.Id}");
                NeedItem(r.BaseItem, $"recipe {r.Id} base");
                foreach (var ic in r.Inputs) NeedItem(ic.Item, $"recipe {r.Id}");
                NeedItem(r.PotencyResult, $"recipe {r.Id} potency");
                foreach (var ic in r.PotencyInputs) NeedItem(ic.Item, $"recipe {r.Id} potency");
            }
            foreach (var d in db.DropTables.Values)
                foreach (var e in d.Entries) NeedItem(e.Item, $"droptable {d.Id}");
            foreach (var s in db.Stages.Values)
                foreach (var w in s.Waves)
                {
                    foreach (var e in w.Enemies) NeedClass(e.Class, $"stage {s.Id}");
                    foreach (var g in w.Guests) NeedClass(g.Class, $"stage {s.Id} guest");
                    NeedClass(w.SurpriseSpeaker, $"stage {s.Id} surprise speaker");
                }
            // ContentDb.ThemeOf resolves a stage id to the *first* theme that lists it — a stage accidentally
            // listed in two themes would silently belong to the wrong one there (expedition tracking,
            // carried orbs) with no error here to catch it, so every stage id must be theme-unique.
            var stageThemeOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in db.Campaign.Themes)
                foreach (var s in t.Stages)
                {
                    if (!db.Stages.ContainsKey(s)) p.Add($"theme {t.Id}: unknown stage '{s}'");
                    else if (stageThemeOwner.TryGetValue(s, out var owner)) p.Add($"theme {t.Id}: stage '{s}' already belongs to theme '{owner}'");
                    else stageThemeOwner[s] = t.Id;
                }
            foreach (var t in db.Traps.Values) NeedBuff(t.ApplyBuff, $"trap {t.Id}");
            foreach (var pk in db.Pickups.Values)
            {
                NeedBuff(pk.ApplyBuff, $"pickup {pk.Id}");
                if (pk.Weight <= 0) p.Add($"pickup {pk.Id}: weight must be positive");
            }
            var roles = new HashSet<string> { "Tank", "Healer", "Mage", "Striker", "Support", "Ranged" };
            foreach (var c in db.Classes.Values)
                if (c.Team == Team.Hero && (c.Role == null || !roles.Contains(c.Role))) p.Add($"class {c.Id}: hero needs a Role (Tank/Healer/Mage/Striker/Support/Ranged), has '{c.Role}'");
            foreach (var b in db.Bounties.Values) { NeedClass(b.TargetClass, $"bounty {b.Id}"); NeedItem(b.RewardItem, $"bounty {b.Id}"); }
            foreach (var c in db.Summon.Pool) NeedClass(c, "summon pool");
            foreach (var e in db.Enchants.Values) NeedItem(e.Essence, $"enchant {e.Id}");

            // Skill trees (§25.6.1, GG-T50): every node's grant and prerequisites must resolve, and every
            // hero class must name a home tree that exists.
            foreach (var t in db.SkillTrees.Values)
            {
                var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var n in t.Nodes)
                {
                    if (string.IsNullOrEmpty(n.Id)) { p.Add($"tree {t.Id}: node with empty Id"); continue; }
                    if (!nodeIds.Add(n.Id)) p.Add($"tree {t.Id}: duplicate node id '{n.Id}'");
                }
                foreach (var n in t.Nodes)
                {
                    NeedAbility(n.Grants, $"tree {t.Id} node {n.Id}");
                    if (n.Cost <= 0) p.Add($"tree {t.Id} node {n.Id}: cost must be positive");
                    if (n.RequiredPointsInTree < 0) p.Add($"tree {t.Id} node {n.Id}: required points can't be negative");
                    foreach (var req in n.Prerequisites)
                    {
                        if (!nodeIds.Contains(req)) p.Add($"tree {t.Id} node {n.Id}: unknown prerequisite '{req}'");
                        else if (req == n.Id) p.Add($"tree {t.Id} node {n.Id}: can't require itself");
                    }
                }
                // Cycle check: walk each node's prerequisite chain and fail only if it revisits a node
                // already on the *current* path. Two branches sharing a common ancestor (e.g. a finale
                // node requiring two capstones that both trace back to the same root) is a valid diamond,
                // not a cycle — tracking "seen anywhere" instead of "seen on this path" flagged those too.
                foreach (var n in t.Nodes)
                {
                    var onPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (HasCycle(n)) p.Add($"tree {t.Id} node {n.Id}: prerequisite cycle");

                    bool HasCycle(SkillNodeDef cur)
                    {
                        if (!onPath.Add(cur.Id)) return true;
                        foreach (var req in cur.Prerequisites)
                        {
                            var reqNode = t.Nodes.Find(x => string.Equals(x.Id, req, StringComparison.OrdinalIgnoreCase));
                            if (reqNode != null && HasCycle(reqNode)) return true;
                        }
                        onPath.Remove(cur.Id);
                        return false;
                    }
                }
            }
            foreach (var c in db.Classes.Values)
            {
                if (c.Team != Team.Hero) continue;
                if (string.IsNullOrEmpty(c.HomeTree)) p.Add($"class {c.Id}: hero needs a HomeTree");
                else if (!db.SkillTrees.ContainsKey(c.HomeTree)) p.Add($"class {c.Id}: unknown home tree '{c.HomeTree}'");
            }
            return p;
        }
    }
}
