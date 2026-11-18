using System.Collections.Generic;
using UnityEngine;

namespace BoneSquadKiting.Audio
{
    [DisallowMultipleComponent]
    public sealed class SoundManager : MonoBehaviour
    {
        [SerializeField] SoundDatabaseSO database;
        [SerializeField] BgmPlayer music;
        [SerializeField] SfxPool effects;
        [SerializeField, Min(0f)] float crossfadeSeconds = 0.5f;

        public static SoundManager Current { get; private set; }
        public string CurrentBgmId => music.CurrentId;
        readonly Dictionary<string, float> _lastVolleySound = new();

        // Several soldiers often fire or land the same sound on the same beat.
        public void PlayVolleySfx(string id)
        {
            float now = Time.unscaledTime;
            if (_lastVolleySound.TryGetValue(id, out float last) && now - last < 0.06f) return;
            if (PlaySfx(id)) _lastVolleySound[id] = now;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Current != null) return;
            var prefab = Resources.Load<SoundManager>("Audio/SoundManager");
            if (prefab != null) Instantiate(prefab);
            else Debug.LogError("Missing Resources/Audio/SoundManager prefab.");
        }

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }
            Current = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public bool PlayBgm(string id, bool restart = false) =>
            music.Play(database.FindBgm(id), crossfadeSeconds, restart);

        public void StopBgm() => music.Stop(crossfadeSeconds);

        public bool PlaySfx(string id)
        {
            var entry = database.FindSfx(id);
            if (entry?.Clip == null) return false;
            effects.Play(entry);
            return true;
        }
    }
}
