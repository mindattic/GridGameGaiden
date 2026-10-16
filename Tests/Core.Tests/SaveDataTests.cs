using System;
using System.Collections.Generic;
using Gaiden.Meta;
using Xunit;

namespace Gaiden.Tests
{
    public class SaveDataTests
    {
        private static SaveData NewSave() => new SaveData
        {
            Roster = { new HeroSave { ClassId = "Paladin" } }
        };

        [Fact]
        public void Migrate_of_null_returns_null()
        {
            Assert.Null(SaveMigrations.Migrate(null));
        }

        [Fact]
        public void Migrate_rejects_a_version_newer_than_current()
        {
            var s = new SaveData { Version = SaveData.CurrentVersion + 1 };
            var ex = Assert.Throws<InvalidOperationException>(() => SaveMigrations.Migrate(s));
            Assert.Contains(s.Version.ToString(), ex.Message);
        }

        [Fact]
        public void Migrate_restores_null_sections()
        {
            var s = NewSave();
            s.Bounty = null;
            s.Worn = null;
            s.Settings = null;
            var back = SaveMigrations.Migrate(s);
            Assert.NotNull(back.Bounty);
            Assert.NotNull(back.Worn);
            Assert.NotNull(back.Settings);
        }

        [Fact]
        public void Migrate_pads_short_hero_bar_and_relics()
        {
            var s = NewSave();
            s.Roster[0].Bar = new List<string> { "attack" };
            s.Roster[0].Relics = new List<string>();
            var back = SaveMigrations.Migrate(s);
            Assert.Equal(5, back.Roster[0].Bar.Count);
            Assert.Equal("attack", back.Roster[0].Bar[0]);
            Assert.Equal(3, back.Roster[0].Relics.Count);
        }

        [Fact]
        public void AddItem_ignores_null_id_and_zero_delta()
        {
            var s = NewSave();
            s.AddItem(null, 5);
            Assert.Empty(s.Inventory);
            s.AddItem("potion", 0);
            Assert.Empty(s.Inventory);
        }

        [Fact]
        public void AddItem_removes_the_key_when_count_drops_to_zero_or_below()
        {
            var s = NewSave();
            s.AddItem("potion", 3);
            Assert.Equal(3, s.Count("potion"));
            s.AddItem("potion", -3);
            Assert.False(s.Inventory.ContainsKey("potion"));
            Assert.Equal(0, s.Count("potion"));

            s.AddItem("elixir", 2);
            s.AddItem("elixir", -9); // drives it below zero, not just to zero
            Assert.False(s.Inventory.ContainsKey("elixir"));
        }

        [Fact]
        public void AddItem_trims_worn_copies_most_worn_first_so_worn_never_outnumbers_owned()
        {
            var s = NewSave();
            s.AddItem("sword", 3);
            s.Worn["sword"] = new List<WornGear>
            {
                new WornGear { Durability = 10 },
                new WornGear { Durability = 50 },
                new WornGear { Durability = 30 },
            };
            s.AddItem("sword", -2); // owned drops to 1; worn must shrink to the least-worn single copy
            Assert.Equal(1, s.Count("sword"));
            var worn = Assert.Single(s.Worn["sword"]);
            Assert.Equal(50, worn.Durability); // highest remaining durability = least worn, so it survives

            s.AddItem("sword", -1); // owned drops to 0; the worn list is removed entirely
            Assert.False(s.Worn.ContainsKey("sword"));
        }

        [Fact]
        public void Hero_lookup_is_case_insensitive()
        {
            var s = NewSave();
            Assert.Same(s.Roster[0], s.Hero("paladin"));
            Assert.Same(s.Roster[0], s.Hero("PALADIN"));
            Assert.Null(s.Hero("cleric"));
        }

        [Fact]
        public void Count_of_a_missing_item_is_zero()
        {
            var s = NewSave();
            Assert.Equal(0, s.Count("nonexistent"));
            Assert.Equal(0, s.Count(null));
        }
    }
}
