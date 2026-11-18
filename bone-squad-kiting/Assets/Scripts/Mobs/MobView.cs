using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;
using BoneSquadKiting.Visuals;

namespace BoneSquadKiting.Mobs
{
    /// <summary>
    /// Draws the swarm. A mob whose definition has a prefab is that model (this view only moves and turns
    /// its root and tells it what the mob is doing); the others are placeholder spheres. Views are pooled
    /// per kind. No colliders, no rigidbodies.
    /// </summary>
    public class MobView : MonoBehaviour
    {
        public GameDirector Director;
        public ArtSet Art;
        public Material MobMaterial;
        public Color HitFlashColor = new(3f, 3f, 3f, 1f);
        public float TurnSpeed = 540f;     // degrees per second a model turns toward where it walks
        public Color ShotColor = new(0.75f, 0.3f, 1f, 1f);
        public float ShotHeight = 1.1f;    // how high above the ground enemy shots are drawn

        class View
        {
            public Transform Root;
            public Renderer Sphere;        // placeholder only
            public UnitVisual Visual;      // model only
            public Vector2 LastPos;
            public float LastCd;
            public bool Seen;
        }

        readonly Dictionary<Mob, View> _live = new();
        readonly Dictionary<MobDefinition, Stack<View>> _free = new();
        readonly List<Mob> _gone = new();
        readonly List<Transform> _shots = new();
        Material _shotMaterial;
        readonly Dictionary<MobShot, Transform> _customShots = new();
        readonly Dictionary<GameObject, Stack<Transform>> _shotPools = new();
        readonly Dictionary<Transform, GameObject> _shotPrefabs = new();
        readonly List<MobShot> _finishedShots = new();
        MobSystem _hitSource;
        AnimationEffectSpawner _hitEffects;

        void Update()
        {
            var source = Director != null ? Director.Mobs : null;
            if (source == _hitSource) return;
            if (_hitSource != null) _hitSource.OnProjectileHit -= PlayProjectileHit;
            _hitSource = source;
            if (_hitSource != null) _hitSource.OnProjectileHit += PlayProjectileHit;
        }

        void OnDisable()
        {
            if (_hitSource != null) _hitSource.OnProjectileHit -= PlayProjectileHit;
            _hitSource = null;
        }

        void PlayProjectileHit(MobShot shot, BoneSquadKiting.Squad.SquadUnit unit)
        {
            if (Art == null || Art.EnemyProjectiles == null || unit == null) return;
            foreach (var entry in Art.EnemyProjectiles)
            {
                if (entry == null || entry.Enemy != shot.Def || entry.HitEffectPrefab == null) continue;
                if (_hitEffects == null)
                {
                    var go = new GameObject("Enemy Projectile Hit Effects");
                    go.transform.SetParent(transform, false);
                    _hitEffects = go.AddComponent<AnimationEffectSpawner>();
                }
                _hitEffects.Effects = new[] { new AnimationEffectSpawner.EffectEntry
                    { Name = "Projectile Hit", Prefab = entry.HitEffectPrefab, Lifetime = 4f } };
                var position = new Vector3(unit.X, GroundHeight.Y(unit.X, unit.Z) + 0.8f, unit.Z);
                var rotation = shot.Dir.sqrMagnitude > 1e-6f
                    ? Quaternion.LookRotation(new Vector3(shot.Dir.x, 0f, shot.Dir.y)) : Quaternion.identity;
                _hitEffects.SpawnEffectAt(0, position, rotation);
                break;
            }
        }

        /// <summary>How many enemy shots are being drawn right now.</summary>
        public int ShotsDrawn { get; private set; }
        MaterialPropertyBlock _block;
        Color _baseEmission;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>How many mobs are being drawn right now.</summary>
        public int DrawnCount => _live.Count;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (MobMaterial != null && MobMaterial.HasProperty(EmissionId)) _baseEmission = MobMaterial.GetColor(EmissionId);
        }

