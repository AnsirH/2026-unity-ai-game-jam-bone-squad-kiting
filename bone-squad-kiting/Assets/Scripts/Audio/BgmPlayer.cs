using System.Collections;
using UnityEngine;

namespace BoneSquadKiting.Audio
{
    [DisallowMultipleComponent]
    public sealed class BgmPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource sourceA;
        [SerializeField] private AudioSource sourceB;

        private readonly float[] baseVolumes = new float[2];
        private readonly float[] fades = new float[2];
        private Coroutine fadeRoutine;
        private int activeSource = -1;
        private string currentId;

        public bool IsConfigured => sourceA != null && sourceB != null && sourceA != sourceB;
        public bool IsPlaying => IsConfigured && activeSource >= 0 && Source(activeSource).isPlaying;
        public string CurrentId => IsPlaying ? currentId : null;

        internal bool Play(BgmEntry entry, float duration, bool restart)
        {
            if (!IsConfigured || entry == null || entry.Clip == null)
                return false;

            string key = entry.Id;
            if (!restart && currentId == key && IsPlaying)
                return true;

            int target = activeSource < 0 ? 0 : 1 - activeSource;
            AudioSource incoming = Source(target);
            incoming.Stop();
            incoming.clip = entry.Clip;
            incoming.playOnAwake = false;
            incoming.pitch = Mathf.Clamp(entry.Pitch, 0.01f, 3f);
            incoming.loop = entry.Loop;
            incoming.spatialBlend = 0f;
            incoming.spatialize = false;
            incoming.spatializePostEffects = false;
            baseVolumes[target] = Mathf.Clamp01(entry.Volume);
            fades[target] = 0f;
            incoming.volume = 0f;
            incoming.Play();
            activeSource = target;
            currentId = key;
            StartFade(target, duration);
            return true;
        }

        internal void Stop(float duration)
        {
            if (!IsConfigured || activeSource < 0)
                return;
            currentId = null;
            StartFade(-1, duration);
        }

        internal void StopImmediately()
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = null;
            if (sourceA != null) sourceA.Stop();
            if (sourceB != null) sourceB.Stop();
            activeSource = -1;
            currentId = null;
        }

        private void OnDisable() => StopImmediately();
        private AudioSource Source(int index) => index == 0 ? sourceA : sourceB;

        private void StartFade(int target, float duration)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(Fade(target, Mathf.Max(0f, duration)));
        }

        private IEnumerator Fade(int target, float duration)
        {
            float startA = fades[0];
            float startB = fades[1];
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fades[0] = Mathf.Lerp(startA, target == 0 ? 1f : 0f, t);
                fades[1] = Mathf.Lerp(startB, target == 1 ? 1f : 0f, t);
                ApplyVolumes();
                yield return null;
            }
            for (int i = 0; i < 2; i++)
            {
                fades[i] = i == target ? 1f : 0f;
                if (i != target) Source(i).Stop();
            }
            if (target < 0)
            {
                activeSource = -1;
                currentId = null;
            }
            ApplyVolumes();
            fadeRoutine = null;
        }

        private void ApplyVolumes()
        {
            sourceA.volume = baseVolumes[0] * fades[0];
            sourceB.volume = baseVolumes[1] * fades[1];
        }
    }
}
