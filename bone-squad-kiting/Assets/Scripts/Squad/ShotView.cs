using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;

namespace BoneSquadKiting.Squad
{
    /// <summary>
    /// Draws the projectiles in flight, pooled. With an ArtSet that has projectile prefabs each shot is that
    /// model, turned so its tip (+Z after the art set's rotation) points the way it flies; the leader's
    /// shots use the leader's projectile. Without one, a shot is a small glowing sphere.
    /// </summary>
    public class ShotView : MonoBehaviour
    {
        public GameDirector Director;
        public ArtSet Art;                 // optional; taken from the scene's SquadView when left empty
        public Material ShotMaterial;      // for the placeholder spheres
        public float Scale = 0.16f;
        public float Height = 0.45f;       // above the ground
        public float ArcHeight = 0.4f;     // maximum extra height at the middle of the flight
        public float FullArcDistance = 6f; // distance at which ArcHeight is reached

        class Pool
        {
            public readonly List<Transform> Items = new();
            public int Used;
        }
        readonly Pool _spheres = new(), _shots = new(), _leaderShots = new();
        bool _artLooked;
        readonly Dictionary<Transform, long> _visualIds = new();
        readonly List<GameObject> _launchEffects = new();
        ShotSystem _launchSource;

        void Update()
        {
            var source = Director != null ? Director.Shots : null;
            if (source != _launchSource)
            {
                if (_launchSource != null)
                {
                    _launchSource.OnImpact -= PlayImpact;
                }
                _launchSource = source;
                if (_launchSource != null)
                {
                    _launchSource.OnImpact += PlayImpact;
                }
            }
            foreach (var effect in _launchEffects)
            {
                if (effect == null || !effect.activeSelf) continue;
                bool alive = false;
                foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>())
                    alive |= ps.IsAlive(true);
                if (!alive) effect.SetActive(false);
            }
        }

        void OnDisable()
        {
            if (_launchSource != null)
            {
                _launchSource.OnImpact -= PlayImpact;
            }
            _launchSource = null;
            foreach (var effect in _launchEffects)
                if (effect != null) effect.SetActive(false);
        }

        void PlayImpact(Vector2 position, float height, Vector2 direction)
        {
            PlayEffect(new Vector3(position.x, GroundHeight.Y(position.x, position.y) + height, position.y), direction);
        }

        void PlayEffect(Vector3 position, Vector2 direction)
        {
            if (Art == null)
            {
                var squad = FindAnyObjectByType<SquadView>();
                if (squad != null) Art = squad.Art;
            }
            if (Art == null || Art.LaunchFxPrefab == null) return;
            GameObject effect = null;
            foreach (var item in _launchEffects)
                if (item != null && !item.activeSelf) { effect = item; break; }
            if (effect == null)
            {
                effect = Instantiate(Art.LaunchFxPrefab, transform);
                _launchEffects.Add(effect);
            }
            effect.SetActive(false);
            effect.transform.SetPositionAndRotation(
                position,
                direction.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y)) : Quaternion.identity);
            var systems = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
            }
            effect.SetActive(true);
            foreach (var ps in systems) ps.Play(false);
        }

        void LateUpdate()
        {
            if (Art == null && !_artLooked)
            {
                _artLooked = true;
                var squadView = FindAnyObjectByType<SquadView>();
                if (squadView != null) Art = squadView.Art;
            }
            _spheres.Used = _shots.Used = _leaderShots.Used = 0;

            var shots = Director != null && Director.Shots != null ? Director.Shots.Shots : null;
            int n = shots?.Count ?? 0;
            for (int i = 0; i < n; i++)
            {
                var s = shots[i];
                var leaderPrefab = Art != null ? Art.LeaderShotPrefab : null;
                var shotPrefab = Art != null ? Art.ShotPrefab : null;
                Transform t;
                if (s.Heavy && leaderPrefab != null) t = Take(_leaderShots, leaderPrefab, Art.LeaderShotScale, Art.LeaderShotRotation);
                else if (shotPrefab != null) t = Take(_shots, shotPrefab, Art.ShotScale, Art.ShotRotation);
                else t = Take(_spheres, null, 1f, Vector3.zero);
                float progress = Mathf.Clamp01(s.Travelled / Mathf.Max(0.01f, s.InitialDistance));
                float startY = s.HasWorldOrigin ? s.StartY : Height + GroundHeight.Y(s.X, s.Z);
                float targetY = s.Target != null
                    ? GroundHeight.Y(s.Target.Pos.x, s.Target.Pos.y) + s.Target.Radius
                    : startY;
                float distanceRatio = Mathf.Clamp01(s.InitialDistance / Mathf.Max(0.01f, FullArcDistance));
                float scaledArcHeight = ArcHeight * distanceRatio;
                float y = Mathf.Lerp(startY, targetY, progress) + 4f * scaledArcHeight * progress * (1f - progress);
                t.position = new Vector3(s.X, y, s.Z);
                if (s.Dir.sqrMagnitude > 1e-6f)
                {
                    float verticalSlope = targetY - startY + 4f * scaledArcHeight * (1f - 2f * progress);
                    var tangent = new Vector3(s.Dir.x * s.InitialDistance, verticalSlope, s.Dir.y * s.InitialDistance);
                    if (tangent.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(tangent);
                }
                bool newFlight = !_visualIds.TryGetValue(t, out var previousId) || previousId != s.VisualId || !t.gameObject.activeSelf;
                if (newFlight)
                {
                    foreach (var trail in t.GetComponentsInChildren<TrailRenderer>(true))
                    {
                        trail.emitting = false;
                        trail.Clear();
                    }
                    t.gameObject.SetActive(true);
                    foreach (var trail in t.GetComponentsInChildren<TrailRenderer>())
                        trail.emitting = true;
                    // Art scripts compile separately from the gameplay assembly.
                    t.gameObject.BroadcastMessage("RestartTrail", SendMessageOptions.DontRequireReceiver);
                    _visualIds[t] = s.VisualId;
                }
            }
            Hide(_spheres); Hide(_shots); Hide(_leaderShots);
        }

        static void Hide(Pool pool)
        {
            for (int i = pool.Used; i < pool.Items.Count; i++)
                if (pool.Items[i].gameObject.activeSelf) pool.Items[i].gameObject.SetActive(false);
        }

        // The next free object of a pool, switched on. Its model is kept at the art set's current scale and
        // rotation, so both can be tuned while playing.
        Transform Take(Pool pool, GameObject prefab, float scale, Vector3 rotation)
        {
            if (pool.Used == pool.Items.Count) pool.Items.Add(prefab != null ? CreateModel(prefab) : CreateSphere());
            var t = pool.Items[pool.Used++];
            if (prefab != null)
            {
                var model = t.GetChild(0);
                model.localRotation = Quaternion.Euler(rotation) * prefab.transform.localRotation;
                model.localScale = prefab.transform.localScale * scale;
            }
            return t;
        }

        // A holder that points along the flight, with the model inside it turned to match.
        Transform CreateModel(GameObject prefab)
        {
            var holder = new GameObject(prefab.name + " (shot)");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            var model = Instantiate(prefab, holder.transform);
            model.transform.localPosition = Vector3.zero;
            foreach (var col in model.GetComponentsInChildren<Collider>()) Destroy(col);   // shots are not physical
            return holder.transform;
        }

        Transform CreateSphere()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.SetActive(false);
            go.name = "Shot";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * Scale;
            if (ShotMaterial != null) go.GetComponent<Renderer>().sharedMaterial = ShotMaterial;
            return go.transform;
        }
    }
}
