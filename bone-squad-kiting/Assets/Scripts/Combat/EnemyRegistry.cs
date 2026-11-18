using System.Collections.Generic;
using UnityEngine;

namespace BoneSquadKiting.Combat
{
    /// <summary>Active enemies. M1 registers only the boss; M2 mobs register the same way.</summary>
    public class EnemyRegistry
    {
        public readonly List<IEnemy> Enemies = new();

        public void Register(IEnemy e) { if (!Enemies.Contains(e)) Enemies.Add(e); }
        public void Unregister(IEnemy e) { Enemies.Remove(e); }

        /// <summary>Closest living enemy within range of a point, or null.</summary>
        public IEnemy NearestInRange(Vector2 from, float range)
        {
            IEnemy best = null;
            float bestSqr = range * range;
            foreach (var e in Enemies)
            {
                if (!e.Alive) continue;
                float d = (e.Pos - from).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = e; }
            }
            return best;
        }

        /// <summary>Closest living enemy within range that passes the filter, or null.</summary>
        public IEnemy Nearest(Vector2 from, float range, System.Func<IEnemy, bool> filter)
        {
            IEnemy best = null;
            float bestSqr = range * range;
            foreach (var e in Enemies)
            {
                if (!e.Alive) continue;
                float d = (e.Pos - from).sqrMagnitude;
                if (d > bestSqr || !filter(e)) continue;
                bestSqr = d; best = e;
            }
            return best;
        }
    }
}
