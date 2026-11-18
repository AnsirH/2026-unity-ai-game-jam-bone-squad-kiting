using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Mobs;
using BoneSquadKiting.Squad;

namespace BoneSquadKiting.Stage
{
    /// <summary>How a stage is cleared. Chosen per stage on GameDirector.</summary>
    public enum StageClearRule
    {
        DefeatBoss,                    // boss stage: the boss dies
        ReachEnd,                      // the squad gets to the end point
        DefeatMiniBossThenReachEnd,    // every mini boss on the stage dies, then the squad gets to the end point
    }

    /// <summary>
    /// Endless waves of mobs that get bigger and more frequent the further the squad has advanced from
    /// the start point toward the end point. Standing still does not stop the waves, and does not speed
    /// them up either: the pace follows the furthest progress reached so far.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [Tooltip("This stage's wave setting (one asset = one version). When set, it replaces every field below.")]
        public WavePlan Plan;

        [Header("Without a plan: one mob kind, paced by two curves")]
        public MobDefinition Mob;

        [Header("Pace by progress (x: 0 = start point, 1 = end point)")]
        public AnimationCurve Interval = AnimationCurve.Linear(0f, 12f, 1f, 5f);   // seconds between waves
        public AnimationCurve Count = AnimationCurve.Linear(0f, 8f, 1f, 30f);      // mobs per wave
        public float FirstWaveDelay = 3f;

        [Header("Where mobs appear")]
        public float MinSpawnDistance = 14f;   // from the squad anchor: far enough to be off screen
        public float MaxSpawnDistance = 22f;
        [Range(0f, 1f)] public float AheadShare = 0.7f;   // share of each wave that comes from the end-point side
        public int GroupSize = 8;              // mobs per spawn cluster
        public float GroupSpread = 1.5f;

        [Header("Testing")]
        [Tooltip("Hold the wave timer (bursts wait too). For looking at one wave at a time.")]
        public bool Hold;

        public int WavesSpawned { get; private set; }
        /// <summary>Mobs that have appeared this match, waves and bursts together.</summary>
        public int MobsSpawned { get; private set; }
        /// <summary>The plan row in use, −1 without a plan or before its first row starts.</summary>
        public int SectionIndex => Plan != null ? Plan.SectionIndexAt(MaxProgress) : -1;

        float MinDistance => Plan != null ? Plan.MinSpawnDistance : MinSpawnDistance;
        float MaxDistance => Plan != null ? Plan.MaxSpawnDistance : MaxSpawnDistance;
        float Ahead => Plan != null ? Plan.AheadShare : AheadShare;
        int Group => Mathf.Max(1, Plan != null ? Plan.GroupSize : GroupSize);
        float Spread => Plan != null ? Plan.GroupSpread : GroupSpread;

        readonly List<WaveEntry> _wave = new();
        readonly HashSet<int> _burstsDone = new();

        /// <summary>Make the next wave arrive on the next step.</summary>
        public void SpawnNow() => TimeToNextWave = 0f;
        /// <summary>Furthest progress reached this match, 0..1. Never goes down.</summary>
        public float MaxProgress { get; private set; }
        public float TimeToNextWave { get; private set; }
        /// <summary>Set to end the waves for the rest of the match (after the mini boss falls).</summary>
        public bool Stopped { get; set; }

        FlowField _toEnd;      // distance-to-end-point field, for measuring progress along the actual route
        int _startDistance;

        /// <summary>Call at match start. Without a grid or an end point, progress stays at 0.</summary>
        public void Begin(WalkGrid grid, Vector2 start, Vector2? end)
        {
            WavesSpawned = 0; MobsSpawned = 0; MaxProgress = 0f; Stopped = false;
            TimeToNextWave = Plan != null ? Plan.FirstWaveDelay : FirstWaveDelay;
            _burstsDone.Clear();
            _toEnd = null; _startDistance = 0;
            if (grid == null || end == null) return;
            _toEnd = new FlowField(grid);
            _toEnd.Build(grid.Cell(grid.NearestWalkable(end.Value)));
            _startDistance = _toEnd.DistanceAt(start);
        }

        /// <summary>Progress for a position: 1 − (distance to the end / the start point's distance).</summary>
        public float ProgressAt(Vector2 position)
        {
            if (_toEnd == null || _startDistance <= 0) return 0f;
            int d = _toEnd.DistanceAt(position);
            if (d == FlowField.Unreachable) return MaxProgress;
            return Mathf.Clamp01(1f - d / (float)_startDistance);
        }

