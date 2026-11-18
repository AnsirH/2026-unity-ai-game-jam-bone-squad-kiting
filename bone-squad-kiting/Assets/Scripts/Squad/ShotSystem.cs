using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Squad
{
    public struct Shot
    {
        public float X, Z, Life;
        public int Owner;
        public IEnemy Target;
    }

    /// <summary>
    /// Unit auto-attacks: picks a target from the squad order, fires homing shots, lands hits.
    /// Shots follow their target and reserve their damage on it while in flight, so the squad does not
    /// pour twenty shots into a mob that two would kill.
    /// </summary>
    public class ShotSystem
    {
        const float MuzzleOffset = 0.29f;

        public readonly List<Shot> Shots = new();
        public System.Action<float> OnHit; // damage dealt to an enemy

        public float FireRange = GameConstants.FireRange; // per-unit range; the director keeps it in sync
        public float ThreatRadius = 4f;        // a stopped unit shoots mobs this close before anything else
        public float AttackMoveRadius = 4f;    // attack-move prefers mobs this close to the clicked point

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
                if (u.Dead || u.Cd > 0 || !FireRules.CanFire(u, order)) continue;
                var target = Acquire(u, registry, order, FireRange, ThreatRadius, AttackMoveRadius);
                if (target == null) continue;
                u.Cd = GameConstants.UnitCdMin + Random.value * GameConstants.UnitCdRand;
                var dir = (target.Pos - new Vector2(u.X, u.Z)).normalized;
                Shots.Add(new Shot
                {
                    X = u.X + dir.x * MuzzleOffset, Z = u.Z + dir.y * MuzzleOffset,
                    Life = GameConstants.ShotLife, Owner = u.Id, Target = target,
                });
                target.Reserved += GameConstants.UnitDamage;
                u.Facing = dir;   // turn to face what it shoots
                u.ShotCount++;
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
                    Release(s);
                    (s.Target as IDamageable)?.ApplyDamage(GameConstants.UnitDamage);
                    OnHit?.Invoke(GameConstants.UnitDamage);
                    Shots.RemoveAt(i);
                    continue;
                }
                var dir = to / dist;
                s.X += dir.x * step; s.Z += dir.y * step;
                Shots[i] = s;
            }
        }

        static void Release(Shot s)
        {
            if (s.Target != null) s.Target.Reserved = Mathf.Max(0f, s.Target.Reserved - GameConstants.UnitDamage);
        }
    }
}
