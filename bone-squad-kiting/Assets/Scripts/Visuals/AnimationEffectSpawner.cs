using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BoneSquadKiting.Visuals
{
    [DisallowMultipleComponent]
    public sealed class AnimationEffectSpawner : MonoBehaviour
    {
        [Serializable]
        public class EffectEntry
        {
            public string Name;
            public GameObject Prefab;
            public Transform SpawnPoint;
            public Vector3 PositionOffset;
            public Vector3 RotationOffset;
            public bool FollowSpawnPoint;
            [Tooltip("Particles return when finished. Decals and looping effects use Lifetime.")]
            public bool ReturnWhenParticlesFinish = true;
            [Min(0.01f)] public float Lifetime = 2f;
        }

        public EffectEntry[] Effects = Array.Empty<EffectEntry>();

        sealed class Instance
        {
            public GameObject Object;
            public GameObject Prefab;
            public ParticleSystem[] Particles;
            public TrailRenderer[] Trails;
            public Transform Follow;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public float Expires;
            public int StartedFrame;
            public bool ReturnOnFinish;
        }

        readonly List<Instance> _pool = new();
        Transform _poolRoot;

        /// <summary>Animation Event: Function = SpawnEffect, Int = zero-based Effects index.</summary>
        public void SpawnEffect(int index)
        {
            Spawn(index, null, null);
        }

        public void SpawnEffectAt(int index, Vector3 position, Quaternion rotation)
        {
            Spawn(index, position, rotation);
        }

        void Spawn(int index, Vector3? worldPosition, Quaternion? worldRotation)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (index < 0 || index >= Effects.Length || Effects[index] == null || Effects[index].Prefab == null)
            {
                Debug.LogWarning($"Invalid effect slot {index} on {name}.", this);
                return;
            }
            var entry = Effects[index];
            if (_poolRoot == null)
            {
                var root = new GameObject(name + " Effect Pool");
                SceneManager.MoveGameObjectToScene(root, gameObject.scene);
                _poolRoot = root.transform;
            }
            Instance item = null;
            foreach (var candidate in _pool)
                if (candidate.Object != null && !candidate.Object.activeSelf && candidate.Prefab == entry.Prefab)
                { item = candidate; break; }
            if (item == null)
            {
                // Instantiate under an inactive parent so Play On Awake cannot run at the old position.
                var staging = new GameObject("Inactive staging");
                staging.transform.SetParent(_poolRoot, false);
                staging.SetActive(false);
                var go = Instantiate(entry.Prefab, staging.transform);
                go.SetActive(false);
                go.transform.SetParent(_poolRoot, false);
                Destroy(staging);
                item = new Instance
                {
                    Object = go, Prefab = entry.Prefab,
                    Particles = go.GetComponentsInChildren<ParticleSystem>(true),
                    Trails = go.GetComponentsInChildren<TrailRenderer>(true)
                };
                _pool.Add(item);
            }
            var point = entry.SpawnPoint != null ? entry.SpawnPoint : transform;
            item.LocalPosition = entry.PositionOffset;
            item.LocalRotation = Quaternion.Euler(entry.RotationOffset);
            item.Object.transform.SetPositionAndRotation(worldPosition ?? point.TransformPoint(item.LocalPosition), worldRotation ?? point.rotation * item.LocalRotation);
            item.Follow = !worldPosition.HasValue && entry.FollowSpawnPoint ? point : null;
            item.Expires = Time.time + Mathf.Max(0.01f, entry.Lifetime);
            item.StartedFrame = Time.frameCount;
            item.ReturnOnFinish = entry.ReturnWhenParticlesFinish && item.Particles.Length > 0;
            foreach (var ps in item.Particles)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.None;
                main.playOnAwake = false;
            }
            foreach (var trail in item.Trails) trail.Clear();
            item.Object.SetActive(true);
            foreach (var ps in item.Particles)
                if (ps.gameObject.activeInHierarchy) ps.Play(false);
        }

        void LateUpdate()
        {
            foreach (var item in _pool)
            {
                if (item.Object == null || !item.Object.activeSelf) continue;
                if (item.Follow != null)
                    item.Object.transform.SetPositionAndRotation(item.Follow.TransformPoint(item.LocalPosition), item.Follow.rotation * item.LocalRotation);
                bool alive = false;
                if (item.ReturnOnFinish)
                    foreach (var ps in item.Particles)
                        if (ps != null && ps.gameObject.activeInHierarchy) alive |= ps.IsAlive(true);
                if (Time.time >= item.Expires || (item.ReturnOnFinish && !alive && Time.frameCount > item.StartedFrame))
                    Return(item);
            }
        }

        static void Return(Instance item)
        {
            foreach (var ps in item.Particles)
                if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            item.Object.SetActive(false);
            item.Follow = null;
        }

        void OnDisable()
        {
            foreach (var item in _pool)
                if (item.Object != null) Return(item);
        }

        void OnDestroy()
        {
            if (_poolRoot != null) Destroy(_poolRoot.gameObject);
        }
    }
}
