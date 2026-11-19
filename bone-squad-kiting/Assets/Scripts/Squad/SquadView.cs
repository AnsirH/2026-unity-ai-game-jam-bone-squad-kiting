using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Data;
using BoneSquadKiting.Fx;
using BoneSquadKiting.Game;
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
            public HitFlash Flash;
            public Transform Root;
            public Renderer Capsule;       // placeholder only
            public UnitVisual Visual;      // model only
            public Transform SpawnPoint;   // projectile origin authored in the character prefab
            public int ShotsSeen;
            public int Index;
            public GameObject SpeedBuff;
        }

        readonly List<View> _views = new();
        GameDirector _director;
        MaterialPropertyBlock _block;

        void Awake()
        {
            if (!Application.isPlaying) return;
            // Units already placed under this object (so they show in the editor) are reused in order.
            foreach (Transform child in transform) _views.Add(Wrap(child, _views.Count));
            _block = new MaterialPropertyBlock();
        }

        void Start()
        {
            if (!Application.isPlaying || Art == null) return;
            _director = FindSquadDirector();
            if (_director == null) return;
            var reviveFx = GetComponent<ReviveBoneFx>();
            if (reviveFx == null) reviveFx = gameObject.AddComponent<ReviveBoneFx>();
            reviveFx.Bind(_director, Art);
        }

        GameDirector FindSquadDirector()
        {
            if (Squad == null) return null;
            if (Squad.TryGetComponent<GameDirector>(out var director) && director.Squad == Squad) return director;
            foreach (var candidate in FindObjectsByType<GameDirector>(FindObjectsSortMode.None))
                if (candidate.Squad == Squad) return candidate;
            return null;
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
                UpdateSpeedBuff(v, u);
                if (v.Visual != null) DriveModel(v, u);
                else DriveCapsule(v, u);
            }
        }

        // A model: feet on the ground, turned to face where it walks or shoots, animation by state.
        void DriveModel(View v, SquadUnit u)
        {
            v.Visual.SetHitFlash(u.Flash, _block);
            v.Root.position = new Vector3(u.X, BoneSquadKiting.Stage.GroundHeight.Y(u.X, u.Z), u.Z);
            var size = ModelScale(Art, u.IsLeader);
            if (v.Root.localScale != size) v.Root.localScale = size;   // follows the art set, so it can be tuned while playing
            if (u.Facing.sqrMagnitude > 1e-4f)
            {
                var want = Quaternion.LookRotation(new Vector3(u.Facing.x, 0f, u.Facing.y)) * Quaternion.Euler(0f, Art.ModelYawOffset, 0f);
                v.Root.rotation = Quaternion.RotateTowards(v.Root.rotation, want, Art.TurnSpeed * Time.deltaTime);
            }
            // The model's attack animation decides when the projectile leaves and how long the motion takes.
            // A unit meant to attack more often than its attack motion lasts plays the motion faster.
            float natural = v.Visual.NaturalAttackLength;
            v.Visual.AttackPlaybackScale = (natural > 0f && u.AttackInterval > 0f && natural > u.AttackInterval ? natural / u.AttackInterval : 1f) * u.AttackCycleSpeed;
            u.AttackWindup = v.Visual.AttackEventTime; u.AttackDuration = v.Visual.AttackLength;
            u.HasReleaseEvent = u.AttackWindup > 0f && v.Visual.Animator.isActiveAndEnabled && v.Visual.Animator.fireEvents;
            RegisterMuzzle(v, u);
            if (u.ShotCount != v.ShotsSeen) { v.ShotsSeen = u.ShotCount; v.Visual.PlayAttack(); }
            if (u.Moving) v.Visual.CancelAttack();
            v.Visual.SetState(u.Moving ? VisualState.Run : u.Reviving && v.Visual.Has(VisualState.Revive) ? VisualState.Revive : VisualState.Idle);
        }

        void UpdateSpeedBuff(View view, SquadUnit unit)
        {
            bool show = Art != null && Art.ShowAttackSpeedBuff && !unit.Dead && !unit.Moving && !unit.Reviving && unit.AtAttackSpeedCap;
            if (show && view.SpeedBuff == null && Art != null && Art.AttackSpeedBuffPrefab != null)
            {
                view.SpeedBuff = Instantiate(Art.AttackSpeedBuffPrefab);
                view.SpeedBuff.name = "Maximum Attack Speed Buff";
                view.SpeedBuff.SetActive(false);
                view.SpeedBuff.transform.localScale *= Art.AttackSpeedBuffScale;
                // Preserve the authored world size even when character FBX roots use a 100x scale.
                view.SpeedBuff.transform.position = view.Root.position + Vector3.up * Art.AttackSpeedBuffHeight;
                view.SpeedBuff.transform.SetParent(view.Root, true);
                foreach (var ps in view.SpeedBuff.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.loop = true;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.stopAction = ParticleSystemStopAction.None;
                }
            }
            if (view.SpeedBuff == null || view.SpeedBuff.activeSelf == show) return;
            view.SpeedBuff.SetActive(show);
            foreach (var ps in view.SpeedBuff.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (show && ps.gameObject.activeInHierarchy) ps.Play(false);
                else if (!show) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        void DriveCapsule(View v, SquadUnit u)
        {
            float scale = u.IsLeader ? UnitScale * LeaderScale : UnitScale;
            v.Root.position = new Vector3(u.X, scale + BoneSquadKiting.Stage.GroundHeight.Y(u.X, u.Z), u.Z);
            if (v.Root.localScale.x != scale) v.Root.localScale = Vector3.one * scale;
            if (v.Capsule == null) return;
            // Brighten units that were just hit; the flash value decays in SquadUnit.MoveToward.
            if (u.Reviving && u.Flash <= 0f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
                v.Flash.Apply(v.Capsule, _block, 0.35f + 0.65f * pulse, ReviveColor, reviving: true);
            }
            else v.Flash.Apply(v.Capsule, _block, u.Flash, HitFlashColor);
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
                view.SpawnPoint = FindChild(root, "SpawnPoint");
                if (view.Visual == null)
                {
                    view.Visual = root.gameObject.AddComponent<UnitVisual>();
                    view.Visual.States = leader && Art.LeaderPrefab != null ? Art.LeaderStates : Art.UnitStates;
                    view.Visual.Animator = root.GetComponentInChildren<Animator>();
                }
                if (Application.isPlaying)
                {
                    view.Visual.SetLoopVariation(index);
                    // The animation's release event lets the shot go at the exact frame the hand opens.
                    view.Visual.AttackReleased = () =>
                    {
                        if (_director == null) _director = FindSquadDirector();
                        if (_director != null && _director.Phase == GamePhase.Play && !_director.Paused
                            && !_director.Frozen && !_director.InCutscene && _director.Shots != null
                            && Squad != null && index < Squad.Units.Count && view.ShotsSeen == Squad.Units[index].ShotCount)
                        {
                            RegisterMuzzle(view, Squad.Units[index]);
                            _director.Shots.ReleasePending(Squad.Units[index]);
                        }
                    };
                }
            }
            else view.Capsule = root.GetComponent<Renderer>();
            return view;
        }

        void RegisterMuzzle(View view, SquadUnit unit)
        {
            if (view.SpawnPoint == null) return;
            if (_director == null) _director = FindSquadDirector();
            if (_director != null && _director.Shots != null)
                _director.Shots.SetMuzzlePosition(unit.Id, view.SpawnPoint.position);
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
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