        /// <summary>Advance the wave timer. Returns the number of mobs spawned this call (0 = no wave).</summary>
        public int Tick(FormationController squad, MobSystem mobs, EnemyRegistry registry, float dt)
        {
            MaxProgress = Mathf.Max(MaxProgress, ProgressAt(squad.Anchor));
            if (Stopped || Hold) return 0;
            int spawned = 0;

            // One-off groups: the first time the squad gets this far. Read from the plan every step, so a
            // plan edited while playing applies at once.
            if (Plan != null)
                for (int i = 0; i < Plan.Bursts.Count; i++)
                {
                    var burst = Plan.Bursts[i];
                    if (burst == null || burst.AtProgress > MaxProgress || !_burstsDone.Add(i)) continue;
                    spawned += SpawnWave(burst.Mobs, squad, mobs, registry);
                }

            TimeToNextWave -= dt;
            if (TimeToNextWave > 0f) { MobsSpawned += spawned; return spawned; }

            if (Plan != null)
            {
                float interval = Plan.IntervalAt(MaxProgress);
                if (interval <= 0f) { TimeToNextWave = 0.5f; MobsSpawned += spawned; return spawned; }   // no row has started yet: look again shortly
                TimeToNextWave = interval;
                Plan.CompositionAt(MaxProgress, _wave);
            }
            else
            {
                if (Mob == null) { MobsSpawned += spawned; return spawned; }
                TimeToNextWave = Mathf.Max(0.5f, Interval.Evaluate(MaxProgress));
                _wave.Clear();
                _wave.Add(new WaveEntry { Mob = Mob, Count = Mathf.Max(0, Mathf.RoundToInt(Count.Evaluate(MaxProgress))) });
            }
            spawned += SpawnWave(_wave, squad, mobs, registry);
            WavesSpawned++;
            MobsSpawned += spawned;
            return spawned;
        }

        int SpawnWave(List<WaveEntry> entries, FormationController squad, MobSystem mobs, EnemyRegistry registry)
        {
            var grid = squad.Grid;
            List<Vector2> ahead = new(), behind = new();
            if (grid != null) CollectSpawnCells(grid, squad.Anchor, ahead, behind);

            int spawned = 0;
            Vector2 center = default;
            foreach (var entry in entries)
            {
                if (entry == null || entry.Mob == null) continue;
                for (int i = 0; i < entry.Count; i++)
                {
                    if (spawned % Group == 0) center = PickCenter(squad.Anchor, ahead, behind);
                    var p = center + Random.insideUnitCircle * Spread;
                    if (grid != null) p = grid.NearestWalkable(p);
                    if (mobs.Spawn(entry.Mob, p, registry) == null) return spawned;   // alive cap reached: the wave is cut short
                    spawned++;
                }
            }
            return spawned;
        }

        // Walkable cells in the spawn ring around the squad, split by whether they are nearer the end point.
        void CollectSpawnCells(WalkGrid grid, Vector2 anchor, List<Vector2> ahead, List<Vector2> behind)
        {
            int here = _toEnd != null ? _toEnd.DistanceAt(anchor) : FlowField.Unreachable;
            for (int z = 0; z < grid.Height; z++)
                for (int x = 0; x < grid.Width; x++)
                {
                    if (!grid.Walkable[grid.Index(x, z)]) continue;
                    var c = grid.CellCenter(x, z);
                    float d = (c - anchor).magnitude;
                    if (d < MinDistance || d > MaxDistance) continue;
                    int toEnd = _toEnd != null ? _toEnd.Distance[grid.Index(x, z)] : FlowField.Unreachable;
                    bool isAhead = here != FlowField.Unreachable && toEnd != FlowField.Unreachable && toEnd < here;
                    (isAhead ? ahead : behind).Add(c);
                }
        }

        Vector2 PickCenter(Vector2 anchor, List<Vector2> ahead, List<Vector2> behind)
        {
            bool wantAhead = Random.value < Ahead;
            var pool = wantAhead && ahead.Count > 0 ? ahead : behind.Count > 0 ? behind : ahead;
            if (pool.Count > 0) return pool[Random.Range(0, pool.Count)];
            // No grid, or nothing walkable in the ring: a point on the ring around the squad.
            return anchor + Random.insideUnitCircle.normalized * MinDistance;
        }
    }
}
