using System;
using UnityEngine;

namespace BoneSquadKiting.Audio
{
    [Serializable]
    public class SoundEntry
    {
        public string Id;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1f;
        [Range(0.01f, 3f)] public float Pitch = 1f;
    }

    [Serializable]
    public sealed class BgmEntry : SoundEntry
    {
        public bool Loop = true;
    }

    [CreateAssetMenu(fileName = "SoundDatabase", menuName = "BoneSquadKiting/Audio/Sound Database")]
    public sealed class SoundDatabaseSO : ScriptableObject
    {
        public BgmEntry[] Bgm = Array.Empty<BgmEntry>();
        public SoundEntry[] Sfx = Array.Empty<SoundEntry>();

        public BgmEntry FindBgm(string id) => Find(Bgm, id);
        public SoundEntry FindSfx(string id) => Find(Sfx, id);

        static T Find<T>(T[] entries, string id) where T : SoundEntry
        {
            if (entries == null || string.IsNullOrWhiteSpace(id)) return null;
            foreach (var entry in entries)
                if (entry != null && entry.Id == id) return entry;
            return null;
        }
    }
}
