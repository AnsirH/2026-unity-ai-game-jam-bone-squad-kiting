using UnityEngine;
using UnityEngine.Audio;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Audio
{
    [DisallowMultipleComponent]
    public sealed class SfxPool : MonoBehaviour
    {
        [SerializeField] AudioMixerGroup output;
        [SerializeField, Min(1)] int maxVoices = 16;
        AudioSource[] voices;
        int nextVoice;

        const double VolleySeconds = 0.12;
        readonly System.Random shotRandom = new();
        AudioSource crossbow, spear;
        double volleyStart;
        int boltsInVolley;

        internal void PlaySquadShot(SfxEntry entry, bool leader)
        {
            if (leader)
            {
                if (spear == null) spear = CreateVoice();
                StartVoice(spear, entry, ShotPitch(entry), entry.Volume);
                return;
            }

            double now = Time.unscaledTimeAsDouble;
            bool newVolley = crossbow == null || !crossbow.isPlaying || now - volleyStart >= VolleySeconds;
            if (newVolley)
            {
                if (crossbow == null) crossbow = CreateVoice();
                volleyStart = now;
                boltsInVolley = 0;
            }

            // Only released bolts count. One voice grows with the volley without stacking identical transients.
            boltsInVolley = Mathf.Min(boltsInVolley + 1, GameConstants.SquadSize);
            float gain = 0.30f + 0.60f * Mathf.Log(boltsInVolley, GameConstants.SquadSize);
            float volume = Mathf.Clamp01(entry.Volume * gain);
            if (newVolley) StartVoice(crossbow, entry, ShotPitch(entry), volume);
            else crossbow.volume = volume;
        }

        float ShotPitch(SfxEntry entry) =>
            Mathf.Clamp(entry.Pitch * (0.9f + 0.2f * (float)shotRandom.NextDouble()), 0.9f, 1.1f);

        AudioSource CreateVoice()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = output;
            return source;
        }

        internal void Stop(AudioClip clip)
        {
            if (crossbow != null && crossbow.clip == clip) crossbow.Stop();
            if (spear != null && spear.clip == clip) spear.Stop();
            if (voices == null) return;
            foreach (var voice in voices)
                if (voice != null && voice.clip == clip && voice.isPlaying) voice.Stop();
        }

        internal void Play(SfxEntry entry)
        {
            voices ??= new AudioSource[Mathf.Max(1, maxVoices)];
            int slot = nextVoice;
            for (int i = 0; i < voices.Length; i++)
            {
                int candidate = (nextVoice + i) % voices.Length;
                if (voices[candidate] == null || !voices[candidate].isPlaying)
                {
                    slot = candidate;
                    break;
                }
            }

            if (voices[slot] == null) voices[slot] = CreateVoice();
            StartVoice(voices[slot], entry, Mathf.Clamp(entry.Pitch, 0.01f, 3f), Mathf.Clamp01(entry.Volume));
            nextVoice = (slot + 1) % voices.Length;
        }

        static void StartVoice(AudioSource source, SfxEntry entry, float pitch, float volume)
        {
            source.Stop();
            source.clip = entry.Clip;
            source.pitch = pitch;
            source.volume = volume;
            source.timeSamples = Mathf.Clamp(Mathf.RoundToInt(entry.StartOffset * entry.Clip.frequency), 0, entry.Clip.samples - 1);
            source.Play();
        }
    }
}
