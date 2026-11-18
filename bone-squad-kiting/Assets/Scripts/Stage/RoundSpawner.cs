using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Mobs;

namespace BoneSquadKiting.Stage
{
    /// <summary>How a stage is cleared. Chosen per stage on GameDirector.</summary>
    public enum StageClearRule
    {
        DefeatBoss,             // boss stage: the boss dies
        KillAllThenReachEnd,    // every round wiped out, then walk to the end point
        KillAll,                // every round wiped out
        ReachEnd,               // just get to the end point
    }

    /// <summary>
    /// A stage's enemies, as a fixed list of rounds. Each round spawns a set number of one mob kind around
    /// its spawn points. There is no endless spawning: when the last round is dead the stage has no enemies left.
    /// </summary>
    public class RoundSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class Round
        {
            public MobDefinition Mob;
            public int Count = 20;
            public Transform[] SpawnPoints;   // mobs are spread over these; none = around this object
            public float Spread = 2f;         // random scatter around each point
        }

        public List<Round> Rounds = new();
        public float FirstRoundDelay = 2f;    // seconds into the match before round 1

        public int Started { get; private set; }
        public int Total => Rounds.Count;
        public bool AllStarted => Started >= Rounds.Count;

        public void ResetRounds() { Started = 0; }

        /// <summary>Start the next round when it is due. Returns true if a round started this call.</summary>
        public bool Tick(MobSystem mobs, EnemyRegistry registry, WalkGrid grid, float elapsed)
        {
            if (AllStarted || !ShouldStartNext(mobs.AliveCount, elapsed)) return false;
            Spawn(Rounds[Started], mobs, registry, grid);
            Started++;
            return true;
        }

        // The one place that decides when the next round begins. Current rule: the first round after a short
        // delay, each later round once the previous one is wiped out. (Other triggers — a timer, how far the
        // squad has advanced — are undecided; change this method when they are.)
        bool ShouldStartNext(int mobsAlive, float elapsed)
        {
            if (Started == 0) return elapsed >= FirstRoundDelay;
            return mobsAlive == 0;
        }

        void Spawn(Round round, MobSystem mobs, EnemyRegistry registry, WalkGrid grid)
        {
            if (round.Mob == null) return;
            for (int i = 0; i < round.Count; i++)
            {
                Vector3 origin = round.SpawnPoints != null && round.SpawnPoints.Length > 0 && round.SpawnPoints[i % round.SpawnPoints.Length] != null
                    ? round.SpawnPoints[i % round.SpawnPoints.Length].position : transform.position;
                var p = new Vector2(origin.x, origin.z) + Random.insideUnitCircle * round.Spread;
                if (grid != null) p = grid.NearestWalkable(p);
                mobs.Spawn(round.Mob, p, registry);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.9f);
            for (int r = 0; r < Rounds.Count; r++)
            {
                var points = Rounds[r].SpawnPoints;
                if (points == null) continue;
                foreach (var t in points)
                {
                    if (t == null) continue;
                    Gizmos.DrawWireSphere(t.position + Vector3.up * 0.1f, Mathf.Max(0.3f, Rounds[r].Spread));
                    Gizmos.DrawLine(t.position, t.position + Vector3.up * (1.5f + r * 0.5f)); // taller pole = later round
                }
            }
        }
    }
}
