using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Data;
using BoneSquadKiting.Visuals;

namespace BoneSquadKiting.Squad
{
    /// <summary>
    /// Draws the squad. With an ArtSet that has unit prefabs, each unit is that model: this view only
    /// moves and turns its root and tells it what the unit is doing (its size, height and materials are
    /// the prefab's own). Without one, each unit is a placeholder capsule that flashes when hit.
    /// </summary>
    [ExecuteAlways]   // so a scale changed in the art set shows on the placed models without entering Play mode
    public class SquadView : MonoBehaviour
    {
        public FormationController Squad;
        public ArtSet Art;                 // optional: models for the soldiers and the leader

        [Header("Placeholder capsules (used when the art set has no prefab)")]
        public Material UnitMaterial;
        public float UnitScale = 0.34f;
        public float LeaderScale = 1.4f;   // the squad leader is drawn this much bigger

        public Color HitFlashColor = new(3f, 3f, 3f, 1f); // emission while a unit's hit flash is at full
        public Color ReviveColor = new(0.4f, 3f, 0.9f, 1f); // pulsing emission on a unit that is reviving a comrade

        class View
        {
            public Transform Root;
            public Renderer Capsule;       // placeholder only
            public UnitVisual Visual;      // model only
            public int ShotsSeen;
        }

        readonly List<View> _views = new();
        MaterialPropertyBlock _block;
        Color _baseEmission;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            if (!Application.isPlaying) return;
            // Units already placed under this object (so they show in the editor) are reused in order.
            foreach (Transform child in transform) _views.Add(Wrap(child, _views.Count));
            _block = new MaterialPropertyBlock();
            if (UnitMaterial != null && UnitMaterial.HasProperty(EmissionId)) _baseEmission = UnitMaterial.GetColor(EmissionId);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) { ApplyModelScaleInEditor(); return; }
            if (Squad == null) return;
            while (_views.Count < Squad.Units.Count)
            {
                var go = BuildUnit(_views.Count, Art, UnitScale, LeaderScale, UnitMaterial);
                go.transform.SetParent(transform, false);
                _views.Add(Wrap(go.transform, _views.Count));
            }
            for (int i = 0; i < _views.Count; i++)
            {
                var v = _views[i];
                bool alive = i < Squad.Units.Count && !Squad.Units[i].Dead;
                if (v.Root.gameObject.activeSelf != alive) v.Root.gameObject.SetActive(alive);
                if (!alive) continue;
                var u = Squad.Units[i];
                if (v.Visual != null) DriveModel(v, u);
                else DriveCapsule(v, u);
            }
        }

        // A model: feet on the ground, turned to face where it walks or shoots, animation by state.
        void DriveModel(View v, SquadUnit u)
        {
            v.Root.position = new Vector3(u.X, BoneSquadKiting.Stage.GroundHeight.Y(u.X, u.Z), u.Z);
            var size = ModelScale(Art, u.IsLeader);
            if (v.Root.localScale != size) v.Root.localScale = size;   // follows the art set, so it can be tuned while playing
            if (u.Facing.sqrMagnitude > 1e-4f)
            {
                var want = Quaternion.LookRotation(new Vector3(u.Facing.x, 0f, u.Facing.y)) * Quaternion.Euler(0f, Art.ModelYawOffset, 0f);
                v.Root.rotation = Quaternion.RotateTowards(v.Root.rotation, want, Art.TurnSpeed * Time.deltaTime);
            }
            if (u.ShotCount != v.ShotsSeen) { v.ShotsSeen = u.ShotCount; v.Visual.PlayAttack(); }
            v.Visual.SetState(u.Moving ? VisualState.Run : u.Reviving && v.Visual.Has(VisualState.Revive) ? VisualState.Revive : VisualState.Idle);
        }

        void DriveCapsule(View v, SquadUnit u)
        {
            float scale = u.IsLeader ? UnitScale * LeaderScale : UnitScale;
            v.Root.position = new Vector3(u.X, scale + BoneSquadKiting.Stage.GroundHeight.Y(u.X, u.Z), u.Z);
            if (v.Root.localScale.x != scale) v.Root.localScale = Vector3.one * scale;
            if (v.Capsule == null) return;
            // Brighten units that were just hit; the flash value decays in SquadUnit.MoveToward.
            if (u.Flash > 0f)
            {
                _block.SetColor(EmissionId, Color.Lerp(_baseEmission, HitFlashColor, u.Flash));
                v.Capsule.SetPropertyBlock(_block);
            }
            else if (u.Reviving)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
                _block.SetColor(EmissionId, Color.Lerp(_baseEmission, ReviveColor, 0.35f + 0.65f * pulse));
                v.Capsule.SetPropertyBlock(_block);
            }
            else if (v.Capsule.HasPropertyBlock()) v.Capsule.SetPropertyBlock(null);
        }

        /// <summary>The size a unit model should have: its prefab's own scale times the art set's multiplier.</summary>
        public static Vector3 ModelScale(ArtSet art, bool leader)
        {
            bool useLeader = leader && art.LeaderPrefab != null;
            var prefab = useLeader ? art.LeaderPrefab : art.UnitPrefab;
            return prefab.transform.localScale * (useLeader ? art.LeaderScale : art.UnitScale);
        }

        // Outside Play mode: keep the models that are placed in the scene at the art set's current size.
        void ApplyModelScaleInEditor()
        {
            if (Art == null || Art.UnitPrefab == null) return;
            bool hasLeader = Squad == null || Squad.HasLeader;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.GetComponent<MeshFilter>() != null) continue;   // a placeholder capsule, not a model
                var size = ModelScale(Art, i == 0 && hasLeader);
                if (child.localScale != size) child.localScale = size;
            }
        }

        // Work out what kind of view an object is: a model (it gets a UnitVisual if the prefab has none) or a capsule.
        View Wrap(Transform root, int index)
        {
            var view = new View { Root = root };
            bool leader = index == 0 && Squad != null && Squad.HasLeader;
            var prefab = Art == null ? null : leader && Art.LeaderPrefab != null ? Art.LeaderPrefab : Art.UnitPrefab;
            if (prefab != null && root.GetComponent<MeshFilter>() == null)   // a placed model, not a primitive capsule
            {
                view.Visual = root.GetComponent<UnitVisual>();
                if (view.Visual == null)
                {
                    view.Visual = root.gameObject.AddComponent<UnitVisual>();
                    view.Visual.States = leader && Art.LeaderPrefab != null ? Art.LeaderStates : Art.UnitStates;
                    view.Visual.Animator = root.GetComponentInChildren<Animator>();
                }
            }
            else view.Capsule = root.GetComponent<Renderer>();
            return view;
        }

        /// <summary>
        /// One unit's visual: the art set's model if it has one (the leader's for unit 0), otherwise a
        /// placeholder capsule. Also used by the scene builder to pre-place the squad.
        /// </summary>
        public static GameObject BuildUnit(int index, ArtSet art, float capsuleScale, float leaderScale, Material material, bool hasLeader = true)
        {
            bool leader = index == 0 && hasLeader;
            var prefab = art == null ? null : leader && art.LeaderPrefab != null ? art.LeaderPrefab : art.UnitPrefab;
            if (prefab != null)
            {
                var model = Instantiate(prefab);
                model.name = (leader ? "Leader " : "Unit ") + index;
                model.transform.localScale = ModelScale(art, leader);
                return model;
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Unit " + index;
            var col = go.GetComponent<Collider>(); // units are not pickable or physical
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            go.transform.localScale = Vector3.one * (leader ? capsuleScale * leaderScale : capsuleScale);
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
