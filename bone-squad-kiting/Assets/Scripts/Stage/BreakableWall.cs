using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Audio;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Visuals;

namespace BoneSquadKiting.Stage
{
    /// <summary>
    /// A wall or gate that blocks the way until the squad destroys it. It is a target like any enemy: the
    /// squad shoots it, it flashes when hit (its renderers need the Mob PBR material, like a mini boss), and
    /// the bar at the top of the screen shows its health while the squad is near. When its health runs out
    /// it breaks: its objects disappear with an effect and the ground under its Blocker outline opens, so
    /// the squad can walk through. Put the object where the squad should aim; a new match rebuilds it.
    /// </summary>
    public class BreakableWall : MonoBehaviour, IEnemy, IDamageable
    {
        public GameDirector Director;
        public string DisplayName = "성문";
        public float MaxHp = 8000f;
        [Tooltip("Shots that come this close to this object's position hit it.")]
        public float HitRadius = 4f;
        [Tooltip("The bar at the top shows this wall once the squad is this close.")]
        [Min(0f)] public float ShowBarDistance = 12f;

        [Header("Looks")]
        [Tooltip("These flash when the wall is hit. They need a Mob PBR material (the enemy outline look).")]
        public Renderer[] Renderers = new Renderer[0];
        public Color HitFlashColor = new(4f, 2.5f, 2f, 1f);
        [Tooltip("Hidden when the wall breaks (and shown again when a new match starts).")]
        public GameObject[] BreakObjects = new GameObject[0];
        public GameObject BreakEffect;
        public float BreakEffectScale = 6f;
        [Tooltip("The effect is played at this many points along the wall (this object's left-right axis), spread over Break Width.")]
        public int BreakEffectCount = 3;
        public float BreakWidth = 8f;
        public float BreakShake = 0.6f;
        public float BreakShakeSeconds = 0.5f;
        public string BreakSound = "boss.greatsword.impact";
        [Tooltip("Mini bosses standing this close to the wall (on top of it) die with it. 0 = none.")]
        public float KillMiniBossesWithin = 8f;

        [Header("Way through")]
        [Tooltip("An Exclude outline over the ground the wall stands on. That ground cannot be walked on until the wall breaks.")]
        public WalkOutline Blocker;
        [Tooltip("This is the last gate of the stage. Breaking it ends reinforcements.")]
        public bool EndsReinforcements;
        [Tooltip("Troops that approach from behind when the gate is first hit and when it reaches half health.")]
        public MobDefinition RearGuard;
        [Min(1)] public int RearGuardCount = 6;
        [Tooltip("World-space directions shown after this gate opens.")]
        public GameObject ExitGuide;

        /// <summary>The wall the top bar should show right now, if any.</summary>
        public static BreakableWall Shown { get; private set; }

        public float Health { get; private set; }
        public float HpFrac => MaxHp > 0f ? Health / MaxHp : 0f;
        public bool Broken { get; private set; }
        public float Flash { get; private set; }

        // IEnemy
        public Vector2 Pos => new(transform.position.x, transform.position.z);
        public bool Alive => !Broken && Health > 0f;
        public float Radius => HitRadius;
        public EnemyKind Kind => EnemyKind.Elite;
        public float Hp => Health;
        public float Reserved { get; set; }

        EnemyRegistry _registry;
        MaterialPropertyBlock _block;
        HitFlash[] _flashes;
        BoneSquadKiting.Cameras.EdgeScrollCamera _cam;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _flashes = new HitFlash[Renderers.Length];
            Health = MaxHp;
            foreach (var part in BreakObjects)
                if (part != null) EnemyRef.AddBodyPick(part)?.Bind(this);
        }

        void OnDisable()
        {
            if (Shown == this) Shown = null;
            _registry?.Unregister(this);
            _registry = null;
        }

        public void ApplyDamage(float amount)
        {
            if (!Alive || amount <= 0f) return;
            float prior = Health;
            Health = Mathf.Max(0f, Health - amount);
            Flash = 0.8f;
            if (Health <= 0f) Break();
            else if (prior >= MaxHp || (prior > MaxHp * .5f && Health <= MaxHp * .5f)) CallRearGuard();
        }

        void CallRearGuard()
        {
            if (RearGuard == null || Director == null || Director.Mobs == null || Director.Stage == null ||
                Director.Stage.EndPoint == null || Director.Squad.Grid == null) return;
            var exit = Director.Stage.EndPoint.position;
            var forward = (new Vector2(exit.x, exit.z) - Pos).normalized;
            var right = new Vector2(forward.y, -forward.x);
            var rear = Director.Squad.Anchor - forward * 7.5f;
            for (int i = 0; i < RearGuardCount; i++)
            {
                var spot = rear + right * ((i % 3 - 1) * 1.1f) - forward * (i / 3);
                var ground = Director.Squad.Grid.NearestWalkable(spot);
                if (Director.Mobs.Spawn(RearGuard, ground, Director.Enemies) == null) break;
            }
        }

