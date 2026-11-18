using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;

namespace BoneSquadKiting.Bosses
{
    /// <summary>
    /// Casts a pattern sequence for one enemy (the boss or a mini boss): wait, telegraph, then deal damage
    /// inside the pattern's danger shape. A zone can follow the squad until it locks (boss), stay where it
    /// was announced (mini boss), and turn while it is up. A burst pattern goes off once when the telegraph
    /// ends; a sustained pattern then stays for a while and damages at intervals.
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
            public float Angle;         // degrees the zone has turned, clockwise seen from above
            public bool Sustaining;     // telegraph is over and a sustained pattern is dealing damage
            public float SustainRemaining, TickIn;
            public int Hits, Lost;      // totals so far, for sustained patterns
        }

        public AttackSequence Sequence;
        public Active? Current;
        public int Index;
        public float NextAttack = GameConstants.FirstAttackDelay;
        public bool TrackSquad = true;    // the boss's zones follow the squad until the lock time; a mini boss's do not
        public float FixedGap = -1f;      // > 0: seconds between casts, instead of the boss's HP-based gap
        public float FixedGapRandom = 0f; // with FixedGap: up to this many extra seconds, rolled for every gap
        public float RotationSpeed = 0f;  // degrees per second the caster's zones turn; sign = direction (+ = clockwise), 0 = still

        public System.Action<PatternDefinition> OnStart;
        public System.Action<int, int> OnResolved; // units hit, units lost (totals, once the pattern is over)
        public System.Action<int, int> OnDamage;   // units hit, units lost, every time damage lands (each sustained tick too)

        public void Tick(FormationController squad, Boss boss, float dt)
        {
            if (Current is not Active a)
            {
                NextAttack -= dt;
                if (NextAttack <= 0) StartPattern(squad);
                return;
            }

            a.Elapsed += dt;
            a.Angle = Mathf.Repeat(a.Angle + RotationSpeed * dt, 360f);

            if (a.Sustaining)
            {
                a.SustainRemaining -= dt;
                a.TickIn -= dt;
                if (a.TickIn <= 0f)
                {
                    a.TickIn += Mathf.Max(0.05f, a.Pattern.TickInterval);
                    Damage(ref a, squad.Units, a.Pattern.TickDamage);
                }
                Current = a;
                if (a.SustainRemaining <= 0f) Finish(a, boss);
                return;
            }

            a.Remaining = Mathf.Max(0, a.Pattern.Time - a.Elapsed);
            if (a.Tracks && a.Remaining > GameConstants.CastTrackLockTime)
            {
                float k = Mathf.Min(1, dt * TrackLerp);
                a.Cx = Mathf.Lerp(a.Cx, squad.Anchor.x, k);
                a.Cz = Mathf.Lerp(a.Cz, squad.Anchor.y, k);
            }
            Current = a;
            if (a.Remaining > 0) return;

            if (a.Pattern.Kind == PatternKind.Sustained && a.Pattern.SustainSeconds > 0f)
            {
                // The telegraph is over: the zone is live now and keeps turning. First damage lands at once.
                a.Sustaining = true;
                a.SustainRemaining = a.Pattern.SustainSeconds;
                a.TickIn = 0f;
                Current = a;
                return;
            }
            Damage(ref a, squad.Units, GameConstants.PatternUnitDamage);   // burst: once, at the angle it has reached
            Finish(a, boss);
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
        {
            var a = new Active { Shape = shape, Cx = cx, Cz = cz };
            Damage(ref a, units, GameConstants.PatternUnitDamage);
            Finish(a, null);
        }

        // Hurt every unit inside the zone as it stands right now (position and angle).
        void Damage(ref Active a, IList<SquadUnit> units, float amount)
        {
            // Decide who is caught before dealing any damage, so a unit killed by the leader's share
            // still counts as hit by its own.
            var caught = new List<SquadUnit>();
            foreach (var u in units)
                if (!u.Dead && DangerShapeEval.InDanger(a.Shape, a.Cx, a.Cz, u.X, u.Z, a.Angle)) caught.Add(u);
            int aliveBefore = 0; foreach (var u in units) if (!u.Dead) aliveBefore++;
            foreach (var u in caught) SquadDamage.Apply(units, u, amount);
            int aliveAfter = 0; foreach (var u in units) if (!u.Dead) aliveAfter++;
            int lost = aliveBefore - aliveAfter;
            a.Hits += caught.Count; a.Lost += lost;
            OnDamage?.Invoke(caught.Count, lost);
        }

        void Finish(Active a, Boss boss)
        {
            Current = null;
            NextAttack = FixedGap > 0f ? FixedGap + Random.value * FixedGapRandom
                : (boss != null && boss.HpFrac < GameConstants.FastAttackHpFrac)
                    ? GameConstants.NextAttackPhase2 : GameConstants.NextAttackPhase1;
            OnResolved?.Invoke(a.Hits, a.Lost);
        }
    }
}
