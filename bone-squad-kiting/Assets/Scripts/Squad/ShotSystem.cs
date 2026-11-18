using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Squad
{
    public struct Shot
    {
        public float X, Z, Vx, Vz, Life;
        public int Owner;
        public IEnemy Target;
    }

    /// <summary>Unit auto-attacks: target acquisition from the squad order, projectile flight and hits.</summary>
    public class ShotSystem
    {
        const float MuzzleOffset = 0.29f;
        const float BoundsX = 15f, BoundsZ = 13f;

        public readonly List<Shot> Shots = new();
        public System.Action<float> OnHit; // damage dealt to an enemy

        /// <summary>An AttackTarget order's living, in-range target wins; otherwise the nearest enemy in range.</summary>
        public static IEnemy Acquire(SquadUnit u, EnemyRegistry registry, SquadOrder order)
        {
            var p = new Vector2(u.X, u.Z);
            if (order.Kind == OrderKind.AttackTarget && order.Target is { Alive: true } t
                && (t.Pos - p).magnitude < GameConstants.FireRange)
                return t;
            return registry.NearestInRange(p, GameConstants.FireRange);
        }

        /// <param name="squadMoving">True while the squad anchor is walking; see <see cref="FireRules"/>.</param>
        public void Tick(IList<SquadUnit> units, EnemyRegistry registry, SquadOrder order, bool squadMoving, float dt)
        {
            foreach (var u in units)
            {
                if (u.Dead || u.Cd > 0 || !FireRules.CanFire(u, order, squadMoving)) continue;
                var target = Acquire(u, registry, order);
                if (target == null) continue;
                u.Cd = GameConstants.UnitCdMin + Random.value * GameConstants.UnitCdRand;
                float ang = Mathf.Atan2(target.Pos.y - u.Z, target.Pos.x - u.X);
                float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                Shots.Add(new Shot
                {
                    X = u.X + cos * MuzzleOffset, Z = u.Z + sin * MuzzleOffset,
                    Vx = cos * GameConstants.ShotSpeed, Vz = sin * GameConstants.ShotSpeed,
                    Life = GameConstants.ShotLife, Owner = u.Id, Target = target,
                });
                u.ShotCount++;
            }

            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                var s = Shots[i];
                s.X += s.Vx * dt; s.Z += s.Vz * dt; s.Life -= dt;
                bool hit = s.Target is { Alive: true }
                           && (s.Target.Pos - new Vector2(s.X, s.Z)).magnitude < GameConstants.ShotHitRadius;
                if (hit)
                {
                    (s.Target as IDamageable)?.ApplyDamage(GameConstants.UnitDamage);
                    OnHit?.Invoke(GameConstants.UnitDamage);
                    Shots.RemoveAt(i);
                }
                else if (s.Life <= 0 || Mathf.Abs(s.X) > BoundsX || Mathf.Abs(s.Z) > BoundsZ) Shots.RemoveAt(i);
                else Shots[i] = s;
            }
        }
    }
}
