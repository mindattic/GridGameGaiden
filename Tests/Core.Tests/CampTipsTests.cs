using System.Linq;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T173: the camp tutorial shows each station's tip once, never to bots, and can be replayed.</summary>
    public class CampTipsTests
    {
        [Test]
        public void A_station_tip_is_pending_until_marked_seen()
        {
            var s = new SaveData();
            Assert.That(CampTips.Pending(s, "shop"), Is.Not.Null);
            CampTips.MarkSeen(s, "shop");
            Assert.That(CampTips.Pending(s, "shop"), Is.Null);
            Assert.That(CampTips.Pending(s, "blacksmith"), Is.Not.Null, "other stations are unaffected");
        }

        [Test]
        public void Bot_played_saves_and_unknown_stations_get_no_tip()
        {
            Assert.That(CampTips.Pending(new SaveData { TestMode = true }, "shop"), Is.Null);
            Assert.That(CampTips.Pending(new SaveData(), "settings"), Is.Null);
            Assert.That(CampTips.Pending(null, "shop"), Is.Null);
        }

        [Test]
        public void Replay_brings_every_tip_back()
        {
            var s = new SaveData();
            foreach (var t in CampTips.All) CampTips.MarkSeen(s, t.Station);
            Assert.That(CampTips.All.Any(t => CampTips.Pending(s, t.Station) != null), Is.False);
            CampTips.ResetAll(s);
            Assert.That(CampTips.All.All(t => CampTips.Pending(s, t.Station) != null), Is.True);
        }

        [Test]
        public void An_older_save_without_the_field_migrates_with_none_seen()
        {
            var s = new SaveData { SeenCampTips = null };
            var back = SaveMigrations.Migrate(s);
            Assert.That(back.SeenCampTips, Is.Not.Null.And.Empty);
        }

        [Test]
        public void The_blacksmith_tip_names_and_points_at_the_most_worn_piece()
        {
            var c = TestKit.Real;
            var s = Progression.NewGame(c, "T");
            var tip = CampTips.For("blacksmith");
            Assert.That(CampTips.Resolve(c, s, tip), Is.SameAs(tip), "no worn gear: the tip reads as written");
            var worn = CampNpcTests.WearLeadWeapon(c, s, 0.5f);
            var resolved = CampTips.Resolve(c, s, tip);
            Assert.That(resolved.Target, Is.EqualTo(Repair.RowNameFor(s.Party[0], GridGame.Core.Content.EquipmentSlot.Weapon)));
            Assert.That(resolved.Text, Does.Contain(worn.Item.Name).And.Contain((int)System.Math.Round(worn.Fraction * 100) + "%"));
            Assert.That(CampTips.Resolve(c, s, CampTips.For("shop")), Is.SameAs(CampTips.For("shop")), "other tips are untouched");
        }

        [Test]
        public void Every_tip_has_one_station_a_title_and_a_sentence()
        {
            Assert.That(CampTips.All.Select(t => t.Station).Distinct().Count(), Is.EqualTo(CampTips.All.Count));
            foreach (var t in CampTips.All)
            {
                Assert.That(t.Title, Is.Not.Empty, t.Station);
                Assert.That(t.Text, Is.Not.Empty, t.Station);
            }
        }
    }
}
