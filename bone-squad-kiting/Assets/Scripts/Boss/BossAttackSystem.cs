using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Audio;
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
            public float ShapeScale;
            public float Angle;         // degrees the zone has turned, clockwise seen from above
            public bool Sustaining;     // telegraph is over and a sustained pattern is dealing damage
            public float SustainRemaining, TickIn;
            public int Hits, Lost;      // totals so far, for sustained patterns
            public bool StrikeThrown;   // the strike effect is on its way
            public bool Landed;         // the zone has gone off at least once (near misses are judged on that first moment only)
        }

        public AttackSequence Sequence;
        /// <summary>Patterns given directly (a mini boss's list). Used instead of Sequence when set.</summary>
        public IReadOnlyList<PatternDefinition> Patterns;
        public CastVoice CastSound;
        public Active? Current;
        public int Index;
        public float NextAttack = GameConstants.FirstAttackDelay;
        public bool TrackSquad = true;    // the boss's zones follow the squad until the lock time; a mini boss's do not
        public float FixedGap = -1f;      // > 0: seconds between casts, instead of the boss's HP-based gap
        public float FixedGapRandom = 0f; // with FixedGap: up to this many extra seconds, rolled for every gap
        public float RotationSpeed = 0f;  // degrees per second the caster's zones turn; sign = direction (+ = clockwise), 0 = still

        /// <summary>Where the caster stands, when it is known: effects that fly in from the caster start there.</summary>
        public bool HasCaster; public float CasterX, CasterZ;

        public System.Action<PatternDefinition> OnStart;
        public System.Action<int, int> OnResolved; // units hit, units lost (totals, once the pattern is over)
        public System.Action<Active> OnImpact; // optional caster-specific impact at its resolved position and size
        public System.Action<int, int> OnDamage;   // units hit, units lost, every time damage lands (each sustained tick too)
        /// <summary>
        /// A zone went off, caught nobody, and this many units stood within NearMissMargin of it. Judged
        /// once per pattern, on the moment it first goes off.
        /// </summary>
        public System.Action<int> OnNearMiss;
        /// <summary>How close to a zone's edge (outside it) counts as a near miss. 0 = near misses are off.</summary>
        public static float NearMissMargin = 1f;

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

            float previousRemaining = a.Remaining;
            a.Remaining = Mathf.Max(0, a.Pattern.Time - a.Elapsed);
            float approach = Mathf.Min(GameConstants.CastTrackLockTime, a.Pattern.Time * 0.5f);
            if (previousRemaining > approach && a.Remaining <= approach)
                CastSounds.Approach(CastSound, a.Pattern.Id);
            if (a.Tracks && a.Remaining > GameConstants.CastTrackLockTime)
            {
                float k = Mathf.Min(1, dt * TrackLerp);
                a.Cx = Mathf.Lerp(a.Cx, squad.Anchor.x, k);
                a.Cz = Mathf.Lerp(a.Cz, squad.Anchor.y, k);
            }
            if (!a.StrikeThrown && a.Pattern.StrikeEffect != null && a.Pattern.Kind == PatternKind.Burst
                && a.Remaining <= a.Pattern.StrikeFlightSeconds)
            {
                // Thrown early enough to land as the zone goes off. Without a known caster it drops from where the zone is.
                a.StrikeThrown = true;
                var caster = HasCaster ? new Vector2(CasterX, CasterZ) : boss != null ? boss.Pos : new Vector2(a.Cx, a.Cz);
                BoneSquadKiting.Fx.ZoneStrikeFx.Play(a.Pattern, a.Shape, a.Cx, a.Cz, a.Angle, caster, a.Remaining);
            }
            Current = a;
            if (a.Remaining > 0) return;
            CastSounds.Land(CastSound, a.Pattern.Id);

            if (a.Pattern.Kind == PatternKind.Sustained && a.Pattern.SustainSeconds > 0f)
            {
                // The telegraph is over: the zone is live now and keeps turning. First damage lands at once.
                a.Sustaining = true;
                a.SustainRemaining = a.Pattern.SustainSeconds;
                a.TickIn = 0f;
                Current = a;
                return;
            }
            if (a.Pattern.BurstEffect != null)
            {
                var effect = Object.Instantiate(a.Pattern.BurstEffect, new Vector3(a.Cx, 0.03f, a.Cz), Quaternion.Euler(0f, a.Angle, 0f));
                if (a.Pattern.ScaleWithSquad) effect.transform.localScale *= a.ShapeScale;
            }
            BoneSquadKiting.Fx.ZoneFillFx.Play(a.Pattern, a.Shape, a.Cx, a.Cz, a.Angle);
            Damage(ref a, squad.Units, GameConstants.PatternUnitDamage);   // burst: once, at the angle it has reached
            Finish(a, boss);
        }

        void StartPattern(FormationController squad)
        {
            IReadOnlyList<PatternDefinition> list = Patterns ?? (Sequence != null ? Sequence.Patterns : null);
            if (list == null || list.Count == 0) return;
            var p = list[Index % list.Count];
            Index++;
            if (p == null) return;   // an empty slot in the list: skip to the next one on the next cast
            float scale = p.ScaleWithSquad ? FormationSlots.SquadFraction(squad.AliveCount) : 1f;
            var shape = p.ScaleWithSquad ? p.Shape.Scaled(scale) : p.Shape;
            Current = new Active { Pattern = p, Shape = shape, ShapeScale = scale, Tracks = TrackSquad, Cx = squad.Anchor.x, Cz = squad.Anchor.y, Elapsed = 0, Remaining = p.Time };
            CastSounds.Begin(CastSound, p.Id);
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
            if (!a.Landed)
            {
                a.Landed = true;
                if (caught.Count == 0 && NearMissMargin > 0f)
                {
                    int near = 0;
                    foreach (var u in units)
                        if (!u.Dead && DangerShapeEval.NearDanger(a.Shape, a.Cx, a.Cz, u.X, u.Z, a.Angle, NearMissMargin)) near++;
                    if (near > 0) OnNearMiss?.Invoke(near);
                }
            }
            int aliveBefore = 0; foreach (var u in units) if (!u.Dead) aliveBefore++;
            foreach (var u in caught) SquadDamage.Apply(units, u, amount);
            int aliveAfter = 0; foreach (var u in units) if (!u.Dead) aliveAfter++;
            int lost = aliveBefore - aliveAfter;
            a.Hits += caught.Count; a.Lost += lost;
            OnDamage?.Invoke(caught.Count, lost);
        }

        void Finish(Active a, Boss boss)
        {
            OnImpact?.Invoke(a);
            Current = null;
            NextAttack = FixedGap > 0f ? FixedGap + Random.value * FixedGapRandom
                : (boss != null && boss.HpFrac < GameConstants.FastAttackHpFrac)
                    ? GameConstants.NextAttackPhase2 : GameConstants.NextAttackPhase1;
            OnResolved?.Invoke(a.Hits, a.Lost);
        }
    }
}
