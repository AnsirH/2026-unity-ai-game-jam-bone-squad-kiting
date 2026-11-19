using UnityEngine;
using UnityEngine.Audio;

namespace BoneSquadKiting.Audio
{
    [DisallowMultipleComponent]
    public sealed class SfxPool : MonoBehaviour
    {
        [SerializeField] AudioMixerGroup output;
        [SerializeField, Min(1)] int maxVoices = 16;
        AudioSource[] voices;
        int nextVoice;

        internal void Stop(AudioClip clip)
        {
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

            var source = voices[slot];
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.outputAudioMixerGroup = output;
                voices[slot] = source;
            }
            source.Stop();
            source.clip = entry.Clip;
            source.pitch = Mathf.Clamp(entry.Pitch, 0.01f, 3f);
            source.volume = Mathf.Clamp01(entry.Volume);
            source.timeSamples = Mathf.Clamp(Mathf.RoundToInt(entry.StartOffset * entry.Clip.frequency), 0, entry.Clip.samples - 1);
            source.Play();
            nextVoice = (slot + 1) % voices.Length;
        }
    }
}
