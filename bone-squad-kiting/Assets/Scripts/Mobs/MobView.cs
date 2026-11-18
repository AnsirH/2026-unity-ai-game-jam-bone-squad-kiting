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
        public Material MobMaterial;
        public Color HitFlashColor = new(3f, 3f, 3f, 1f);
        public float TurnSpeed = 540f;     // degrees per second a model turns toward where it walks

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
                if (moving)
                {
                    var want = Quaternion.Euler(0f, Mathf.Atan2(moved.x, moved.y) * Mathf.Rad2Deg + m.Def.ModelYawOffset, 0f);
                    v.Root.rotation = Quaternion.RotateTowards(v.Root.rotation, want, TurnSpeed * Time.deltaTime);
                }
                if (m.AttackCd > v.LastCd + 1e-4f) v.Visual.PlayAttack();     // the cooldown jumped back up: it just hit
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
