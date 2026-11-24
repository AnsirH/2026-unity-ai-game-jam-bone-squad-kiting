using System.Collections.Generic;
using BoneSquadKiting.Cameras;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BoneSquadKiting.Fx
{
    [DisallowMultipleComponent]
    public sealed class PresentationManager : MonoBehaviour
    {
        static readonly Dictionary<Scene, PresentationManager> Managers = new();

        public PresentationDatabase Database;
        public EdgeScrollCamera Cam;

        Volume _moodVolume, _effectVolume;
        VolumeProfile _moodFrom, _moodTo;
        float _moodStartWeight, _moodDuration, _moodElapsed;
        float _effectHold, _effectReturn, _effectElapsed;

        public static PresentationManager Current =>
            Managers.TryGetValue(SceneManager.GetActiveScene(), out var manager) ? manager : null;

        void OnEnable()
        {
            if (Managers.TryGetValue(gameObject.scene, out var existing) && existing != null && existing != this)
            {
                Debug.LogWarning("This scene already has a PresentationManager.", this);
                enabled = false;
                return;
            }
            Managers[gameObject.scene] = this;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (!Managers.TryGetValue(gameObject.scene, out var owner) || owner != this) return;
            Managers.Remove(gameObject.scene);
            if (Cam != null) Cam.ClearPresentationShake();
            if (_moodVolume == null) return;
            _moodFrom = _moodTo = null;
            _moodDuration = _moodElapsed = 0f;
            _moodVolume.weight = _effectVolume.weight = 0f;
            _moodVolume.sharedProfile = _effectVolume.sharedProfile = null;
        }

        void OnActiveSceneChanged(Scene previous, Scene active)
        {
            if (_moodVolume == null) return;
            // URP global volumes otherwise affect cameras in other additively loaded stages.
            _moodVolume.enabled = _effectVolume.enabled = gameObject.scene == active;
        }

        public bool Play(string id)
        {
            var effect = Database != null ? Database.FindEffect(id) : null;
            if (!isActiveAndEnabled || effect == null) return false;
            if (Cam != null && effect.ShakeStrength > 0f && effect.ShakeSeconds > 0f)
                Cam.PlayPresentationShake(effect.ShakeStrength, effect.ShakeSeconds);
            if (effect.Profile != null)
            {
                EnsureVolumes();
                _effectVolume.sharedProfile = effect.Profile;
                _effectVolume.weight = 1f;
                _effectHold = Mathf.Max(0f, effect.HoldSeconds);
                _effectReturn = Mathf.Max(0f, effect.ReturnSeconds);
                _effectElapsed = 0f;
            }
            return true;
        }

        public bool SetMood(string id)
        {
            var mood = Database != null ? Database.FindMood(id) : null;
            if (!isActiveAndEnabled || mood == null || mood.Profile == null) return false;
            EnsureVolumes();
            BeginMoodTransition(mood.Profile, mood.TransitionSeconds);
            return true;
        }

        public void ClearMood()
        {
            if (_moodTo != null) BeginMoodTransition(null, _moodDuration);
        }

        void BeginMoodTransition(VolumeProfile target, float seconds)
        {
            _moodFrom = _moodVolume.sharedProfile;
            _moodStartWeight = _moodVolume.weight;
            _moodTo = target;
            _moodDuration = Mathf.Max(0f, seconds);
            _moodElapsed = 0f;
            UpdateMood(0f);
        }

        void UpdateMood(float dt)
        {
            _moodElapsed += dt;
            if (_moodElapsed >= _moodDuration)
            {
                _moodVolume.sharedProfile = _moodTo;
                _moodVolume.weight = _moodTo == null ? 0f : 1f;
                _moodFrom = null;
                return;
            }

            float t = _moodElapsed / _moodDuration;
            bool replacingProfile = _moodFrom != null && _moodFrom != _moodTo && _moodStartWeight > 0f;
            if (replacingProfile)
            {
                // Two different profiles share a volume. Swap only at zero weight so interrupted
                // transitions also start at the strength currently visible on screen.
                if (_moodTo != null) t *= 2f;
                if (t < 1f)
                {
                    _moodVolume.weight = _moodStartWeight * (1f - t);
                    return;
                }
                t -= 1f;
            }
            _moodVolume.sharedProfile = _moodTo;
            float startWeight = _moodFrom == _moodTo ? _moodStartWeight : 0f;
            _moodVolume.weight = _moodTo == null ? 0f : Mathf.Lerp(startWeight, 1f, t);
        }

        void Update() { if (Time.deltaTime > 0f) Tick(Time.deltaTime); }

        public void Tick(float dt)
        {
            if (_moodVolume == null) return;
            if (_moodElapsed < _moodDuration) UpdateMood(dt);
            if (_effectVolume.sharedProfile == null) return;
            _effectElapsed += dt;
            if (_effectElapsed < _effectHold) return;
            float fadeTime = _effectElapsed - _effectHold;
            _effectVolume.weight = _effectReturn > 0f ? Mathf.Clamp01(1f - fadeTime / _effectReturn) : 0f;
            if (_effectVolume.weight == 0f) _effectVolume.sharedProfile = null;
        }

        void EnsureVolumes()
        {
            if (_moodVolume != null)
            {
                OnActiveSceneChanged(default, SceneManager.GetActiveScene());
                return;
            }
            int mask = 1;
            if (Cam != null && Cam.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                mask = data.volumeLayerMask;
            int layer = 0;
            while (layer < 31 && (mask & (1 << layer)) == 0) layer++;

            float basePriority = 0f;
            foreach (var volume in VolumeManager.instance.GetVolumes(mask))
                if (volume.gameObject.scene == gameObject.scene)
                    basePriority = Mathf.Max(basePriority, volume.priority);
            _moodVolume = CreateVolume("Presentation Mood", layer, basePriority + 1f);
            _effectVolume = CreateVolume("Presentation Effect", layer, basePriority + 2f);
        }

        Volume CreateVolume(string volumeName, int layer, float priority)
        {
            var child = new GameObject(volumeName) { layer = layer };
            child.SetActive(false);
            child.transform.SetParent(transform, false);
            var volume = child.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = 0f;
            volume.enabled = gameObject.scene == SceneManager.GetActiveScene();
            child.SetActive(true);
            return volume;
        }
    }
}