        void Update()
        {
            if (Director == null) { Director = FindFirstObjectByType<GameDirector>(); if (Director == null) return; }
            // Every match has a new enemy list: that is the sign to stand the wall up again and join it.
            if (Director.Phase == GamePhase.Play && Director.Enemies != null && Director.Enemies != _registry) Rebuild(Director.Enemies);

            Flash = Mathf.Max(0f, Flash - Time.deltaTime * 3f);
            for (int i = 0; i < Renderers.Length; i++)
                if (Renderers[i] != null) _flashes[i].Apply(Renderers[i], _block, Flash, HitFlashColor);

            bool near = Director.Squad != null && (Director.Squad.Anchor - Pos).sqrMagnitude <= ShowBarDistance * ShowBarDistance;
            bool show = Alive && Director.Phase == GamePhase.Play && _registry != null && near;
            if (show) Shown = this; else if (Shown == this) Shown = null;
        }

        void Rebuild(EnemyRegistry registry)
        {
            _registry?.Unregister(this);
            _registry = registry;
            if (ExitGuide != null) ExitGuide.SetActive(false);
            Broken = false; Health = MaxHp; Flash = 0f; Reserved = 0f;
            foreach (var go in BreakObjects) if (go != null) go.SetActive(true);
            SetGround(false);
            registry.Register(this);
        }

        void Break()
        {
            Broken = true; Health = 0f;
            if (Shown == this) Shown = null;
            _registry?.Unregister(this);
            foreach (var go in BreakObjects) if (go != null) go.SetActive(false);
            SetGround(true);
            if (EndsReinforcements && Director != null) Director.EndStageReinforcements();
            if (ExitGuide != null) ExitGuide.SetActive(true);
            // Whoever stood on the wall falls with it.
            if (KillMiniBossesWithin > 0f && Director != null && Director.Elites != null)
                foreach (var e in Director.Elites.Elites.ToArray())
                    if (e.Alive && (e.Pos - Pos).magnitude <= KillMiniBossesWithin) e.ApplyDamage(float.MaxValue);

            if (BreakEffect != null)
            {
                int n = Mathf.Max(1, BreakEffectCount);
                for (int i = 0; i < n; i++)
                {
                    float along = n == 1 ? 0f : Mathf.Lerp(-0.5f, 0.5f, i / (float)(n - 1)) * BreakWidth;
                    var at = transform.position + transform.right * along;
                    at.y = GroundHeight.Y(at.x, at.z);
                    var fx = Instantiate(BreakEffect, at, Quaternion.LookRotation(Vector3.up));
                    fx.transform.localScale = BreakEffect.transform.localScale * BreakEffectScale;
                    Destroy(fx, 4f);
                }
            }
            if (!string.IsNullOrEmpty(BreakSound)) SoundManager.Current?.PlaySfx(BreakSound);
            if (BreakShake > 0f)
            {
                if (_cam == null) _cam = FindFirstObjectByType<BoneSquadKiting.Cameras.EdgeScrollCamera>();
                if (_cam != null) _cam.PlayPresentationShake(BreakShake, BreakShakeSeconds);
            }
        }

        // Open or close the ground under the Blocker outline, in the grid the squad and the mobs already use.
        // Open ground still has to lie inside one of the stage's Include outlines.
        void SetGround(bool open)
        {
            var grid = Director != null && Director.Squad != null ? Director.Squad.Grid : null;
            if (grid == null || Blocker == null) return;
            var hole = Blocker.Points();
            if (hole.Length < 3) return;
            var includes = new List<Vector2[]>();
            foreach (var outline in FindObjectsByType<WalkOutline>(FindObjectsSortMode.None))
                if (outline.gameObject.scene == gameObject.scene && outline.Mode == WalkOutlineMode.Include && outline.Points().Length >= 3) includes.Add(outline.Points());
            for (int z = 0; z < grid.Height; z++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var c = grid.CellCenter(x, z);
                    if (!Polygon.Contains(hole, c)) continue;
                    bool walkable = open;
                    if (open && includes.Count > 0)
                    {
                        walkable = false;
                        foreach (var area in includes) if (Polygon.Contains(area, c)) { walkable = true; break; }
                    }
                    grid.Walkable[grid.Index(x, z)] = walkable;
                }
            var flow = Director.Flow;
            if (flow != null) flow.Build(flow.Target);      // the mobs' routes follow the change
        }
    }
}
