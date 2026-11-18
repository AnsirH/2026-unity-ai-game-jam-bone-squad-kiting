using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Squad
{
    public struct Shot
    {
        public long VisualId;
        public float X, Z, Life;
        public float StartY, InitialDistance, Travelled;
        public bool HasWorldOrigin;
        public int Owner;
        public IEnemy Target;
        public bool Heavy;      // thrown by the squad leader: drawn as its own projectile
        public Vector2 Dir;     // which way it is flying
        public float Damage;    // what it deals when it lands, fixed when it is fired
        public float Reserve;   // how much of the target's health was counted on for it
    }

    /// <summary>
    /// Unit auto-attacks: picks a target from the squad order, fires homing shots, lands hits.
    /// Shots follow their target and reserve their damage on it while in flight, so the squad does not
    /// pour twenty shots into a mob that two would kill.
    /// </summary>
    public class ShotSystem
    {
        static long nextVisualId;
        const float MuzzleOffset = 0.29f;

        public readonly List<Shot> Shots = new();
        readonly Dictionary<int, Vector3> _muzzlePositions = new();
        public System.Action<float> OnHit; // damage dealt to an enemy
        public event System.Action<Shot> OnLaunched;
        public event System.Action<Shot> OnTargetHit;
        public event System.Action<Vector2, float, Vector2> OnImpact;

        public float FireRange = GameConstants.FireRange; // per-unit range; the director keeps it in sync
        public float UnitDamage = GameConstants.UnitDamage;        // a soldier's shot
        public float UnitInterval = GameConstants.UnitCdMin, UnitIntervalRandom = GameConstants.UnitCdRand;
        public float LeaderDamage = GameConstants.UnitDamage;      // the squad leader's throw
        public float LeaderInterval = 1.35f, LeaderIntervalRandom = 0f;
        public float DamageMultiplier = 1f;    // applied to shots as they are fired (the near-miss buff); the director sets it
        public float ThreatRadius = 4f;        // a stopped unit shoots mobs this close before anything else
        public float AttackMoveRadius = 4f;    // attack-move prefers mobs this close to the clicked point

        /// <summary>Updates the animated model's projectile spawn point for one unit.</summary>
        public void SetMuzzlePosition(int owner, Vector3 worldPosition) => _muzzlePositions[owner] = worldPosition;

        /// <summary>
        /// Who a unit shoots, by order:
        /// AttackTarget — the ordered enemy while it lives and is in range, then as Move.
        /// AttackMove — mobs near the clicked point, then any mob in range, then an elite.
        /// Move — mobs inside the threat radius, then an elite.
        /// Mobs that already have enough damage flying at them are skipped.
        /// </summary>
        public static IEnemy Acquire(SquadUnit u, EnemyRegistry registry, SquadOrder order,
                                     float fireRange, float threatRadius = 4f, float attackMoveRadius = 4f)
        {
            var p = new Vector2(u.X, u.Z);
            if (order.Kind == OrderKind.AttackTarget && order.Target is { Alive: true } t
                && (t.Pos - p).magnitude < fireRange && NeedsShots(t))
                return t;

            IEnemy mob;
            if (order.Kind == OrderKind.AttackMove)
            {
                var point = order.Point;
                mob = registry.Nearest(point, attackMoveRadius, e => e.Kind == EnemyKind.Mob && NeedsShots(e) && (e.Pos - p).magnitude < fireRange);
                if (mob != null) return mob;
                mob = registry.Nearest(p, fireRange, e => e.Kind == EnemyKind.Mob && NeedsShots(e));
            }
            else mob = registry.Nearest(p, Mathf.Min(threatRadius, fireRange), e => e.Kind == EnemyKind.Mob && NeedsShots(e));
            if (mob != null) return mob;

            return registry.Nearest(p, fireRange, e => e.Kind == EnemyKind.Elite);
        }

        static bool NeedsShots(IEnemy e) => e.Hp > e.Reserved;

        public void Tick(IList<SquadUnit> units, EnemyRegistry registry, SquadOrder order, float dt)
        {
            foreach (var u in units)
            {
                u.AttackInterval = u.IsLeader ? LeaderInterval : UnitInterval;
                if (u.PendingTarget != null)
                {
                    // Mid wind-up: the shot leaves when the time is up (or earlier, when the model's
                    // animation event calls ReleasePending). Moving, dying or losing the target calls it off.
                    if (u.Dead || !FireRules.CanFire(u, order) || u.PendingTarget is not { Alive: true }) CancelPending(u);
                    else if ((u.WindupLeft -= dt) <= 0f) ReleasePending(u);
                    continue;
                }
                if (u.Dead || u.Cd > 0 || !FireRules.CanFire(u, order)) continue;
                var target = Acquire(u, registry, order, FireRange, ThreatRadius, AttackMoveRadius);
                if (target == null) continue;
                float interval = u.IsLeader ? LeaderInterval + Random.value * LeaderIntervalRandom : UnitInterval + Random.value * UnitIntervalRandom;
                u.Cd = Mathf.Max(interval, u.AttackDuration);     // never sooner than its attack motion allows
                u.PendingReserve = u.IsLeader ? LeaderDamage : UnitDamage;
                target.Reserved += u.PendingReserve;
                u.Facing = (target.Pos - new Vector2(u.X, u.Z)).normalized;   // turn to face what it shoots
                u.ShotCount++;                                               // the view starts the attack motion on this
                u.PendingTarget = target;
                // A visible model releases on its animation event. A culled Animator can
                // skip events, so finish the wind-up at the end of that attack as a fallback.
                u.WindupLeft = u.HasReleaseEvent ? u.AttackDuration : u.AttackWindup;
                if (u.AttackWindup <= 0f) ReleasePending(u);
            }

            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                var s = Shots[i];
                s.Life -= dt;
                if (s.Target is not { Alive: true } || s.Life <= 0)
                {
                    Release(s); Shots.RemoveAt(i);   // target died or the shot ran out: it does not retarget
                    continue;
                }
                var to = s.Target.Pos - new Vector2(s.X, s.Z);
                float dist = to.magnitude, step = GameConstants.ShotSpeed * dt;
                if (dist - step < s.Target.Radius)
                {
                    var impactDir = dist > 1e-6f ? to / dist : s.Dir;
                    var impactPosition = s.Target.Pos - impactDir * Mathf.Min(dist, s.Target.Radius);
                    OnImpact?.Invoke(impactPosition, s.Target.Radius, impactDir);
                    OnTargetHit?.Invoke(s);
                    Release(s);
                    (s.Target as IDamageable)?.ApplyDamage(s.Damage);
                    OnHit?.Invoke(s.Damage);
                    Shots.RemoveAt(i);
                    continue;
                }
                var dir = to / dist;
                s.Dir = dir;
                s.Travelled += Mathf.Min(step, dist);
                s.X += dir.x * step; s.Z += dir.y * step;
                Shots[i] = s;
            }
        }

        /// <summary>
        /// Let go of the shot a unit is winding up, now. Called when the wind-up time runs out, or by the
        /// model's attack animation event at the moment the hand opens. Does nothing without a pending shot.
        /// </summary>
        public void ReleasePending(SquadUnit u)
        {
            var target = u.PendingTarget;
            if (target == null) return;
            u.PendingTarget = null;
            if (u.Dead || target is not { Alive: true }) { ReleaseReserve(target, u.PendingReserve); return; }
            var dir = (target.Pos - new Vector2(u.X, u.Z)).normalized;
            bool hasMuzzle = _muzzlePositions.TryGetValue(u.Id, out var muzzle);
            var start = hasMuzzle
                ? new Vector2(muzzle.x, muzzle.z)
                : new Vector2(u.X, u.Z) + dir * MuzzleOffset;
            Shots.Add(new Shot
            {
                VisualId = ++nextVisualId,
                X = start.x, Z = start.y, StartY = hasMuzzle ? muzzle.y : 0f,
                InitialDistance = Mathf.Max(0.01f, Vector2.Distance(start, target.Pos)),
                HasWorldOrigin = hasMuzzle,
                Life = GameConstants.ShotLife, Owner = u.Id, Target = target, Heavy = u.IsLeader, Dir = dir,
                Damage = u.PendingReserve * DamageMultiplier, Reserve = u.PendingReserve,
            });
            OnLaunched?.Invoke(Shots[Shots.Count - 1]);
        }

        void CancelPending(SquadUnit u)
        {
            ReleaseReserve(u.PendingTarget, u.PendingReserve);
            u.PendingTarget = null;
        }

        static void ReleaseReserve(IEnemy target, float amount)
        {
            if (target != null) target.Reserved = Mathf.Max(0f, target.Reserved - amount);
        }

        static void Release(Shot s)
        {
            if (s.Target != null) s.Target.Reserved = Mathf.Max(0f, s.Target.Reserved - s.Reserve);
        }
    }
}
