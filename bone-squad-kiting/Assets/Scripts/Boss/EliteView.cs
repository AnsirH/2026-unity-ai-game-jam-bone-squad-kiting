using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;
using BoneSquadKiting.Visuals;

namespace BoneSquadKiting.Bosses
{
    /// <summary>
    /// Draws mini bosses. One whose definition has a prefab is that model (this view moves its root, turns
    /// it toward the squad and plays its cast animation); the others are placeholder cylinders. Either way
    /// the root carries a collider and an EnemyRef on the Enemy layer so a right click can pick it.
    /// </summary>
    public class EliteView : MonoBehaviour
    {
        public GameDirector Director;
        public Material EliteMaterial;
        public int EnemyLayer;
        public float Height = 1.6f;
        public Color HitFlashColor = new(4f, 2.5f, 2f, 1f);
        public float TurnSpeed = 360f;     // degrees per second a model turns toward the squad

        class View
        {
            public GameObject Root;
            public EnemyRef Ref;
            public Renderer Cylinder;          // placeholder only
            public UnitVisual Visual;          // model only
            public Transform Model;            // model only
            public CapsuleCollider Pick;       // model only
            public Vector2 LastPos;
            public bool WasCasting;
            public bool Seen;
        }

        readonly Dictionary<Elite, View> _live = new();
        readonly Dictionary<EliteDefinition, Stack<View>> _free = new();
        readonly List<Elite> _gone = new();
        MaterialPropertyBlock _block;
        Color _baseEmission;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>How many mini bosses are being drawn right now.</summary>
        public int DrawnCount => _live.Count;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (EliteMaterial != null && EliteMaterial.HasProperty(EmissionId)) _baseEmission = EliteMaterial.GetColor(EmissionId);
        }

        void LateUpdate()
        {
            foreach (var v in _live.Values) v.Seen = false;

            var elites = Director != null && Director.Elites != null ? Director.Elites.Elites : null;
            if (elites != null)
                foreach (var e in elites)
                {
                    if (!e.Alive || e.Def == null) continue;
                    if (!_live.TryGetValue(e, out var v)) { v = Take(e); _live[e] = v; }
                    v.Seen = true;
                    Draw(e, v);
                }

            _gone.Clear();
            foreach (var pair in _live) if (!pair.Value.Seen) _gone.Add(pair.Key);
            foreach (var e in _gone)
            {
                var v = _live[e];
                _live.Remove(e);
                v.Root.SetActive(false);
                v.Ref.Enemy = null;
                if (!_free.TryGetValue(e.Def, out var stack)) _free[e.Def] = stack = new Stack<View>();
                stack.Push(v);
            }
        }

        void Draw(Elite e, View v)
        {
            v.Ref.Enemy = e;
            float y = GroundHeight.Y(e.X, e.Z);

            if (v.Visual == null)
            {
                v.Root.transform.position = new Vector3(e.X, y + Height * 0.5f, e.Z);
                v.Root.transform.localScale = new Vector3(e.Radius * 2f, Height * 0.5f, e.Radius * 2f);
                if (e.Flash > 0f)
                {
                    _block.SetColor(EmissionId, Color.Lerp(_baseEmission, HitFlashColor, Mathf.Clamp01(e.Flash)));
                    v.Cylinder.SetPropertyBlock(_block);
                }
                else if (v.Cylinder.HasPropertyBlock()) v.Cylinder.SetPropertyBlock(null);
                return;
            }

            v.Root.transform.position = new Vector3(e.X, y, e.Z);
            var size = e.Def.Prefab.transform.localScale * e.Def.ModelScale;
            if (v.Model.localScale != size) v.Model.localScale = size;      // follows the definition, so it can be tuned while playing
            v.Pick.radius = e.Radius;                                        // what a click hits stays the game's size, whatever the model's

            var squad = Director != null ? Director.Squad : null;
            var to = e.Entering ? e.Heading : (squad != null ? squad.Anchor - e.Pos : Vector2.zero);   // walking in: face the way it walks
            if (to.sqrMagnitude > 1e-6f)
            {
                var want = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg + e.Def.ModelYawOffset, 0f);
                v.Model.rotation = Quaternion.RotateTowards(v.Model.rotation, want, TurnSpeed * Time.deltaTime);
            }

            bool moving = (e.Pos - v.LastPos).sqrMagnitude > 1e-8f;
            if (e.Casting && !v.WasCasting) v.Visual.PlayAttack(e.Def.CastAnimSeconds);
            else v.Visual.SetState(moving ? VisualState.Run : VisualState.Idle);
            v.WasCasting = e.Casting;
            v.LastPos = e.Pos;
        }

        // A view for this mini boss: a pooled one of its kind, or a new one.
        View Take(Elite e)
        {
            View v;
            if (_free.TryGetValue(e.Def, out var stack) && stack.Count > 0)
            {
                v = stack.Pop();
                v.Root.SetActive(true);
            }
            else v = Create(e.Def);
            v.LastPos = e.Pos; v.WasCasting = false;
            if (v.Visual != null)
            {
                var squad = Director != null ? Director.Squad : null;
                var to = e.Entering ? e.Heading : (squad != null ? squad.Anchor - e.Pos : Vector2.zero);   // walking in: face the way it walks
                if (to.sqrMagnitude > 1e-6f)
                    v.Model.rotation = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg + e.Def.ModelYawOffset, 0f);
                v.Visual.SetState(VisualState.Idle);
            }
            return v;
        }

        View Create(EliteDefinition def)
        {
            var v = new View();
            if (def.Prefab != null)
            {
                v.Root = new GameObject("Mini Boss (" + def.name + ")") { layer = EnemyLayer };
                v.Root.transform.SetParent(transform, false);
                v.Pick = v.Root.AddComponent<CapsuleCollider>();          // the model has no collider of its own to click
                v.Pick.height = Mathf.Max(Height, def.Radius * 2f);
                v.Pick.center = new Vector3(0f, v.Pick.height * 0.5f, 0f);
                v.Pick.radius = def.Radius;
                v.Ref = v.Root.AddComponent<EnemyRef>();

                var model = Instantiate(def.Prefab, v.Root.transform);
                model.name = def.Prefab.name;
                foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                v.Model = model.transform;
                v.Model.localPosition = Vector3.zero;
                v.Visual = model.GetComponent<UnitVisual>();
                if (v.Visual == null)
                {
                    v.Visual = model.AddComponent<UnitVisual>();
                    v.Visual.States = def.States;
                }
                return v;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);   // keeps its collider: it must be clickable
            go.name = "Mini Boss";
            go.layer = EnemyLayer;
            go.transform.SetParent(transform, false);
            v.Root = go;
            v.Ref = go.AddComponent<EnemyRef>();
            v.Cylinder = go.GetComponent<Renderer>();
            if (EliteMaterial != null) v.Cylinder.sharedMaterial = EliteMaterial;
            return v;
        }
    }
}