        void LateUpdate()
        {
            foreach (var v in _live.Values) v.Seen = false;

            var mobs = Director != null && Director.Mobs != null ? Director.Mobs.Mobs : null;
            if (mobs != null)
                foreach (var m in mobs)
                {
                    if (!m.Alive || m.Def == null) continue;
                    if (!_live.TryGetValue(m, out var v)) { v = Take(m); _live[m] = v; }
                    v.Seen = true;
                    Draw(m, v);
                }

            DrawShots();

            _gone.Clear();
            foreach (var pair in _live) if (!pair.Value.Seen) _gone.Add(pair.Key);
            foreach (var m in _gone)
            {
                var v = _live[m];
                _live.Remove(m);
                v.Root.gameObject.SetActive(false);
                if (!_free.TryGetValue(m.Def, out var stack)) _free[m.Def] = stack = new Stack<View>();
                stack.Push(v);
            }
        }

        void DrawShots()
        {
            if (Art == null)
            {
                var squad = FindAnyObjectByType<BoneSquadKiting.Squad.SquadView>();
                if (squad != null) Art = squad.Art;
            }
            var shots = Director != null && Director.Mobs != null ? Director.Mobs.Shots : null;
            _finishedShots.Clear();
            foreach (var pair in _customShots)
                if (shots == null || !shots.Contains(pair.Key)) _finishedShots.Add(pair.Key);
            foreach (var shot in _finishedShots)
            {
                var t = _customShots[shot];
                t.gameObject.SetActive(false);
                var prefab = _shotPrefabs[t];
                if (!_shotPools.TryGetValue(prefab, out var pool)) _shotPools[prefab] = pool = new Stack<Transform>();
                pool.Push(t);
                _customShots.Remove(shot);
            }
            int n = shots?.Count ?? 0;
            while (_shots.Count < n)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Mob Shot";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                if (_shotMaterial == null)
                {
                    _shotMaterial = new Material(go.GetComponent<Renderer>().sharedMaterial) { color = ShotColor };
                    if (_shotMaterial.HasProperty("_BaseColor")) _shotMaterial.SetColor("_BaseColor", ShotColor);
                    if (_shotMaterial.HasProperty(EmissionId)) { _shotMaterial.EnableKeyword("_EMISSION"); _shotMaterial.SetColor(EmissionId, ShotColor * 2f); }
                }
                go.GetComponent<Renderer>().sharedMaterial = _shotMaterial;
                _shots.Add(go.transform);
            }
            for (int i = 0; i < _shots.Count; i++)
            {
                bool on = i < n;
                if (_shots[i].gameObject.activeSelf != on) _shots[i].gameObject.SetActive(on);
                if (!on) continue;
                var s = shots[i];
                ArtSet.EnemyProjectile look = null;
                if (Art != null && Art.EnemyProjectiles != null)
                    foreach (var entry in Art.EnemyProjectiles)
                        if (entry != null && entry.Enemy == s.Def && entry.Prefab != null) { look = entry; break; }
                if (look != null)
                {
                    _shots[i].gameObject.SetActive(false);
                    bool fresh = !_customShots.TryGetValue(s, out var t);
                    if (fresh)
                    {
                        if (_shotPools.TryGetValue(look.Prefab, out var pool) && pool.Count > 0) t = pool.Pop();
                        else
                        {
                            var holder = new GameObject("Enemy Projectile");
                            holder.SetActive(false);
                            holder.transform.SetParent(transform, false);
                            var model = Instantiate(look.Prefab, holder.transform);
                            model.transform.localPosition = Vector3.zero;
                            foreach (var col in model.GetComponentsInChildren<Collider>()) Destroy(col);
                            t = holder.transform;
                            holder.AddComponent<BoneSquadKiting.Visuals.ProjectileFade>();
                            _shotPrefabs[t] = look.Prefab;
                        }
                        _customShots[s] = t;
                    }
                    t.position = new Vector3(s.X, GroundHeight.Y(s.X, s.Z) + ShotHeight, s.Z);
                    if (s.Dir.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(new Vector3(s.Dir.x, 0f, s.Dir.y));
                    var mesh = t.GetChild(0);
                    mesh.localScale = look.Prefab.transform.localScale * look.Scale;
                    mesh.localRotation = Quaternion.Euler(look.Rotation) * look.Prefab.transform.localRotation;
                    if (fresh)
                    {
                        foreach (var trail in t.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
                        t.gameObject.SetActive(true);
                        t.gameObject.BroadcastMessage("RestartTrail", SendMessageOptions.DontRequireReceiver);
                    }
                    float opacity = s.Def.ShotLifetime > 0f
                        ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.Def.ShotFadeStart, 1f, s.Age / s.Def.ShotLifetime)) : 1f;
                    t.GetComponent<BoneSquadKiting.Visuals.ProjectileFade>().SetFade(opacity);
                    continue;
                }
                float d = s.Def.ShotRadius * 2f;
                _shots[i].position = new Vector3(s.X, GroundHeight.Y(s.X, s.Z) + ShotHeight, s.Z);
                _shots[i].localScale = new Vector3(d, d, d);
            }
            ShotsDrawn = n;
        }

        void OnDestroy() { if (_shotMaterial != null) Destroy(_shotMaterial); }

        void Draw(Mob m, View v)
        {
            float y = GroundHeight.Y(m.X, m.Z);
            var moved = m.Pos - v.LastPos;
            bool moving = moved.sqrMagnitude > 1e-8f;

            if (v.Visual != null)
            {
                v.Root.position = new Vector3(m.X, y, m.Z);
                var size = m.Def.Prefab.transform.localScale * m.Def.ModelScale;
                if (v.Root.localScale != size) v.Root.localScale = size;      // follows the definition, so it can be tuned while playing
                var face = moved;
                if (!moving && m.Def.IsRanged && Director != null && Director.Squad != null) face = Director.Squad.Anchor - m.Pos;   // a shooter standing still looks at the squad
                if (face.sqrMagnitude > 1e-8f)
                {
                    var want = Quaternion.Euler(0f, Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg + m.Def.ModelYawOffset, 0f);
                    v.Root.rotation = Quaternion.RotateTowards(v.Root.rotation, want, TurnSpeed * Time.deltaTime);
                }
                if (m.AttackCd > v.LastCd + 1e-4f) v.Visual.PlayAttack(m.Def.AttackAnimSeconds);     // the cooldown jumped back up: it just hit
                else v.Visual.SetState(moving ? VisualState.Run : VisualState.Idle);
            }
            else
            {
                float d = m.Radius * 2f;
                v.Root.position = new Vector3(m.X, y + m.Radius, m.Z);
                v.Root.localScale = new Vector3(d, d, d);
                if (m.Flash > 0f)
                {
                    _block.SetColor(EmissionId, Color.Lerp(_baseEmission, HitFlashColor, m.Flash));
                    v.Sphere.SetPropertyBlock(_block);
                }
                else if (v.Sphere.HasPropertyBlock()) v.Sphere.SetPropertyBlock(null);
            }
            v.LastPos = m.Pos; v.LastCd = m.AttackCd;
        }

        // A view for this mob: a pooled one of its kind, or a new one.
        View Take(Mob m)
        {
            View v;
            if (_free.TryGetValue(m.Def, out var stack) && stack.Count > 0)
            {
                v = stack.Pop();
                v.Root.gameObject.SetActive(true);
            }
            else v = Create(m.Def);
            v.LastPos = m.Pos; v.LastCd = m.AttackCd;
            if (v.Visual != null)
            {
                // Face the squad from the start instead of whichever way the last owner was looking.
                var squad = Director != null ? Director.Squad : null;
                var to = squad != null ? squad.Anchor - m.Pos : Vector2.up;
                if (to.sqrMagnitude > 1e-6f)
                    v.Root.rotation = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg + m.Def.ModelYawOffset, 0f);
                v.Visual.SetState(VisualState.Run);
            }
            return v;
        }

        View Create(MobDefinition def)
        {
            var v = new View();
            if (def.Prefab != null)
            {
                var model = Instantiate(def.Prefab, transform);
                model.name = def.name;
                foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);   // the game does not use colliders for mobs
                v.Root = model.transform;
                v.Visual = model.GetComponent<UnitVisual>();
                if (v.Visual == null)
                {
                    v.Visual = model.AddComponent<UnitVisual>();
                    v.Visual.States = def.States;
                }
                return v;
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Mob";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            v.Root = go.transform;
            v.Sphere = go.GetComponent<Renderer>();
            if (MobMaterial != null) v.Sphere.sharedMaterial = MobMaterial;
            return v;
        }
    }
}
