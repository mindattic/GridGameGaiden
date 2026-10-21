using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using NUnit.Framework;

namespace GridGame.Tests
{
    public class ContentLoaderTests
    {
        [Test]
        public void Load_rejects_unknown_json_members()
        {
            string Read(string name) => name == "classes"
                ? "[{\"Id\":\"x\",\"Name\":\"X\",\"Team\":\"Hero\",\"NotARealField\":1}]"
                : null;

            Assert.Throws<FormatException>(() => ContentLoader.Load(Read));
        }

        [Test]
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

            Assert.That(db.TryClass("missing"), Is.Null);
            Assert.That(db.TryItem("missing"), Is.Null);
            Assert.That(db.TryStage("missing"), Is.Null);
            Assert.That(db.TryAbility("missing"), Is.Null);
            Assert.That(db.TryBuff("missing"), Is.Null);

            Assert.That(db.Class("knight").Id, Is.EqualTo("knight"));
            Assert.That(db.Item("potion").Id, Is.EqualTo("potion"));
            Assert.That(db.Stage("s1").Id, Is.EqualTo("s1"));
            Assert.That(db.Ability("slash").Id, Is.EqualTo("slash"));
            Assert.That(db.Buff("burning").Id, Is.EqualTo("burning"));
        }

        [Test]
        public void ThemeOf_resolves_a_stage_to_its_theme_or_null()
        {
            var db = new ContentDb();
            db.Stages["s1"] = new StageDef { Id = "s1", Name = "S1" };
            db.Stages["s2"] = new StageDef { Id = "s2", Name = "S2" };
            var theme1 = new ThemeDef { Id = "t1", Name = "T1", Stages = { "s1" } };
            var theme2 = new ThemeDef { Id = "t2", Name = "T2", Stages = { "s2" } };
            db.Campaign.Themes.Add(theme1);
            db.Campaign.Themes.Add(theme2);

            Assert.That(db.ThemeOf("s1"), Is.SameAs(theme1));
            Assert.That(db.ThemeOf("s2"), Is.SameAs(theme2));
            Assert.That(db.ThemeOf("missing"), Is.Null);
        }

        [Test]
        public void CampaignStages_orders_by_theme_then_by_stage_within_the_theme()
        {
            var db = new ContentDb();
            foreach (var id in new[] { "s1", "s2", "s3", "s4" })
                db.Stages[id] = new StageDef { Id = id, Name = id };

            db.Campaign.Themes.Add(new ThemeDef { Id = "t1", Name = "T1", Stages = { "s2", "s1" } });
            db.Campaign.Themes.Add(new ThemeDef { Id = "t2", Name = "T2", Stages = { "s4", "s3" } });

            var ordered = db.CampaignStages().Select(s => s.Id).ToList();
            Assert.That(ordered, Is.EqualTo(new[] { "s2", "s1", "s4", "s3" }));
        }

        [Test]
        public void CampaignStages_skips_stage_ids_with_no_matching_stage()
        {
            var db = new ContentDb();
            db.Stages["s1"] = new StageDef { Id = "s1", Name = "S1" };
            db.Campaign.Themes.Add(new ThemeDef { Id = "t1", Name = "T1", Stages = { "missing", "s1" } });

            Assert.That(db.CampaignStages().Select(s => s.Id), Is.EqualTo(new[] { "s1" }));
        }
    }
}
