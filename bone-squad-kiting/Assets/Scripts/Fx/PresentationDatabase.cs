using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoneSquadKiting.Fx
{
    [Serializable]
    public abstract class PresentationPreset
    {
        public string Id;
        public VolumeProfile Profile;
    }

    [Serializable]
    public sealed class PresentationEffect : PresentationPreset
    {
        [Min(0f)] public float ShakeStrength;
        [Min(0f)] public float ShakeSeconds;
        [Min(0f)] public float HoldSeconds = 0.1f;
        [Min(0f)] public float ReturnSeconds = 0.2f;
    }

    [Serializable]
    public sealed class PresentationMood : PresentationPreset
    {
        [Min(0f)] public float TransitionSeconds = 0.3f;
    }

    [CreateAssetMenu(fileName = "PresentationDatabase", menuName = "BoneSquadKiting/Fx/Presentation Database")]
    public sealed class PresentationDatabase : ScriptableObject
    {
        public PresentationEffect[] Effects = Array.Empty<PresentationEffect>();
        public PresentationMood[] Moods = Array.Empty<PresentationMood>();

        public PresentationEffect FindEffect(string id) => Find(Effects, id);
        public PresentationMood FindMood(string id) => Find(Moods, id);

        static T Find<T>(T[] presets, string id) where T : PresentationPreset
        {
            if (presets == null || string.IsNullOrWhiteSpace(id)) return null;
            foreach (var preset in presets)
                if (preset != null && preset.Id == id) return preset;
            return null;
        }
    }
}
