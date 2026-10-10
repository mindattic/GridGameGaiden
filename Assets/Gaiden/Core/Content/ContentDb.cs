using System;
using System.Collections.Generic;

namespace Gaiden.Core.Content
{
    /// <summary>All loaded content, indexed by id. Immutable after load.</summary>
    public sealed class ContentDb
    {
        public readonly Dictionary<string, ClassDef> Classes = new Dictionary<string, ClassDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, AbilityDef> Abilities = new Dictionary<string, AbilityDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, BuffDef> Buffs = new Dictionary<string, BuffDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, RecipeDef> Recipes = new Dictionary<string, RecipeDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, DropTableDef> DropTables = new Dictionary<string, DropTableDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, StageDef> Stages = new Dictionary<string, StageDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, TrapDef> Traps = new Dictionary<string, TrapDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, BountyDef> Bounties = new Dictionary<string, BountyDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, EnchantDef> Enchants = new Dictionary<string, EnchantDef>(StringComparer.OrdinalIgnoreCase);
        public readonly List<StageDef> StageOrder = new List<StageDef>();
        public CampaignDef Campaign = new CampaignDef();
        public SummonDef Summon = new SummonDef();
        public readonly Dictionary<string, CaravanUpgradeDef> Caravan = new Dictionary<string, CaravanUpgradeDef>(StringComparer.OrdinalIgnoreCase);

        public ClassDef Class(string id) => Get(Classes, id, "class");
        public AbilityDef Ability(string id) => Get(Abilities, id, "ability");
        public BuffDef Buff(string id) => Get(Buffs, id, "buff");
        public ItemDef Item(string id) => Get(Items, id, "item");
        public StageDef Stage(string id) => Get(Stages, id, "stage");

        public AbilityDef TryAbility(string id) => id != null && Abilities.TryGetValue(id, out var a) ? a : null;
        public ItemDef TryItem(string id) => id != null && Items.TryGetValue(id, out var a) ? a : null;
        public BuffDef TryBuff(string id) => id != null && Buffs.TryGetValue(id, out var a) ? a : null;
        public ClassDef TryClass(string id) => id != null && Classes.TryGetValue(id, out var a) ? a : null;

        private static T Get<T>(Dictionary<string, T> map, string id, string kind)
        {
            if (id != null && map.TryGetValue(id, out var v)) return v;
            throw new KeyNotFoundException($"Unknown {kind} '{id}'");
        }

        /// <summary>Campaign stages in play order (themes in order, stages in order).</summary>
        public List<StageDef> CampaignStages()
        {
            var list = new List<StageDef>();
            foreach (var t in Campaign.Themes)
                foreach (var s in t.Stages)
                    if (Stages.TryGetValue(s, out var def)) list.Add(def);
            return list;
        }

        public StageDef TryStage(string id) => id != null && Stages.TryGetValue(id, out var st) ? st : null;

        public ThemeDef ThemeOf(string stageId)
        {
            foreach (var t in Campaign.Themes)
                if (t.Stages.Contains(stageId)) return t;
            return null;
        }
    }
}
