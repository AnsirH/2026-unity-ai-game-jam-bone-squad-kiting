using System.Collections.Generic;
using UnityEngine;

namespace BoneSquadKiting.Squad
{
    /// <summary>The one place damage to squad units is applied, so the squad leader rule lives in one spot.</summary>
    public static class SquadDamage
    {
        /// <summary>Test switch: while on, nothing hurts the squad. Only the move tuning scene's panel sets it.</summary>
        public static bool Invincible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Invincible = false;

        /// <summary>
        /// Deal damage aimed at <paramref name="victim"/>. The squad leader does not take damage while any
        /// other unit lives: another unit takes it instead (and may die from it). Once the leader is the
        /// last one standing it takes damage itself. Returns the unit that actually took the damage.
        /// </summary>
        public static SquadUnit Apply(IList<SquadUnit> units, SquadUnit victim, float amount)
        {
            if (Invincible) return victim;
            if (victim.IsLeader)
            {
                var stand = StandIn(units, victim);
                if (stand != null) victim = stand;
            }
            if (victim.Dead) return victim;
            victim.Hp -= amount;
            victim.Flash = 1f;
            if (victim.Hp <= 0f)
            {
                victim.Dead = true;
                BoneSquadKiting.Audio.SoundManager.Current?.PlayVolleySfx("squad.hit.bones");
            }
            return victim;
        }

        // Who takes the leader's damage: the living unit closest to the leader. (Not a settled rule — change here.)
        static SquadUnit StandIn(IList<SquadUnit> units, SquadUnit leader)
        {
            SquadUnit best = null; float bestSqr = float.MaxValue;
            foreach (var u in units)
            {
                if (u == leader || u.Dead) continue;
                float d = (new Vector2(u.X, u.Z) - new Vector2(leader.X, leader.Z)).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = u; }
            }
            return best;
        }
    }
}
