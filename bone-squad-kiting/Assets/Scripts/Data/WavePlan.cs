using System.Collections.Generic;
using UnityEngine;

namespace BoneSquadKiting.Data
{
    /// <summary>One kind of mob and how many of it.</summary>
    [System.Serializable]
    public class WaveEntry
    {
        public MobDefinition Mob;
        [Min(0)] public int Count = 8;
    }

    /// <summary>What the waves are like from a given progress onward.</summary>
    [System.Serializable]
    public class WaveSection
    {
        [Tooltip("This row applies once the squad has come this far (0 = start point, 1 = end point).")]
        [Range(0f, 1f)] public float FromProgress;
        [Tooltip("Seconds between waves.")]
        [Min(0.5f)] public float Interval = 10f;
        [Tooltip("What one wave is made of.")]
        public List<WaveEntry> Mobs = new();
    }

    /// <summary>A one-off group that arrives the first time the squad reaches a given progress.</summary>
    [System.Serializable]
    public class WaveBurst
    {
        [Range(0f, 1f)] public float AtProgress = 0.5f;
        public List<WaveEntry> Mobs = new();
    }

    /// <summary>
    /// The whole wave setting of one stage, as one asset: one asset is one version. Duplicate it to make
    /// the next version (Stage1_v01, Stage1_v02 …) and point the stage at the one to play.
    ///
    /// Waves never end. Which row of <see cref="Sections"/> applies follows the furthest progress the squad
    /// has reached, so standing still neither stops the waves nor speeds them up.
    /// </summary>
    [CreateAssetMenu(menuName = "BoneSquadKiting/Wave Plan")]
    public class WavePlan : ScriptableObject
    {
        [TextArea(2, 5)] public string Note;            // what this version changed

        [Min(0f)] public float FirstWaveDelay = 3f;

        [Tooltip("Off: each row applies as written until the next row starts. On: interval and counts slide smoothly from one row to the next.")]
        public bool Blend;
        public List<WaveSection> Sections = new();
        public List<WaveBurst> Bursts = new();

        [Header("Where mobs appear")]
        public float MinSpawnDistance = 14f;   // from the squad's center: far enough to be off screen
        public float MaxSpawnDistance = 22f;
        [Range(0f, 1f)] public float AheadShare = 0.7f;   // share of each wave that comes from the end-point side
        [Min(1)] public int GroupSize = 8;     // mobs per spawn cluster
        [Min(0f)] public float GroupSpread = 1.5f;

        /// <summary>Index of the row that applies at this progress: the one with the greatest FromProgress
        /// not above it. −1 when no row has started yet (no waves).</summary>
        public int SectionIndexAt(float progress)
        {
            int best = -1;
            for (int i = 0; i < Sections.Count; i++)
            {
                if (Sections[i] == null || Sections[i].FromProgress > progress) continue;
                if (best < 0 || Sections[i].FromProgress >= Sections[best].FromProgress) best = i;
            }
            return best;
        }

        // The row that starts next after row i, or −1.
        int NextSectionIndex(int i)
        {
            int next = -1;
            for (int k = 0; k < Sections.Count; k++)
            {
                if (k == i || Sections[k] == null || Sections[k].FromProgress <= Sections[i].FromProgress) continue;
                if (next < 0 || Sections[k].FromProgress < Sections[next].FromProgress) next = k;
            }
            return next;
        }

        // How far progress is from row i toward the row after it, 0..1. 0 without blending or a next row.
        float BlendTo(int i, float progress, out int next)
        {
            next = Blend ? NextSectionIndex(i) : -1;
            if (next < 0) return 0f;
            float span = Sections[next].FromProgress - Sections[i].FromProgress;
            return span <= 0f ? 0f : Mathf.Clamp01((progress - Sections[i].FromProgress) / span);
        }

        /// <summary>Seconds between waves at this progress. 0 when no row applies.</summary>
        public float IntervalAt(float progress)
        {
            int i = SectionIndexAt(progress);
            if (i < 0) return 0f;
            float t = BlendTo(i, progress, out int next);
            float interval = next < 0 ? Sections[i].Interval : Mathf.Lerp(Sections[i].Interval, Sections[next].Interval, t);
            return Mathf.Max(0.5f, interval);
        }

        /// <summary>What one wave is made of at this progress. Clears and fills <paramref name="into"/>.</summary>
        public void CompositionAt(float progress, List<WaveEntry> into)
        {
            into.Clear();
            int i = SectionIndexAt(progress);
            if (i < 0) return;
            float t = BlendTo(i, progress, out int next);
            foreach (var e in Sections[i].Mobs)
                if (e != null && e.Mob != null) Add(into, e.Mob, e.Count * (1f - t));
            if (next >= 0)
                foreach (var e in Sections[next].Mobs)
                    if (e != null && e.Mob != null) Add(into, e.Mob, e.Count * t);
            foreach (var e in into) e.Count = Mathf.RoundToInt(_scratch[e.Mob]);
            into.RemoveAll(e => e.Count <= 0);
            _scratch.Clear();
        }

        readonly Dictionary<MobDefinition, float> _scratch = new();

        void Add(List<WaveEntry> into, MobDefinition mob, float count)
        {
            if (_scratch.TryGetValue(mob, out float have)) { _scratch[mob] = have + count; return; }
            _scratch[mob] = count;
            into.Add(new WaveEntry { Mob = mob, Count = 0 });
        }

        /// <summary>Mobs in one wave at this progress.</summary>
        public int CountAt(float progress)
        {
            var list = new List<WaveEntry>();
            CompositionAt(progress, list);
            int n = 0;
            foreach (var e in list) n += e.Count;
            return n;
        }

        /// <summary>Mobs arriving per minute at this progress (bursts not counted).</summary>
        public float MobsPerMinuteAt(float progress)
        {
            float interval = IntervalAt(progress);
            return interval <= 0f ? 0f : CountAt(progress) / interval * 60f;
        }
    }
}
