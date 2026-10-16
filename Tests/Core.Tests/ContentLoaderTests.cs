using System;
using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Content;
using Xunit;

namespace Gaiden.Tests
{
    public class ContentLoaderTests
    {
        [Fact]
        public void Load_rejects_unknown_json_members()
        {
            string Read(string name) => name == "classes"
                ? "[{\"Id\":\"x\",\"Name\":\"X\",\"Team\":\"Hero\",\"NotARealField\":1}]"
                : null;

            Assert.Throws<FormatException>(() => ContentLoader.Load(Read));
        }

        [Fact]
        public void Get_throws_on_unknown_id_while_try_returns_null()
        {
            var db = new ContentDb();
            db.Classes["knight"] = new ClassDef { Id = "knight", Name = "Knight" };
            db.Items["potion"] = new ItemDef { Id = "potion", Name = "Potion" };
            db.Stages["s1"] = new StageDef { Id = "s1", Name = "S1" };
            db.Abilities["slash"] = new AbilityDef { Id = "slash", Name = "Slash" };
            db.Buffs["burning"] = new BuffDef { Id = "burning", Name = "Burning" };

            Assert.Throws<KeyNotFoundException>(() => db.Class("missing"));
            Assert.Throws<KeyNotFoundException>(() => db.Item("missing"));
            Assert.Throws<KeyNotFoundException>(() => db.Stage("missing"));
            Assert.Throws<KeyNotFoundException>(() => db.Ability("missing"));
            Assert.Throws<KeyNotFoundException>(() => db.Buff("missing"));

            Assert.Null(db.TryClass("missing"));
            Assert.Null(db.TryItem("missing"));
            Assert.Null(db.TryStage("missing"));
            Assert.Null(db.TryAbility("missing"));
            Assert.Null(db.TryBuff("missing"));

            Assert.Equal("knight", db.Class("knight").Id);
            Assert.Equal("potion", db.Item("potion").Id);
            Assert.Equal("s1", db.Stage("s1").Id);
            Assert.Equal("slash", db.Ability("slash").Id);
            Assert.Equal("burning", db.Buff("burning").Id);
        }

        [Fact]
        public void ThemeOf_resolves_a_stage_to_its_theme_or_null()
        {
            var db = new ContentDb();
            db.Stages["s1"] = new StageDef { Id = "s1", Name = "S1" };
            db.Stages["s2"] = new StageDef { Id = "s2", Name = "S2" };
            var theme1 = new ThemeDef { Id = "t1", Name = "T1", Stages = { "s1" } };
            var theme2 = new ThemeDef { Id = "t2", Name = "T2", Stages = { "s2" } };
            db.Campaign.Themes.Add(theme1);
            db.Campaign.Themes.Add(theme2);

            Assert.Same(theme1, db.ThemeOf("s1"));
            Assert.Same(theme2, db.ThemeOf("s2"));
            Assert.Null(db.ThemeOf("missing"));
        }

        [Fact]
        public void CampaignStages_orders_by_theme_then_by_stage_within_the_theme()
        {
            var db = new ContentDb();
            foreach (var id in new[] { "s1", "s2", "s3", "s4" })
                db.Stages[id] = new StageDef { Id = id, Name = id };

            db.Campaign.Themes.Add(new ThemeDef { Id = "t1", Name = "T1", Stages = { "s2", "s1" } });
            db.Campaign.Themes.Add(new ThemeDef { Id = "t2", Name = "T2", Stages = { "s4", "s3" } });

            var ordered = db.CampaignStages().Select(s => s.Id).ToList();
            Assert.Equal(new[] { "s2", "s1", "s4", "s3" }, ordered);
        }

        [Fact]
        public void CampaignStages_skips_stage_ids_with_no_matching_stage()
        {
            var db = new ContentDb();
            db.Stages["s1"] = new StageDef { Id = "s1", Name = "S1" };
            db.Campaign.Themes.Add(new ThemeDef { Id = "t1", Name = "T1", Stages = { "missing", "s1" } });

            Assert.Equal(new[] { "s1" }, db.CampaignStages().Select(s => s.Id));
        }
    }
}
