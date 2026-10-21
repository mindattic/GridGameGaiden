using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Archetype skill trees (§25.6.1, GG-T50): one tree per hero archetype, ContentLoader validates
    /// every node's grant and prerequisites, and every hero class names a home tree that exists.</summary>
    public class SkillTreeTests
    {
        [Test]
        public void Every_hero_class_names_a_real_home_tree()
        {
            var c = Real;
            foreach (var cls in c.Classes.Values.Where(x => x.Team == Team.Hero))
            {
                Assert.That(string.IsNullOrEmpty(cls.HomeTree), Is.False, $"{cls.Id}: no HomeTree");
                Assert.That(c.SkillTrees.ContainsKey(cls.HomeTree), Is.True, $"{cls.Id}: unknown home tree '{cls.HomeTree}'");
            }
        }

        [Test]
        public void Every_hero_archetype_in_play_has_a_tree()
        {
            var c = Real;
            var archetypes = c.Classes.Values.Where(x => x.Team == Team.Hero).Select(x => x.HomeTree).Distinct().ToList();
            Assert.That(archetypes, Is.Not.Empty);
            foreach (var a in archetypes) Assert.That(c.SkillTrees[a].Nodes, Is.Not.Empty);
        }

        [Test]
        public void Every_node_grant_resolves_to_a_real_ability_or_passive()
        {
            var c = Real;
            foreach (var t in c.SkillTrees.Values)
                foreach (var n in t.Nodes)
                {
                    Assert.That(c.Abilities.ContainsKey(n.Grants), Is.True, $"{t.Id}/{n.Id}: unknown grant '{n.Grants}'");
                    Assert.That(n.Cost > 0, Is.True, $"{t.Id}/{n.Id}: cost must be positive");
                    Assert.That(n.RequiredPointsInTree >= 0, Is.True, $"{t.Id}/{n.Id}: required points can't be negative");
                }
        }

        [Test]
        public void Every_prerequisite_points_at_a_node_in_the_same_tree()
        {
            var c = Real;
            foreach (var t in c.SkillTrees.Values)
            {
                var ids = new HashSet<string>(t.Nodes.Select(n => n.Id));
                foreach (var n in t.Nodes)
                    foreach (var req in n.Prerequisites)
                        Assert.That(ids, Does.Contain(req));
            }
        }

        /// <summary>GG-T50: "every hero starts there with its existing default abilities pre-unlocked" — the
        /// hero's starting bar (<see cref="ClassDef.DefaultLoadout"/>) is Known from level 1 regardless of
        /// what's still locked in its home tree (<see cref="MetaServices.Progression.NewHero"/>).</summary>
        [Test]
        public void A_new_hero_already_knows_its_default_loadout_in_its_home_tree_archetype()
        {
            var c = Real;
            foreach (var cls in c.Classes.Values.Where(x => x.Team == Team.Hero))
            {
                Assert.That(string.IsNullOrEmpty(cls.HomeTree), Is.False, $"{cls.Id}: no home tree");
                var h = Progression.NewHero(c, cls.Id);
                foreach (var entry in cls.DefaultLoadout)
                {
                    if (entry.StartsWith("item:")) continue;
                    Assert.That(h.Known, Does.Contain(entry));
                }
            }
        }

        private static ContentDb BrokenTreeDb()
        {
            var db = Mini();
            db.Classes["knight"].Role = "Tank";
            db.Classes["knight"].HomeTree = "guardian";
            db.Abilities["shield_bash"] = new AbilityDef { Id = "shield_bash", Name = "Shield Bash", Kind = AbilityKind.Skill };
            db.SkillTrees["guardian"] = new SkillTreeDef
            {
                Id = "guardian",
                Name = "Guardian",
                Nodes =
                {
                    new SkillNodeDef { Id = "root", Grants = "shield_bash", Cost = 1, RequiredPointsInTree = 0 },
                    new SkillNodeDef { Id = "ghost_grant", Grants = "does_not_exist", Cost = 1 },
                    new SkillNodeDef { Id = "ghost_prereq", Grants = "shield_bash", Cost = 1, Prerequisites = { "nowhere" } },
                    new SkillNodeDef { Id = "root", Grants = "shield_bash", Cost = 1 }, // duplicate id
                    new SkillNodeDef { Id = "bad_cost", Grants = "shield_bash", Cost = 0 },
                    new SkillNodeDef { Id = "cycle_a", Grants = "shield_bash", Cost = 1, Prerequisites = { "cycle_b" } },
                    new SkillNodeDef { Id = "cycle_b", Grants = "shield_bash", Cost = 1, Prerequisites = { "cycle_a" } },
                },
            };
            return db;
        }

        [Test]
        public void Validate_catches_every_kind_of_broken_tree_reference()
        {
            var problems = ContentLoader.Validate(BrokenTreeDb());
            Assert.That(problems.Any(p => p.Contains("unknown ability 'does_not_exist'")), Is.True);
            Assert.That(problems.Any(p => p.Contains("unknown prerequisite 'nowhere'")), Is.True);
            Assert.That(problems.Any(p => p.Contains("duplicate node id 'root'")), Is.True);
            Assert.That(problems.Any(p => p.Contains("cost must be positive")), Is.True);
            Assert.That(problems.Any(p => p.Contains("prerequisite cycle")), Is.True);
        }

        [Test]
        public void Validate_rejects_a_hero_class_missing_or_unknown_home_tree()
        {
            var db = Mini();
            db.Classes["knight"].Role = "Tank";
            db.Classes["knight"].HomeTree = null;
            var problems = ContentLoader.Validate(db);
            Assert.That(problems.Any(p => p.Contains("knight") && p.Contains("HomeTree")), Is.True);

            db.Classes["knight"].HomeTree = "nope";
            problems = ContentLoader.Validate(db);
            Assert.That(problems.Any(p => p.Contains("unknown home tree 'nope'")), Is.True);
        }
    }
}
