using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Bosses
{
    /// <summary>Boss state. Stationary in M1; M2 enemies carry their own moving Pos.</summary>
    public class Boss : IEnemy, IDamageable
    {
        public float MaxHp = GameConstants.BossMax;
        public float Hp = GameConstants.BossMax;
        public int Phase = 1;
        public float Flash;
        /// <summary>Health cannot be taken below this (a fight raises it while a phase must not be skipped).</summary>
        public float HpFloor;
        /// <summary>Where it stands, and which way it faces (degrees around Y, clockwise seen from above).</summary>
        public Vector2 Position = new(GameConstants.BossX, GameConstants.BossZ);
        public float Facing;

        public float HpFrac => MaxHp > 0f ? Hp / MaxHp : 0f;
        public Vector2 Pos => Position;
        public bool Alive => Hp > 0;
        public float Radius => GameConstants.ShotHitRadius;
        public EnemyKind Kind => EnemyKind.Elite;
        float IEnemy.Hp => Hp;

        public void ApplyDamage(float amount)
        {
            Hp = Mathf.Max(HpFloor, Hp - amount);
            Flash = 0.8f;
        }
    }
}
