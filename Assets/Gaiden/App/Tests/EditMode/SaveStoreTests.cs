using System;
using System.IO;
using GridGame.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GridGame.App.Tests
{
    public class SaveStoreTests
    {
        private string root;
        private SaveStore store;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "GaidenSaveStoreTests_" + Guid.NewGuid().ToString("N"));
            store = new SaveStore(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private string SlotPath(int slot) => Path.Combine(root, $"slot{slot + 1}.json");

        [Test]
        public void Load_OfMissingSlot_ReturnsNull()
        {
            Assert.IsNull(store.Load(0));
        }

        [Test]
        public void Load_OfCorruptFile_ReturnsNullInsteadOfThrowing()
        {
            File.WriteAllText(SlotPath(0), "{ this is not valid json ");

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^\\[Save\\] slot 1 unreadable:.*"));
            SaveData loaded = null;
            Assert.DoesNotThrow(() => loaded = store.Load(0));
            Assert.IsNull(loaded);
        }

        [Test]
        public void Load_ToleratesUnknownOrStaleProperties()
        {
            File.WriteAllText(SlotPath(0),
                @"{ ""Name"": ""Tester"", ""Gold"": 10, ""SomeRemovedField"": ""ghost"", ""AnotherGhost"": 123 }");

            var loaded = store.Load(0);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("Tester", loaded.Name);
            Assert.AreEqual(10, loaded.Gold);
        }

        [Test]
        public void Load_RunsMigrate()
        {
            var data = new SaveData();
            data.Roster.Add(new HeroSave { ClassId = "knight" });
            data.Roster[0].Bar.Clear();
            store.Write(0, data);

            var loaded = store.Load(0);

            Assert.AreEqual(5, loaded.Roster[0].Bar.Count);
            Assert.IsNotNull(loaded.Roster[0].Armor);
            Assert.IsNotNull(loaded.Roster[0].Jewelry);
        }

        [Test]
        public void Write_ThenLoad_RoundTrips_AndLeavesNoTmpFile()
        {
            var data = new SaveData { Name = "Hero", Gold = 42 };

            store.Write(0, data);

            Assert.IsFalse(File.Exists(SlotPath(0) + ".tmp"));
            var loaded = store.Load(0);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Hero", loaded.Name);
            Assert.AreEqual(42, loaded.Gold);
        }

        [Test]
        public void Write_Overwrite_Replaces()
        {
            store.Write(0, new SaveData { Gold = 1 });
            store.Write(0, new SaveData { Gold = 2 });

            Assert.AreEqual(2, store.Load(0).Gold);
        }

        [Test]
        public void Write_OnSuccess_ReturnsTrue()
        {
            Assert.IsTrue(store.Write(0, new SaveData()));
        }

        [Test]
        public void Write_WhenTheTempFileCannotBeWritten_ReturnsFalseInsteadOfThrowing()
        {
            // Hold the .tmp path open exclusively so File.WriteAllText fails the way a locked file would.
            var tmp = SlotPath(0) + ".tmp";
            using (new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^\\[Save\\] slot 1 write failed:.*"));
                bool ok = true;
                Assert.DoesNotThrow(() => ok = store.Write(0, new SaveData { Gold = 99 }));
                Assert.IsFalse(ok);
            }
        }

        [Test]
        public void Write_WhenTheReplaceStepFails_ReturnsFalseAndLeavesThePreviousSaveIntact()
        {
            store.Write(0, new SaveData { Gold = 1 });
            var p = SlotPath(0);

            // Lock the existing save file so File.Replace can't take it, the way an OS-level lock would.
            using (new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^\\[Save\\] slot 1 write failed:.*"));
                bool ok = true;
                Assert.DoesNotThrow(() => ok = store.Write(0, new SaveData { Gold = 2 }));
                Assert.IsFalse(ok);
            }

            Assert.IsFalse(File.Exists(p + ".tmp"), "a failed write should not leave a stray .tmp file behind");
            Assert.AreEqual(1, store.Load(0).Gold, "the previous save must survive a failed write");
        }

        [Test]
        public void Delete_RemovesSave()
        {
            store.Write(0, new SaveData());
            Assert.IsTrue(store.Exists(0));

            store.Delete(0);

            Assert.IsFalse(store.Exists(0));
            Assert.IsNull(store.Load(0));
        }

        [Test]
        public void Exists_ReflectsPresence()
        {
            Assert.IsFalse(store.Exists(0));
            store.Write(0, new SaveData());
            Assert.IsTrue(store.Exists(0));
        }

        [Test]
        public void MostRecent_IsMinusOne_WhenEmpty()
        {
            Assert.AreEqual(-1, store.MostRecent());
        }

        [Test]
        public void MostRecent_PicksLatestWrite()
        {
            store.Write(0, new SaveData());
            store.Write(1, new SaveData());
            store.Write(2, new SaveData());

            var now = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(SlotPath(0), now.AddMinutes(-3));
            File.SetLastWriteTimeUtc(SlotPath(1), now.AddMinutes(-1));
            File.SetLastWriteTimeUtc(SlotPath(2), now.AddMinutes(-2));

            Assert.AreEqual(1, store.MostRecent());
        }
    }
}
