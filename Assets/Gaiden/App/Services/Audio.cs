using System.Collections.Generic;
using UnityEngine;

namespace Gaiden.App
{
    /// <summary>Music + SFX by key. Every battle event maps to a cue (bible §12.0: every event gets a sound).</summary>
    public sealed class Audio : MonoBehaviour
    {
        private AudioSource music;
        private readonly List<AudioSource> sfx = new List<AudioSource>();
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private string currentTrack;
        public string CurrentTrack => currentTrack;
        /// <summary>Recent cues (for automation checks that every event is voiced).</summary>
        public readonly List<string> CueLog = new List<string>();
        private float musicVolume = 0.6f, sfxVolume = 0.85f;
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

        public static Audio Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<Audio>();
            a.music = go.AddComponent<AudioSource>();
            a.music.loop = true;
            a.music.playOnAwake = false;
            for (int i = 0; i < 10; i++)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                a.sfx.Add(s);
            }
            return a;
        }

        public void Apply(Meta.SettingsSave s)
        {
            musicVolume = s.MuteMusic ? 0f : s.MusicVolume;
            sfxVolume = s.MuteSfx ? 0f : s.SfxVolume;
            music.volume = musicVolume;
        }

        private AudioClip Clip(string path)
        {
            if (clips.TryGetValue(path, out var c)) return c;
            c = Resources.Load<AudioClip>(path);
            clips[path] = c;
            return c;
        }

        public void Music(string track)
        {
            if (track == currentTrack) return;
            currentTrack = track;
            var c = track == null ? null : Clip("Audio/Music/" + track);
            music.Stop();
            if (c == null) return;
            music.clip = c;
            music.volume = musicVolume;
            music.Play();
        }

        /// <summary>Plays a cue. Rapid repeats of the same cue are thinned so chains don't clip into noise.</summary>
        public void Sfx(string key, float volume = 1f, float pitch = 1f)
        {
            if (string.IsNullOrEmpty(key)) return;
            CueLog.Add(key);
            if (CueLog.Count > 300) CueLog.RemoveAt(0);
            if (sfxVolume <= 0f) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(key, out var last) && now - last < 0.04f) return;
            lastPlayed[key] = now;
            var c = Clip("Audio/Sfx/" + Cue(key));
            if (c == null) return;
            AudioSource src = sfx[0];
            foreach (var s in sfx) if (!s.isPlaying) { src = s; break; }
            src.pitch = pitch;
            src.PlayOneShot(c, volume * sfxVolume);
        }

        /// <summary>Semantic cue → file. Keep game code speaking in cues, not filenames.</summary>
        private static string Cue(string key)
        {
            switch (key)
            {
                case "ui.click": return "Click";
                case "ui.select": return "Select";
                case "ui.back": return "Click";
                case "ui.buy": return "SFX_Pick_Up";
                case "ui.error": return "Rumble";
                case "move": return "Move0" + Random.Range(0, 6);
                case "slide": return "Slide";
                case "pickup": return "Portrait";
                case "pincer": return "Vefects_SFX_Charge_01";
                case "hit": return "Slash0" + Random.Range(0, 7);
                case "crit": return "Vefects_SFX_Impact_01";
                case "miss": return "Vefects_SFX_Dust_Puff_01";
                case "death": return "Death";
                case "heal": return "SFX_Heal_Cast";
                case "orb": return "SFX_Pick_Up";
                case "turn": return "NextTurn";
                case "fire": return "SFX_FireBall_Hit";
                case "ice": return "SFX_Explosion_Ice";
                case "lightning": return "SFX_Lightning_Hit";
                case "poison": return "Vefects_SFX_Poison_Puff_01";
                case "magic": return "Vefects_SFX_Explosion_Magic_01";
                case "cast": return "SFX_Buff_Cast";
                case "explosion": return "Vefects_SFX_Explosion_Classic";
                case "shatter": return "Vefects_SFX_Shrapnel_01";
                case "debuff": return "SFX_Debuff_Cast";
                case "dash": return "SFX_Dash";
                case "trap": return "Vefects_SFX_Poison_Puff_01";
                case "victory": return "Victory";
                case "defeat": return "Defeat";
                default: return key;
            }
        }
    }
}
