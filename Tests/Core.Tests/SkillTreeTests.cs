using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Content;
using Gaiden.Meta;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>Archetype skill trees (§25.6.1, GG-T50): one tree per hero archetype, ContentLoader validates
    /// every node's grant and prerequisites, and every hero class names a home tree that exists.</summary>
    public class SkillTreeTests
    {
        [Fact]
        public void Every_hero_class_names_a_real_home_tree()
        {
            var c = Real;
            foreach (var cls in c.Classes.Values.Where(x => x.Team == Team.Hero))
            {
                Assert.False(string.IsNullOrEmpty(cls.HomeTree), $"{cls.Id}: no HomeTree");
                Assert.True(c.SkillTrees.ContainsKey(cls.HomeTree), $"{cls.Id}: unknown home tree '{cls.HomeTree}'");
            }
        }

        [Fact]
        public void Every_hero_archetype_in_play_has_a_tree()
        {
            var c = Real;
            var archetypes = c.Classes.Values.Where(x => x.Team == Team.Hero).Select(x => x.HomeTree).Distinct().ToList();
            Assert.NotEmpty(archetypes);
            foreach (var a in archetypes) Assert.NotEmpty(c.SkillTrees[a].Nodes);
        }

        [Fact]
        public void Every_node_grant_resolves_to_a_real_ability_or_passive()
        {
            var c = Real;
            foreach (var t in c.SkillTrees.Values)
                foreach (var n in t.Nodes)
                {
                    Assert.True(c.Abilities.ContainsKey(n.Grants), $"{t.Id}/{n.Id}: unknown grant '{n.Grants}'");
                    Assert.True(n.Cost > 0, $"{t.Id}/{n.Id}: cost must be positive");
                    Assert.True(n.RequiredPointsInTree >= 0, $"{t.Id}/{n.Id}: required points can't be negative");
                }
        }

        [Fact]
        public void Every_prerequisite_points_at_a_node_in_the_same_tree()
        {
            var c = Real;
            foreach (var t in c.SkillTrees.Values)
            {
                var ids = new HashSet<string>(t.Nodes.Select(n => n.Id));
                foreach (var n in t.Nodes)
                    foreach (var req in n.Prerequisites)
                        Assert.Contains(req, ids);
            }
        }

        /// <summary>GG-T50: "every hero starts there with its existing default abilities pre-unlocked" — the
        /// hero's starting bar (<see cref="ClassDef.DefaultLoadout"/>) is Known from level 1 regardless of
        /// what's still locked in its home tree (<see cref="MetaServices.Progression.NewHero"/>).</summary>
        [Fact]
        public void A_new_hero_already_knows_its_default_loadout_in_its_home_tree_archetype()
        {
            var c = Real;
            foreach (var cls in c.Classes.Values.Where(x => x.Team == Team.Hero))
            {
                Assert.False(string.IsNullOrEmpty(cls.HomeTree), $"{cls.Id}: no home tree");
                var h = Progression.NewHero(c, cls.Id);
                foreach (var entry in cls.DefaultLoadout)
                {
                    if (entry.StartsWith("item:")) continue;
                    Assert.Contains(entry, h.Known);
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

        [Fact]
        public void Validate_catches_every_kind_of_broken_tree_reference()
        {
            var problems = ContentLoader.Validate(BrokenTreeDb());
            Assert.Contains(problems, p => p.Contains("unknown ability 'does_not_exist'"));
            Assert.Contains(problems, p => p.Contains("unknown prerequisite 'nowhere'"));
            Assert.Contains(problems, p => p.Contains("duplicate node id 'root'"));
            Assert.Contains(problems, p => p.Contains("cost must be positive"));
            Assert.Contains(problems, p => p.Contains("prerequisite cycle"));
        }

        [Fact]
        public void Validate_rejects_a_hero_class_missing_or_unknown_home_tree()
        {
            var db = Mini();
            db.Classes["knight"].Role = "Tank";
            db.Classes["knight"].HomeTree = null;
            var problems = ContentLoader.Validate(db);
            Assert.Contains(problems, p => p.Contains("knight") && p.Contains("HomeTree"));

            db.Classes["knight"].HomeTree = "nope";
            problems = ContentLoader.Validate(db);
            Assert.Contains(problems, p => p.Contains("unknown home tree 'nope'"));
        }
    }
}
