using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;

namespace BoneSquadKiting.Bosses
{
    /// <summary>
    /// Runs the boss's pattern sequence: wait, telegraph (zone follows the squad until the lock time),
    /// then resolve against the pattern's danger shape.
    /// </summary>
    public class BossAttackSystem
    {
        const float TrackLerp = 11f;

        public struct Active
        {
            public PatternDefinition Pattern;
            public DangerShape Shape;   // the pattern's shape as cast (already scaled to the squad if it scales)
            public float Cx, Cz, Elapsed, Remaining;
            public bool Tracks;         // whether the zone follows the squad until the lock time
        }

        public AttackSequence Sequence;
        public Active? Current;
        public int Index;
        public float NextAttack = GameConstants.FirstAttackDelay;
        public bool TrackSquad = true;   // the boss's zones follow the squad until the lock time; a mini boss's do not
        public float FixedGap = -1f;     // > 0: seconds between casts, instead of the boss's HP-based gap
        public float FixedGapRandom = 0f; // with FixedGap: up to this many extra seconds, rolled for every gap

        public System.Action<PatternDefinition> OnStart;
        public System.Action<int, int> OnResolved; // units hit, units lost

        public void Tick(FormationController squad, Boss boss, float dt)
        {
            if (Current is Active a)
            {
                a.Elapsed += dt;
                a.Remaining = Mathf.Max(0, a.Pattern.Time - a.Elapsed);
                if (a.Tracks && a.Remaining > GameConstants.CastTrackLockTime)
                {
                    float k = Mathf.Min(1, dt * TrackLerp);
                    a.Cx = Mathf.Lerp(a.Cx, squad.Anchor.x, k);
                    a.Cz = Mathf.Lerp(a.Cz, squad.Anchor.y, k);
                }
                Current = a;
                if (a.Remaining <= 0) Resolve(a.Shape, a.Cx, a.Cz, squad.Units, boss);
            }
            else
            {
                NextAttack -= dt;
                if (NextAttack <= 0) StartPattern(squad);
            }
        }

        void StartPattern(FormationController squad)
        {
            if (Sequence == null || Sequence.Patterns == null || Sequence.Patterns.Length == 0) return;
            var p = Sequence.Patterns[Index % Sequence.Patterns.Length];
            Index++;
            var shape = p.ScaleWithSquad ? p.Shape.Scaled(FormationSlots.SquadFraction(squad.AliveCount)) : p.Shape;
            Current = new Active { Pattern = p, Shape = shape, Tracks = TrackSquad, Cx = squad.Anchor.x, Cz = squad.Anchor.y, Elapsed = 0, Remaining = p.Time };
            OnStart?.Invoke(p);
        }

        /// <summary>Resolve a shape immediately. For tests and debugging.</summary>
        public void ForceResolve(DangerShape shape, float cx, float cz, IList<SquadUnit> units)
            => Resolve(shape, cx, cz, units, null);

        void Resolve(DangerShape shape, float cx, float cz, IList<SquadUnit> units, Boss boss)
        {
            int hit = 0, lost = 0;
            // Decide who is caught before dealing any damage, so a unit killed by the leader's share
            // still counts as hit by its own.
            var caught = new List<SquadUnit>();
            foreach (var u in units)
                if (!u.Dead && DangerShapeEval.InDanger(shape, cx, cz, u.X, u.Z)) caught.Add(u);
            int aliveBefore = 0; foreach (var u in units) if (!u.Dead) aliveBefore++;
            foreach (var u in caught) SquadDamage.Apply(units, u, GameConstants.PatternUnitDamage);
            int aliveAfter = 0; foreach (var u in units) if (!u.Dead) aliveAfter++;
            hit = caught.Count; lost = aliveBefore - aliveAfter;
            Current = null;
            NextAttack = FixedGap > 0f ? FixedGap + Random.value * FixedGapRandom
                : (boss != null && boss.HpFrac < GameConstants.FastAttackHpFrac)
                    ? GameConstants.NextAttackPhase2 : GameConstants.NextAttackPhase1;
            OnResolved?.Invoke(hit, lost);
        }
    }
}
