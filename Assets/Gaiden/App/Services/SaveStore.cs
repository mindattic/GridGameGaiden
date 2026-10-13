using System;
using System.IO;
using System.Runtime.InteropServices;
using Gaiden.Meta;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace Gaiden.App
{
    /// <summary>Three save slots as JSON files (atomic write via temp + replace). Root is overridable for tests/automation.</summary>
    public sealed class SaveStore
    {
        public const int Slots = 3;
        public readonly string Root;

        /// <summary>
        /// Save files get their own settings rather than reusing <see cref="ContentLoader.Settings"/>: that one's
        /// MissingMemberHandling.Error is right for strict content validation, but applied to a save it would throw
        /// on deserialization the moment any field is renamed/removed — before SaveMigrations.Migrate ever runs,
        /// defeating the migration path SaveData documents. Saves tolerate unknown/stale properties instead.
        /// </summary>
        private static readonly JsonSerializerSettings SaveSettings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public SaveStore(string root = null)
        {
            Root = root ?? Path.Combine(Application.persistentDataPath, "Saves");
            Directory.CreateDirectory(Root);
        }

        private string PathFor(int slot) => Path.Combine(Root, $"slot{slot + 1}.json");

        public bool Exists(int slot) => File.Exists(PathFor(slot));

        public SaveData Load(int slot)
        {
            var p = PathFor(slot);
            if (!File.Exists(p)) return null;
            try
            {
                var s = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(p), SaveSettings);
                return SaveMigrations.Migrate(s);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] slot {slot + 1} unreadable: {e.Message}");
                return null;
            }
        }

        public void Write(int slot, SaveData s)
        {
            s.UpdatedUtc = DateTime.UtcNow;
            var p = PathFor(slot);
            var tmp = p + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(s, Formatting.Indented, SaveSettings));
            try
            {
                if (File.Exists(p)) File.Replace(tmp, p, null);
                else File.Move(tmp, p);
            }
            catch (PlatformNotSupportedException)
            {
                // WebGL's file system has no atomic replace.
                if (File.Exists(p)) File.Delete(p);
                File.Move(tmp, p);
            }
            Flush();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GaidenSyncFiles();
        private static void Flush() => GaidenSyncFiles();
#else
        private static void Flush() { }
#endif

        public void Delete(int slot)
        {
            var p = PathFor(slot);
            if (File.Exists(p)) File.Delete(p);
            Flush();
        }

        public int MostRecent()
        {
            int best = -1;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < Slots; i++)
            {
                var p = PathFor(i);
                if (!File.Exists(p)) continue;
                var t = File.GetLastWriteTimeUtc(p);
                if (t > bestTime) { bestTime = t; best = i; }
            }
            return best;
        }
    }
}
