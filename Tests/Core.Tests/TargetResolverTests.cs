using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;
using NUnit.Framework;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>Shape × mode × filter targeting (bible §5, GG-LAW-5) — Tiles, Passes, Collect, IsValidPick, Opposite.</summary>
    public class TargetResolverTests
    {
        private static readonly TargetShape[] AllShapes =
        {
            TargetShape.Self, TargetShape.SingleActor, TargetShape.SingleTile,
            TargetShape.Square, TargetShape.Diamond, TargetShape.Cross,
            TargetShape.Plus, TargetShape.Row, TargetShape.Column,
        };

        /// <summary>Board positions exercising centre (no clipping), corners and edges (clipping on one or both axes).</summary>
        private static readonly (string label, Coord anchor)[] Anchors =
        {
            ("centre", new Coord(3, 4)),
            ("top-left corner", new Coord(0, 0)),
            ("top-right corner", new Coord(BattleConfig.Cols - 1, 0)),
            ("bottom-left corner", new Coord(0, BattleConfig.Rows - 1)),
            ("bottom-right corner", new Coord(BattleConfig.Cols - 1, BattleConfig.Rows - 1)),
            ("left edge", new Coord(0, 3)),
            ("right edge", new Coord(BattleConfig.Cols - 1, 3)),
            ("top edge", new Coord(3, 0)),
            ("bottom edge", new Coord(3, BattleConfig.Rows - 1)),
        };

        /// <summary>
        /// Independent oracle for the expected tile set: Square/Diamond use Coord's own Chebyshev/Manhattan
        /// distance (not TargetResolver's switch), the others use the plain geometric definition of the shape.
        /// </summary>
        private static HashSet<Coord> ExpectedTiles(Coord anchor, TargetShape shape, int radius)
        {
            var result = new HashSet<Coord>();
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    bool hit;
                    switch (shape)
                    {
                        case TargetShape.Self:
                        case TargetShape.SingleActor:
                        case TargetShape.SingleTile: hit = c.Equals(anchor); break;
                        case TargetShape.Square: hit = c.Chebyshev(anchor) <= radius; break;
                        case TargetShape.Diamond: hit = c.Manhattan(anchor) <= radius; break;
                        case TargetShape.Cross: hit = (c.X == anchor.X && System.Math.Abs(c.Y - anchor.Y) <= radius) || (c.Y == anchor.Y && System.Math.Abs(c.X - anchor.X) <= radius); break;
                        case TargetShape.Plus: hit = c.X == anchor.X || c.Y == anchor.Y; break;
                        case TargetShape.Row: hit = c.Y == anchor.Y; break;
                        case TargetShape.Column: hit = c.X == anchor.X; break;
                        default: hit = false; break;
                    }
                    if (hit) result.Add(c);
                }
            return result;
        }

        public static IEnumerable<object[]> ShapeAnchorRadiusCombos()
        {
            foreach (var shape in AllShapes)
                foreach (var (label, anchor) in Anchors)
                    for (int radius = 0; radius <= 2; radius++)
                        yield return new object[] { shape, label, anchor, radius };
        }
        [TestCaseSource(nameof(ShapeAnchorRadiusCombos))]
        public void Tiles_matches_the_shapes_geometric_definition_clipped_to_the_board(TargetShape shape, string anchorLabel, Coord anchor, int radius)
        {
            var actual = TargetResolver.Tiles(anchor, shape, radius);
            var expected = ExpectedTiles(anchor, shape, radius);
            Assert.That(actual.Count == actual.Distinct().Count(), Is.True, $"{shape} at {anchorLabel} r{radius}: duplicate tiles returned");
            foreach (var c in actual) Assert.That(BattleState.InBounds(c), Is.True, $"{shape} at {anchorLabel} r{radius}: {c} is off-board");
            Assert.That(actual.ToHashSet(), Is.EqualTo(expected));
        }

        // ── Passes (filters) ──

        [Test]
        public void Passes_enemy_only_accepts_the_opposing_team_and_rejects_own()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var goblin = At(s, 2, 2);
            Assert.That(TargetResolver.Passes(TargetFilter.EnemyOnly, knight, goblin), Is.True);
            Assert.That(TargetResolver.Passes(TargetFilter.EnemyOnly, knight, knight), Is.False);
        }

        [Test]
        public void Passes_ally_only_accepts_own_team_and_rejects_the_opponent()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 5, 7), ("cleric", 4, 7));
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            var goblin = At(s, 2, 2);
            Assert.That(TargetResolver.Passes(TargetFilter.AllyOnly, knight, cleric), Is.True);
            Assert.That(TargetResolver.Passes(TargetFilter.AllyOnly, knight, goblin), Is.False);
        }

        [Test]
        public void Passes_empty_only_rejects_every_occupied_unit()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var goblin = At(s, 2, 2);
            Assert.That(TargetResolver.Passes(TargetFilter.EmptyOnly, knight, goblin), Is.False);
            Assert.That(TargetResolver.Passes(TargetFilter.EmptyOnly, knight, knight), Is.False);
        }

        [Test]
        public void Passes_default_filter_accepts_any_unit_regardless_of_team()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var goblin = At(s, 2, 2);
            Assert.That(TargetResolver.Passes(TargetFilter.Any, knight, goblin), Is.True);
            Assert.That(TargetResolver.Passes(TargetFilter.Any, knight, knight), Is.True);
        }

        // ── Opposite ──

        [Test]
        public void Opposite_flips_hero_and_enemy()
        {
            Assert.That(TargetResolver.Opposite(Team.Hero), Is.EqualTo(Team.Enemy));
            Assert.That(TargetResolver.Opposite(Team.Enemy), Is.EqualTo(Team.Hero));
        }

        // ── Collect ──

        [Test]
        public void Collect_counts_a_2x2_boss_once_even_when_the_shape_covers_several_of_its_tiles()
        {
            var db = Mini();
            db.Abilities["bigsquare"] = new AbilityDef { Id = "bigsquare", Name = "Big Square", Kind = AbilityKind.Spell, Shape = TargetShape.Square, Radius = 2, Mode = TargetMode.PickTile, Filter = TargetFilter.EnemyOnly };
            var (s, _) = Battle(db, Stage(("boss", 2, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var boss = At(s, 2, 2);
            var hit = TargetResolver.Collect(s, db.Abilities["bigsquare"], knight, new Coord(3, 3));
            Assert.That(hit, Has.Exactly(1).Items);
            Assert.That(hit[0], Is.SameAs(boss));
        }

        [Test]
        public void Collect_excludes_dead_units()
        {
            var db = Mini();
            db.Abilities["bigsquare"] = new AbilityDef { Id = "bigsquare", Name = "Big Square", Kind = AbilityKind.Spell, Shape = TargetShape.Square, Radius = 2, Mode = TargetMode.PickTile, Filter = TargetFilter.EnemyOnly };
            var (s, _) = Battle(db, Stage(("goblin", 2, 2), ("wolf", 3, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var goblin = At(s, 2, 2);
            goblin.Hp = 0;
            var hit = TargetResolver.Collect(s, db.Abilities["bigsquare"], knight, new Coord(2, 2));
            Assert.That(hit.Any(u => u.Def.Id == "goblin"), Is.False);
            Assert.That(hit.Any(u => u.Def.Id == "wolf"), Is.True);
        }

        [Test]
        public void Collect_excludes_off_board_units()
        {
            var db = Mini();
            db.Abilities["bigsquare"] = new AbilityDef { Id = "bigsquare", Name = "Big Square", Kind = AbilityKind.Spell, Shape = TargetShape.Square, Radius = 2, Mode = TargetMode.PickTile, Filter = TargetFilter.EnemyOnly };
            var (s, _) = Battle(db, Stage(("goblin", 2, 2), ("wolf", 3, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var goblin = At(s, 2, 2);
            goblin.OnBoard = false;
            var hit = TargetResolver.Collect(s, db.Abilities["bigsquare"], knight, new Coord(2, 2));
            Assert.That(hit.Any(u => u.Def.Id == "goblin"), Is.False);
            Assert.That(hit.Any(u => u.Def.Id == "wolf"), Is.True);
        }

        [Test]
        public void Collect_self_shape_returns_only_the_living_caster()
        {
            var db = Mini();
            db.Abilities["selfbuff"] = new AbilityDef { Id = "selfbuff", Name = "Self Buff", Kind = AbilityKind.Skill, Shape = TargetShape.Self, Mode = TargetMode.Auto, Filter = TargetFilter.Any };
            var (s, _) = Battle(db, Stage(("goblin", 2, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var hit = TargetResolver.Collect(s, db.Abilities["selfbuff"], knight, TargetResolver.AutoAnchor(knight));
            Assert.That(hit, Has.Exactly(1).Items);
            Assert.That(hit[0], Is.SameAs(knight));
        }

        [Test]
        public void Collect_self_shape_returns_nothing_for_a_dead_caster()
        {
            var db = Mini();
            db.Abilities["selfbuff"] = new AbilityDef { Id = "selfbuff", Name = "Self Buff", Kind = AbilityKind.Skill, Shape = TargetShape.Self, Mode = TargetMode.Auto, Filter = TargetFilter.Any };
            var (s, _) = Battle(db, Stage(("goblin", 2, 2)), ("knight", 5, 7), ("cleric", 4, 7));
            var knight = Hero(s, "knight");
            knight.Hp = 0;
            var hit = TargetResolver.Collect(s, db.Abilities["selfbuff"], knight, TargetResolver.AutoAnchor(knight));
            Assert.That(hit, Is.Empty);
        }

        [Test]
        public void Collect_all_enemies_shape_returns_every_living_enemy_and_skips_the_dead()
        {
            var db = Mini();
            db.Abilities["nuke"] = new AbilityDef { Id = "nuke", Name = "Nuke", Kind = AbilityKind.Spell, Shape = TargetShape.AllEnemies, Mode = TargetMode.Auto, Filter = TargetFilter.EnemyOnly };
            var (s, _) = Battle(db, Stage(("goblin", 2, 2), ("wolf", 3, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var goblin = At(s, 2, 2);
            goblin.Hp = 0;
            var hit = TargetResolver.Collect(s, db.Abilities["nuke"], knight, TargetResolver.AutoAnchor(knight));
            Assert.That(hit, Has.Exactly(1).Items);
            Assert.That(hit[0].Def.Id, Is.EqualTo("wolf"));
        }

        [Test]
        public void Collect_all_allies_shape_returns_every_living_hero()
        {
            var db = Mini();
            db.Abilities["heal_all"] = new AbilityDef { Id = "heal_all", Name = "Heal All", Kind = AbilityKind.Spell, Shape = TargetShape.AllAllies, Mode = TargetMode.Auto, Filter = TargetFilter.AllyOnly };
            var (s, _) = Battle(db, Stage(("goblin", 2, 2)), ("knight", 5, 7), ("cleric", 4, 7));
            var cleric = Hero(s, "cleric");
            var hit = TargetResolver.Collect(s, db.Abilities["heal_all"], cleric, TargetResolver.AutoAnchor(cleric));
            Assert.That(hit.Count, Is.EqualTo(2));
            Assert.That(hit.Any(u => u.Def.Id == "knight"), Is.True);
            Assert.That(hit.Any(u => u.Def.Id == "cleric"), Is.True);
        }

        // ── IsValidPick ──

        [Test]
        public void IsValidPick_rejects_a_target_off_the_board()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var fireball = s.Content.Abilities["fireball"];
            Assert.That(TargetResolver.IsValidPick(s, fireball, knight, new Coord(-1, 0)), Is.False);
            Assert.That(TargetResolver.IsValidPick(s, fireball, knight, new Coord(BattleConfig.Cols, 0)), Is.False);
        }

        [Test]
        public void IsValidPick_auto_mode_always_accepts_an_in_bounds_tile()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("goblin", 2, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var steal = s.Content.Abilities["steal"]; // Mode = Auto
            Assert.That(TargetResolver.IsValidPick(s, steal, knight, new Coord(0, 0)), Is.True);
        }

        [Test]
        public void IsValidPick_empty_only_rejects_an_occupied_tile_and_accepts_a_free_one()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var teleport = s.Content.Abilities["teleport"]; // Filter = EmptyOnly, Mode = PickTile
            Assert.That(TargetResolver.IsValidPick(s, teleport, knight, new Coord(2, 2)), Is.False);
            Assert.That(TargetResolver.IsValidPick(s, teleport, knight, new Coord(0, 0)), Is.True);
        }

        [Test]
        public void IsValidPick_empty_only_rejects_a_tile_holding_a_trap()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var teleport = s.Content.Abilities["teleport"];
            s.Traps.Add(new Trap { At = new Coord(0, 0), TrapId = "spike" });
            Assert.That(TargetResolver.IsValidPick(s, teleport, knight, new Coord(0, 0)), Is.False);
        }

        [Test]
        public void IsValidPick_adjacent_only_rejects_a_non_adjacent_or_empty_target_and_accepts_an_adjacent_enemy()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 4)), ("knight", 2, 3), ("cleric", 5, 7));
            var knight = Hero(s, "knight");
            var tank = At(s, 2, 4);
            var shieldBash = s.Content.Abilities["shield_bash"]; // AdjacentOnly, PickActor, EnemyOnly
            Assert.That(TargetResolver.IsValidPick(s, shieldBash, knight, tank.Pos), Is.True);
            Assert.That(TargetResolver.IsValidPick(s, shieldBash, knight, new Coord(2, 3)), Is.False); // empty (self tile)
            var (s2, _) = Battle(db, Stage(("tank", 0, 0)), ("knight", 2, 3), ("cleric", 5, 7));
            var knight2 = Hero(s2, "knight");
            var tank2 = At(s2, 0, 0);
            Assert.That(TargetResolver.IsValidPick(s2, shieldBash, knight2, tank2.Pos), Is.False); // occupied, but not adjacent
        }

        [Test]
        public void IsValidPick_pick_actor_rejects_wrong_team_and_accepts_the_correct_filter()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var goblin = At(s, 2, 2);
            var fireball = s.Content.Abilities["fireball"]; // PickActor, EnemyOnly
            Assert.That(TargetResolver.IsValidPick(s, fireball, knight, goblin.Pos), Is.True);
            Assert.That(TargetResolver.IsValidPick(s, fireball, knight, knight.Pos), Is.False);
        }

        [Test]
        public void IsValidPick_pick_tile_accepts_only_tiles_whose_shape_catches_a_valid_unit()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 5, 7));
            var knight = Hero(s, "knight");
            var meteor = s.Content.Abilities["meteor"]; // PickTile, Diamond r2, EnemyOnly
            Assert.That(TargetResolver.IsValidPick(s, meteor, knight, new Coord(2, 2)), Is.True);
            Assert.That(TargetResolver.IsValidPick(s, meteor, knight, new Coord(5, 7)), Is.False); // far corner, no enemy in range
        }
    }
}
